import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalate: vi.fn(),
    getEscalations: vi.fn(),
  },
}));

const escalate = vi.mocked(ticketsApi.escalate);

const ticket: TicketDetail = {
  id: 2,
  reference: 'TCK-0002',
  title: 'Export to CSV produces empty file',
  description: 'The export button downloads a 0 KB file.',
  status: 'New',
  priority: 'Medium',
  customer: { id: 2, name: 'Fabrikam', tier: 'Standard', email: 'help@fabrikam.example', phone: null },
  category: { id: 3, name: 'Technical', requiresSpecialist: false },
  assignedAgent: null,
  createdAtUtc: '2026-10-29T09:00:00Z',
  updatedAtUtc: '2026-10-29T09:00:00Z',
  dueAtUtc: '2026-10-30T09:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.mocked(ticketsApi.getEscalations).mockResolvedValue([]);
    escalate.mockReset();
  });

  it('blocks a reason shorter than 5 characters without calling the API', async () => {
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticket} onUpdated={vi.fn()} />);

    await user.type(screen.getByLabelText('Reason'), '  Lost  ');
    await user.click(screen.getByRole('button', { name: 'Escalate one level' }));

    expect(screen.getByText('Give a reason of 5 to 500 characters.')).toBeInTheDocument();
    expect(escalate).not.toHaveBeenCalled();
  });

  it('shows a 409 from the server and leaves the ticket unchanged', async () => {
    escalate.mockRejectedValue(
      new ApiError(409, 'Ticket TCK-0002 is already Critical and cannot be escalated further.'),
    );
    const onUpdated = vi.fn();
    const user = userEvent.setup();
    render(<EscalationPanel ticket={ticket} onUpdated={onUpdated} />);

    await user.type(screen.getByLabelText('Reason'), 'Customer is losing sales');
    await user.click(screen.getByRole('button', { name: 'Escalate one level' }));

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent('already Critical and cannot be escalated further'),
    );
    expect(onUpdated).not.toHaveBeenCalled();
  });

  it('is disabled for a Critical ticket', () => {
    render(<EscalationPanel ticket={{ ...ticket, priority: 'Critical' }} onUpdated={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Escalate one level' })).toBeDisabled();
    expect(screen.getByText('Already Critical: there is no higher priority.')).toBeInTheDocument();
  });
});
