import { useCallback, useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import { useAsyncData } from '../../../hooks/useAsyncData';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail } from '../../../types/api';

/** The same limits as the API's EscalateTicketRequestValidator, so bad input never reaches it. */
export const reasonMinLength = 5;
export const reasonMaxLength = 500;

interface EscalationPanelProps {
  ticket: TicketDetail;
  onUpdated: (ticket: TicketDetail) => void;
}

/** Escalates the ticket one priority level, and shows its escalation history. */
export function EscalationPanel({ ticket, onUpdated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('');
  const [errors, setErrors] = useState<{ reason?: string; escalatedBy?: string }>({});
  const [serverError, setServerError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const loadHistory = useCallback(() => ticketsApi.getEscalations(ticket.id), [ticket.id]);
  const history = useAsyncData(loadHistory, 'Unable to load the escalation history.');

  const blockedBecause =
    ticket.status === 'Resolved' || ticket.status === 'Closed'
      ? `A ${ticket.status.toLowerCase()} ticket cannot be escalated.`
      : ticket.priority === 'Critical'
        ? 'Already Critical: there is no higher priority.'
        : undefined;

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setServerError(undefined);

    const found = validate(reason, escalatedBy);
    setErrors(found);

    if (found.reason || found.escalatedBy) {
      return;
    }

    setIsSaving(true);

    try {
      const result = await ticketsApi.escalate(ticket.id, { reason: reason.trim(), escalatedBy: escalatedBy.trim() });
      onUpdated(result.ticket);
      setReason('');
      await history.reload();
    } catch (caught) {
      setServerError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="card">
      <h2>Escalate</h2>

      {blockedBecause && <p className="field__hint">{blockedBecause}</p>}

      <form onSubmit={(event) => void submit(event)} noValidate>
        <Field
          id="escalation-reason"
          label="Reason"
          error={errors.reason}
          hint={`${reasonMinLength} to ${reasonMaxLength} characters.`}
        >
          {(props) => (
            <textarea
              {...props}
              rows={3}
              value={reason}
              disabled={Boolean(blockedBecause)}
              onChange={(event) => setReason(event.target.value)}
            />
          )}
        </Field>

        <Field id="escalation-by" label="Escalated by" error={errors.escalatedBy}>
          {(props) => (
            <input
              {...props}
              value={escalatedBy}
              disabled={Boolean(blockedBecause)}
              onChange={(event) => setEscalatedBy(event.target.value)}
            />
          )}
        </Field>

        <button
          type="submit"
          className="button button--primary"
          disabled={Boolean(blockedBecause) || isSaving}
        >
          {isSaving ? 'Escalating...' : 'Escalate one level'}
        </button>
      </form>

      {serverError && (
        <p className="field__error" role="alert">
          {serverError}
        </p>
      )}

      <h3>History</h3>

      {history.error && <p className="field__error">{history.error}</p>}

      {history.data?.length === 0 && <p className="field__hint">Never escalated.</p>}

      <ol className="escalation-history">
        {history.data?.map((entry) => (
          <li key={entry.id}>
            <p>
              <strong>{entry.fromPriority} → {entry.toPriority}</strong> by {entry.escalatedBy},{' '}
              {formatDateTime(entry.escalatedAtUtc)}
            </p>
            <p>
              Agent: {entry.fromAgent?.fullName ?? 'Unassigned'} → {entry.toAgent?.fullName ?? 'Unassigned'}.
              Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}.
            </p>
            <p>{entry.reason}</p>
          </li>
        ))}
      </ol>
    </div>
  );
}

function validate(reason: string, escalatedBy: string) {
  const errors: { reason?: string; escalatedBy?: string } = {};
  const length = reason.trim().length;

  if (length < reasonMinLength || length > reasonMaxLength) {
    errors.reason = `Give a reason of ${reasonMinLength} to ${reasonMaxLength} characters.`;
  }

  if (escalatedBy.trim() === '') {
    errors.escalatedBy = 'Say who is escalating it.';
  }

  return errors;
}
