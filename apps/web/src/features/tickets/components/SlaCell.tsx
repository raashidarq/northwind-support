import { Badge } from '../../../components/Badge';
import { formatDateTime } from '../../../lib/format';
import type { SlaStatus } from '../../../types/api';
import { slaLabels } from './slaLabels';

interface SlaCellProps {
  dueAtUtc: string | null;
  slaStatus: SlaStatus;
}

/** The due date and where the ticket stands against it. Used on the list and the detail page. */
export function SlaCell({ dueAtUtc, slaStatus }: SlaCellProps) {
  const label = slaLabels[slaStatus];

  return (
    <div className="sla-cell">
      <Badge tone={label.tone}>{label.text}</Badge>
      <span className="sla-cell__due">{formatDateTime(dueAtUtc)}</span>
    </div>
  );
}
