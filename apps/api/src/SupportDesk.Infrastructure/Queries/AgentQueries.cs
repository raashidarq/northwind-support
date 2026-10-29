using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class AgentQueries(SupportDbContext db) : IAgentQueries
{
    public async Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .OrderBy(a => a.FullName)
            .Select(a => new AgentDto(
                a.Id,
                a.FullName,
                a.Email,
                a.IsActive,
                a.MaxOpenTickets,
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed),
                (from specialization in a.Specializations
                 join category in db.Set<Category>() on specialization.CategoryId equals category.Id
                 select new CategorySummaryDto(category.Id, category.Name))
                .ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AgentCandidate>> GetCandidatesAsync(CancellationToken ct) =>
        await db.Set<Agent>()
            .AsNoTracking()
            .Select(a => new AgentCandidate(
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                a.Specializations.Select(s => s.CategoryId).ToList(),
                db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id &&
                    t.Status != TicketStatus.Resolved &&
                    t.Status != TicketStatus.Closed)))
            .ToListAsync(ct);
}
