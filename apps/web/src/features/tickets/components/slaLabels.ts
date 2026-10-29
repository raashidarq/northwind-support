import type { BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

/**
 * Every SLA state, with its own words as well as its own colour, so it never relies on
 * colour alone. A full Record: adding a state without a label is a type error.
 */
export const slaLabels: Record<SlaStatus, { text: string; tone: BadgeTone }> = {
  WithinSla: { text: 'Within SLA', tone: 'info' },
  AtRisk: { text: 'At risk', tone: 'warning' },
  Breached: { text: 'Breached', tone: 'danger' },
  Met: { text: 'Met', tone: 'success' },
  NotApplicable: { text: 'No SLA', tone: 'neutral' },
};
