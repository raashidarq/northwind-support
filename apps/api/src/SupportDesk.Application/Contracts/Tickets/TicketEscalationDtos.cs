using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>One row of a ticket's escalation history: what changed, why, who and when.</summary>
public sealed record TicketEscalationDto(
    int Id,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>Response of POST /api/tickets/{id}/escalate: the ticket after it, and the row it wrote.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);
