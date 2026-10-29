using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the ticket list query against a real (SQLite, in-memory) database, so the filters are
/// proven to translate to SQL rather than just to work on a C# list.
/// </summary>
public sealed class TicketQueriesTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    private readonly Customer _contoso = new("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, Now);
    private readonly Customer _fabrikam = new("Fabrikam", "help@fabrikam.example", null, CustomerTier.Standard, Now);

    public TicketQueriesTests()
    {
        // The in-memory database lives only as long as this connection is open.
        _connection.Open();
        _db = new SupportDbContext(new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _queries = new TicketQueries(_db, new FixedClock(), TestSla.Policy());

        var general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
        var billing = new Category("Billing", requiresSpecialist: false, forcesCriticalPriority: false);
        var agent = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, Now);
        _db.AddRange(_contoso, _fabrikam, general, billing, agent);
        _db.SaveChanges();

        _db.AddRange(
            NewTicket("TCK-0001", "Printer jammed", _contoso, general, TicketPriority.High, TicketStatus.New, agentId: null),
            NewTicket("TCK-0002", "Invoice is wrong", _fabrikam, billing, TicketPriority.Low, TicketStatus.InProgress, agent.Id),
            NewTicket("TCK-0003", "Cannot log in", _fabrikam, general, TicketPriority.High, TicketStatus.InProgress, agent.Id),
            NewTicket("TCK-0004", "Invoice missing", _contoso, billing, TicketPriority.Medium, TicketStatus.Open, agentId: null));
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetPaged_ByPriority_ReturnsOnlyThatPriority()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Priority = TicketPriority.High }, default);

        Assert.Equal(["TCK-0001", "TCK-0003"], References(result.Items));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetPaged_WithSeveralFilters_ReturnsTicketsMatchingAllOfThem()
    {
        var query = new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CustomerId = _fabrikam.Id
        };

        var result = await _queries.GetPagedAsync(query, default);

        Assert.Equal(["TCK-0003"], References(result.Items));
    }

    [Fact]
    public async Task GetPaged_SearchByCustomerName_IsTrimmedAndMatchesTheCustomer()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = "  Contoso " }, default);

        Assert.Equal(["TCK-0001", "TCK-0004"], References(result.Items));
    }

    [Fact]
    public async Task GetPaged_BlankSearch_IsIgnored()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { Search = "   " }, default);

        Assert.Equal(4, result.TotalCount);
    }

    [Fact]
    public async Task GetPaged_UnassignedOnly_ReturnsTicketsWithoutAnAgent()
    {
        var result = await _queries.GetPagedAsync(new TicketQuery { UnassignedOnly = true }, default);

        Assert.Equal(["TCK-0001", "TCK-0004"], References(result.Items));
    }

    [Fact]
    public async Task GetPaged_TotalCount_CountsFilteredTicketsNotJustThePage()
    {
        var query = new TicketQuery { Priority = TicketPriority.High, PageSize = 1 };

        var result = await _queries.GetPagedAsync(query, default);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetEscalations_ReturnsTheHistoryNewestFirstWithAgentNames()
    {
        // Medium and unassigned, so it can be escalated twice: to High, then Critical.
        var ticket = _db.Set<Ticket>().Single(t => t.Reference == "TCK-0004");
        var agentId = _db.Set<Agent>().Single().Id;
        ticket.Escalate("First escalation", "Team lead", TestSla.Policy(), CustomerTier.Premium, agentId, Now.AddHours(1));
        ticket.Escalate("Second escalation", "Team lead", TestSla.Policy(), CustomerTier.Premium, agentId, Now.AddHours(2));
        await _db.SaveChangesAsync();

        var history = await _queries.GetEscalationsAsync(ticket.Id, default);

        Assert.NotNull(history);
        Assert.Equal(["Second escalation", "First escalation"], history.Select(e => e.Reason));
        Assert.Null(history[1].FromAgent);
        Assert.Equal("Alex Turner", history[1].ToAgent?.FullName);
    }

    [Fact]
    public async Task GetEscalations_ForAnUnknownTicket_IsNull() =>
        Assert.Null(await _queries.GetEscalationsAsync(999, default));

    private static Ticket NewTicket(
        string reference, string title, Customer customer, Category category,
        TicketPriority priority, TicketStatus status, int? agentId)
    {
        var ticket = Ticket.Raise(
            reference, title, "A description long enough to be realistic.", customer.Id, category.Id, priority, Now);

        ticket.AssignTo(agentId, Now);

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, Now);
        }

        return ticket;
    }

    /// <summary>Ordered, so a test does not depend on the default sort.</summary>
    private static string[] References(IEnumerable<TicketListItemDto> items) =>
        items.Select(i => i.Reference).Order().ToArray();
}
