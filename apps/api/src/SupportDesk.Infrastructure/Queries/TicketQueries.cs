using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Reports;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>
/// Ticket reads. Projected in the database and never tracked. Aggregates hold only each
/// other's ids, so the customer, category and agent labels are joined in here, on the read side.
/// </summary>
public sealed class TicketQueries(SupportDbContext db, IClock clock, SlaPolicy sla) : ITicketQueries
{
    /// <remarks>
    /// Filters, then counts, then sorts and pages, all in the database. Filtering comes before
    /// the count so the total matches the filtered list.
    /// </remarks>
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        // One "now" for both the SLA filter and the SLA status shown, so they cannot disagree.
        var now = clock.UtcNow;

        var tickets = ApplyFilters(TicketsWithLabels(), query, now);

        var totalCount = await tickets.CountAsync(ct);

        var items = await ApplySort(tickets, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, sla.AtRiskPeriodFor(x.Ticket.Priority, x.Customer.Tier))
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return TicketsWithLabels()
            .Where(x => x.Ticket.Id == id)
            .Select(x => new TicketDetailDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Description = x.Ticket.Description,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerContactDto(
                    x.Customer.Id, x.Customer.Name, x.Customer.Email, x.Customer.Phone, x.Customer.Tier),
                Category = new CategoryDto(x.Category.Id, x.Category.Name, x.Category.RequiresSpecialist),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, sla.AtRiskPeriodFor(x.Ticket.Priority, x.Customer.Tier))
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return await TicketsWithLabels()
            .Where(x => x.Ticket.CustomerId == customerId)
            .OrderByDescending(x => x.Ticket.CreatedAtUtc)
            .ThenByDescending(x => x.Ticket.Id)
            .Select(x => new TicketListItemDto
            {
                Id = x.Ticket.Id,
                Reference = x.Ticket.Reference,
                Title = x.Ticket.Title,
                Status = x.Ticket.Status,
                Priority = x.Ticket.Priority,
                Customer = new CustomerSummaryDto(x.Customer.Id, x.Customer.Name, x.Customer.Tier),
                Category = new CategorySummaryDto(x.Category.Id, x.Category.Name),
                AssignedAgent = x.Agent == null ? null : new AgentSummaryDto(x.Agent.Id, x.Agent.FullName),
                CreatedAtUtc = x.Ticket.CreatedAtUtc,
                UpdatedAtUtc = x.Ticket.UpdatedAtUtc,
                DueAtUtc = x.Ticket.DueAtUtc,
                ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
                SlaStatus = SlaEvaluator.Evaluate(
                    x.Ticket.DueAtUtc, x.Ticket.ResolvedAtUtc, now, sla.AtRiskPeriodFor(x.Ticket.Priority, x.Customer.Tier))
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TicketEscalationDto>?> GetEscalationsAsync(int ticketId, CancellationToken ct)
    {
        if (!await db.Set<Ticket>().AnyAsync(t => t.Id == ticketId, ct))
        {
            return null;
        }

        return await (
            from escalation in db.Set<TicketEscalation>().AsNoTracking()
            where escalation.TicketId == ticketId
            from fromAgent in db.Set<Agent>().Where(a => a.Id == escalation.FromAgentId).DefaultIfEmpty()
            from toAgent in db.Set<Agent>().Where(a => a.Id == escalation.ToAgentId).DefaultIfEmpty()
            orderby escalation.EscalatedAtUtc descending, escalation.Id descending
            select new TicketEscalationDto(
                escalation.Id,
                escalation.FromPriority,
                escalation.ToPriority,
                fromAgent == null ? null : new AgentSummaryDto(fromAgent.Id, fromAgent.FullName),
                toAgent == null ? null : new AgentSummaryDto(toAgent.Id, toAgent.FullName),
                escalation.FromDueAtUtc,
                escalation.ToDueAtUtc,
                escalation.Reason,
                escalation.EscalatedBy,
                escalation.EscalatedAtUtc))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every ticket with the rows that label it. Filter and sort this, then project.
    /// </summary>
    private IQueryable<TicketWithLabels> TicketsWithLabels() =>
        from ticket in db.Set<Ticket>().AsNoTracking()
        join customer in db.Set<Customer>() on ticket.CustomerId equals customer.Id
        join category in db.Set<Category>() on ticket.CategoryId equals category.Id
        from agent in db.Set<Agent>().Where(a => a.Id == ticket.AssignedAgentId).DefaultIfEmpty()
        select new TicketWithLabels { Ticket = ticket, Customer = customer, Category = category, Agent = agent };

    /// <summary>
    /// One <c>Where</c> per filter that was sent; EF Core joins them with AND into the SQL.
    /// </summary>
    private IQueryable<TicketWithLabels> ApplyFilters(IQueryable<TicketWithLabels> source, TicketQuery query, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x =>
                x.Ticket.Title.Contains(search) ||
                x.Ticket.Reference.Contains(search) ||
                x.Customer.Name.Contains(search));
        }

        if (query.Status is { } status)
        {
            source = source.Where(x => x.Ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            source = source.Where(x => x.Ticket.Priority == priority);
        }

        if (query.CategoryId is { } categoryId)
        {
            source = source.Where(x => x.Ticket.CategoryId == categoryId);
        }

        if (query.CustomerId is { } customerId)
        {
            source = source.Where(x => x.Ticket.CustomerId == customerId);
        }

        if (query.AssignedAgentId is { } agentId)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == agentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(x => x.Ticket.AssignedAgentId == null);
        }

        if (query.SlaStatus is { } slaStatus)
        {
            source = ApplySlaFilter(source, slaStatus, now);
        }

        return source;
    }

    /// <summary>
    /// The same decisions as <see cref="SlaEvaluator"/>, written as date comparisons so that
    /// SQL can make them; SQL cannot run the evaluator itself.
    /// </summary>
    /// <remarks>
    /// A ticket is at risk once its due date is no later than now plus its at-risk period, and
    /// that period differs by priority and customer tier. So the cut-off for each combination is
    /// worked out here, and SQL picks the right one per row with a CASE. No date arithmetic
    /// happens in SQL.
    /// </remarks>
    private IQueryable<TicketWithLabels> ApplySlaFilter(IQueryable<TicketWithLabels> source, SlaStatus status, DateTime now)
    {
        var c = AtRiskCutOffsAt(now);

        return status switch
        {
            SlaStatus.NotApplicable => source.Where(x => x.Ticket.DueAtUtc == null),

            SlaStatus.Met => source.Where(x =>
                x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc),

            SlaStatus.Breached => source.Where(x =>
                (x.Ticket.ResolvedAtUtc != null && x.Ticket.ResolvedAtUtc > x.Ticket.DueAtUtc) ||
                (x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc < now)),

            SlaStatus.AtRisk => source.Where(x =>
                x.Ticket.ResolvedAtUtc == null && x.Ticket.DueAtUtc >= now &&
                x.Ticket.DueAtUtc <= (x.Customer.Tier == CustomerTier.Premium
                    ? (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalPremium
                        : x.Ticket.Priority == TicketPriority.High ? c.HighPremium
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumPremium
                        : c.LowPremium)
                    : (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalStandard
                        : x.Ticket.Priority == TicketPriority.High ? c.HighStandard
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumStandard
                        : c.LowStandard))),

            SlaStatus.WithinSla => source.Where(x =>
                x.Ticket.ResolvedAtUtc == null &&
                x.Ticket.DueAtUtc > (x.Customer.Tier == CustomerTier.Premium
                    ? (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalPremium
                        : x.Ticket.Priority == TicketPriority.High ? c.HighPremium
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumPremium
                        : c.LowPremium)
                    : (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalStandard
                        : x.Ticket.Priority == TicketPriority.High ? c.HighStandard
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumStandard
                        : c.LowStandard))),

            _ => source
        };
    }

    /// <remarks>
    /// One SQL query: each ticket's SLA status is worked out as a CASE (the same comparisons as
    /// <see cref="ApplySlaFilter"/>), then the rows are grouped and counted with conditional
    /// COUNTs. Only one row per group comes back; the breach rate and rounding are done here.
    /// </remarks>
    public async Task<IReadOnlyList<SlaSummaryRowDto>> GetSlaSummaryAsync(
        SlaSummaryGrouping groupBy, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var c = AtRiskCutOffsAt(now);

        var tickets = TicketsWithLabels();

        if (from is { } start)
        {
            tickets = tickets.Where(x => x.Ticket.CreatedAtUtc >= start);
        }

        if (to is { } end)
        {
            tickets = tickets.Where(x => x.Ticket.CreatedAtUtc < end);
        }

        // Same order of decisions as SlaEvaluator.Evaluate.
        var rows = tickets.Select(x => new SlaReportRow
        {
            AgentId = x.Ticket.AssignedAgentId,
            AgentName = x.Agent == null ? null : x.Agent.FullName,
            Priority = x.Ticket.Priority,
            CreatedAtUtc = x.Ticket.CreatedAtUtc,
            ResolvedAtUtc = x.Ticket.ResolvedAtUtc,
            SlaStatus = x.Ticket.DueAtUtc == null ? SlaStatus.NotApplicable
                : x.Ticket.ResolvedAtUtc != null
                    ? (x.Ticket.ResolvedAtUtc <= x.Ticket.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
                : x.Ticket.DueAtUtc < now ? SlaStatus.Breached
                : x.Ticket.DueAtUtc <= (x.Customer.Tier == CustomerTier.Premium
                    ? (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalPremium
                        : x.Ticket.Priority == TicketPriority.High ? c.HighPremium
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumPremium
                        : c.LowPremium)
                    : (x.Ticket.Priority == TicketPriority.Critical ? c.CriticalStandard
                        : x.Ticket.Priority == TicketPriority.High ? c.HighStandard
                        : x.Ticket.Priority == TicketPriority.Medium ? c.MediumStandard
                        : c.LowStandard)) ? SlaStatus.AtRisk
                : SlaStatus.WithinSla
        });

        // Agent id as well as name, so two agents with the same name stay apart.
        Expression<Func<SlaReportRow, SlaGroupKey>> key = groupBy == SlaSummaryGrouping.Agent
            ? r => new SlaGroupKey { AgentId = r.AgentId, AgentName = r.AgentName }
            : r => new SlaGroupKey { Priority = r.Priority };

        var groups = await rows
            .GroupBy(key)
            .Select(g => new
            {
                g.Key,
                Total = g.Count(),
                Breached = g.Count(r => r.SlaStatus == SlaStatus.Breached),
                AtRisk = g.Count(r => r.SlaStatus == SlaStatus.AtRisk),
                Within = g.Count(r => r.SlaStatus == SlaStatus.WithinSla),
                Met = g.Count(r => r.SlaStatus == SlaStatus.Met),
                // AVG skips NULLs, so this is the average over resolved tickets only.
                AverageResolutionSeconds = g.Average(r => r.ResolvedAtUtc == null
                    ? null
                    : (double?)EF.Functions.DateDiffSecond(r.CreatedAtUtc, r.ResolvedAtUtc))
            })
            .ToListAsync(ct);

        return groups
            .OrderBy(g => g.Key.AgentName == null) // Unassigned last
            .ThenBy(g => g.Key.AgentName)
            .ThenByDescending(g => g.Key.Priority) // Critical first
            .Select(g =>
            {
                var withDueDate = g.Breached + g.AtRisk + g.Within + g.Met;

                return new SlaSummaryRowDto(
                    groupBy == SlaSummaryGrouping.Agent ? g.Key.AgentName ?? "Unassigned" : g.Key.Priority.ToString()!,
                    g.Total,
                    g.Breached,
                    g.AtRisk,
                    g.Within,
                    g.Met,
                    withDueDate == 0 ? null : Math.Round((double)g.Breached / withDueDate, 4),
                    g.AverageResolutionSeconds is { } seconds ? Math.Round(seconds / 3600, 2) : null);
            })
            .ToList();
    }

    private static IQueryable<TicketWithLabels> ApplySort(IQueryable<TicketWithLabels> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.UpdatedAtUtc)
                : source.OrderBy(x => x.Ticket.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(x => x.Ticket.DueAtUtc)
                : source.OrderBy(x => x.Ticket.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(x => x.Ticket.Priority)
                : source.OrderBy(x => x.Ticket.Priority),
            "status" => descending
                ? source.OrderByDescending(x => x.Ticket.Status)
                : source.OrderBy(x => x.Ticket.Status),
            _ => descending
                ? source.OrderByDescending(x => x.Ticket.CreatedAtUtc)
                : source.OrderBy(x => x.Ticket.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(x => x.Ticket.Id);
    }

    /// <summary>now + the at-risk period for each priority and tier; the SLA CASEs pick one per row.</summary>
    private AtRiskCutOffs AtRiskCutOffsAt(DateTime now)
    {
        DateTime CutOff(TicketPriority priority, CustomerTier tier) => now + sla.AtRiskPeriodFor(priority, tier);

        return new AtRiskCutOffs(
            CriticalStandard: CutOff(TicketPriority.Critical, CustomerTier.Standard),
            HighStandard: CutOff(TicketPriority.High, CustomerTier.Standard),
            MediumStandard: CutOff(TicketPriority.Medium, CustomerTier.Standard),
            LowStandard: CutOff(TicketPriority.Low, CustomerTier.Standard),
            CriticalPremium: CutOff(TicketPriority.Critical, CustomerTier.Premium),
            HighPremium: CutOff(TicketPriority.High, CustomerTier.Premium),
            MediumPremium: CutOff(TicketPriority.Medium, CustomerTier.Premium),
            LowPremium: CutOff(TicketPriority.Low, CustomerTier.Premium));
    }

    private sealed record AtRiskCutOffs(
        DateTime CriticalStandard, DateTime HighStandard, DateTime MediumStandard, DateTime LowStandard,
        DateTime CriticalPremium, DateTime HighPremium, DateTime MediumPremium, DateTime LowPremium);

    /// <summary>A ticket reduced to what the SLA summary report needs.</summary>
    private sealed class SlaReportRow
    {
        public int? AgentId { get; init; }

        public string? AgentName { get; init; }

        public TicketPriority Priority { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime? ResolvedAtUtc { get; init; }

        public SlaStatus SlaStatus { get; init; }
    }

    /// <summary>The report's group key; only the fields for the chosen grouping are set.</summary>
    private sealed class SlaGroupKey
    {
        public int? AgentId { get; init; }

        public string? AgentName { get; init; }

        public TicketPriority? Priority { get; init; }
    }

    private sealed class TicketWithLabels
    {
        public required Ticket Ticket { get; init; }

        public required Customer Customer { get; init; }

        public required Category Category { get; init; }

        public Agent? Agent { get; init; }
    }
}
