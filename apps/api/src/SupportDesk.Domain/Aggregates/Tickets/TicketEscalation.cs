namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// One escalation of a ticket: what changed, why and by whom. Part of the <see cref="Ticket"/>
/// aggregate and only created by <see cref="Ticket.Escalate"/>. History, so it has no way to
/// be changed once written.
/// </summary>
public sealed class TicketEscalation
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);

        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = reason.Trim();
        EscalatedBy = escalatedBy.Trim();
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int Id { get; private set; }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
