using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the SLA summary report (2.5) against a real (SQLite, in-memory) database, so the
/// grouping and conditional counts are proven to run as SQL.
/// </summary>
/// <remarks>
/// The tickets, all Standard tier, with "now" = <see cref="FixedClock.DefaultNow"/>:
/// <code>
/// ref    priority  agent  due in         resolved        SLA         created
/// H-W    High      Alex   2 h 1 s        -               WithinSla   now - 5:59:59
/// H-R    High      Alex   2 h            -               AtRisk      now - 6 h
/// H-B    High      Bea    -1 s           -               Breached    now - 8:00:01
/// H-M    High      Bea    -1 h           1 h before due  Met         now - 9 h (7 h to resolve)
/// H-BL   High      -      -1 h           1 h after due   Breached    now - 9 h (9 h to resolve)
/// H-N    High      -      no due date    -               N/A         now
/// C-B    Critical  -      -1 h           -               Breached    now - 5 h
/// L-N    Low       -      no due date    -               N/A         now
/// </code>
/// </remarks>
public sealed class SlaSummaryReportTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _executedSql = [];
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    public SlaSummaryReportTests()
    {
        _connection.Open();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IMethodCallTranslatorProvider, SqliteDateDiffTranslatorProvider>()
            .LogTo(_executedSql.Add, [RelationalEventId.CommandExecuted])
            .Options);
        _db.Database.EnsureCreated();
        _queries = new TicketQueries(_db, new FixedClock(), TestSla.Policy());

        var customer = new Customer("Fabrikam", "help@fabrikam.example", null, CustomerTier.Standard, Now);
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var alex = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, Now);
        var bea = new Agent("Bea Lopez", "bea@example.com", maxOpenTickets: 10, Now);
        _db.AddRange(customer, category, alex, bea);
        _db.SaveChanges();

        Add("H-W", TicketPriority.High, alex.Id, dueIn: TimeSpan.FromHours(2).Add(TimeSpan.FromSeconds(1)));
        Add("H-R", TicketPriority.High, alex.Id, dueIn: TimeSpan.FromHours(2));
        Add("H-B", TicketPriority.High, bea.Id, dueIn: TimeSpan.FromSeconds(-1));
        Add("H-M", TicketPriority.High, bea.Id, dueIn: TimeSpan.FromHours(-1), resolvedBeforeDue: true);
        Add("H-BL", TicketPriority.High, agentId: null, dueIn: TimeSpan.FromHours(-1), resolvedBeforeDue: false);
        Add("H-N", TicketPriority.High, agentId: null, dueIn: null);
        Add("C-B", TicketPriority.Critical, agentId: null, dueIn: TimeSpan.FromHours(-1));
        Add("L-N", TicketPriority.Low, agentId: null, dueIn: null);
        _db.SaveChanges();

        void Add(string reference, TicketPriority priority, int? agentId, TimeSpan? dueIn, bool? resolvedBeforeDue = null)
        {
            var window = TestSla.Policy().WindowFor(priority, customer.Tier);
            var start = dueIn is null ? Now : Now + dueIn.Value - window;

            var ticket = Ticket.Raise(
                reference, "SLA case", "A description long enough to be realistic.", customer.Id, category.Id,
                priority, start);
            ticket.AssignTo(agentId, start);

            if (dueIn is not null)
            {
                ticket.StartSlaWindow(window, start);
            }

            if (resolvedBeforeDue is { } early)
            {
                ticket.ChangeStatus(TicketStatus.Resolved, start + window + TimeSpan.FromHours(early ? -1 : 1));
            }

            _db.Add(ticket);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ByPriority_CountsEachSlaStateMostUrgentFirst()
    {
        var rows = await _queries.GetSlaSummaryAsync(SlaSummaryGrouping.Priority, null, null, default);

        Assert.Equal(
            [
                new SlaSummaryRowDto("Critical", Total: 1, Breached: 1, AtRisk: 0, Within: 0, Met: 0, 1.0, null),
                // Breach rate 2 of the 5 with a due date; resolution hours (7 + 9) / 2.
                new SlaSummaryRowDto("High", 6, 2, 1, 1, 1, 0.4, 8.0),
                // No ticket with a due date, so no breach rate.
                new SlaSummaryRowDto("Low", 1, 0, 0, 0, 0, null, null)
            ],
            rows);
    }

    [Fact]
    public async Task ByAgent_PutsUnassignedTicketsInTheirOwnGroupLast()
    {
        var rows = await _queries.GetSlaSummaryAsync(SlaSummaryGrouping.Agent, null, null, default);

        Assert.Equal(
            [
                new SlaSummaryRowDto("Alex Turner", Total: 2, Breached: 0, AtRisk: 1, Within: 1, Met: 0, 0.0, null),
                new SlaSummaryRowDto("Bea Lopez", 2, 1, 0, 0, 1, 0.5, 7.0),
                new SlaSummaryRowDto("Unassigned", 4, 2, 0, 0, 0, 1.0, 9.0)
            ],
            rows);
    }

    [Fact]
    public async Task DateRange_IncludesFromAndExcludesTo()
    {
        // H-R was created exactly at "from"; H-N and L-N exactly at "to".
        var rows = await _queries.GetSlaSummaryAsync(
            SlaSummaryGrouping.Priority, from: Now.AddHours(-6), to: Now, default);

        Assert.Equal(["Critical: 1", "High: 2"], rows.Select(r => $"{r.Group}: {r.Total}").Order());
    }

    [Fact]
    public async Task RunsAsOneGroupByQuery()
    {
        _executedSql.Clear();

        await _queries.GetSlaSummaryAsync(SlaSummaryGrouping.Agent, Now.AddDays(-1), Now, default);

        var sql = Assert.Single(_executedSql);
        Assert.Contains("GROUP BY", sql);
    }
}
