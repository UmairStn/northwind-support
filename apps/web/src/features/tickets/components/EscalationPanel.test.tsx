import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    escalateTicket: vi.fn(),
  },
}));

const mockEscalateTicket = vi.mocked(ticketsApi.escalateTicket);

const baseTicket: TicketDetail = {
  id: 42,
  reference: 'TCK-0042',
  title: 'Cannot access payment gateway',
  description: 'All payments failing',
  status: 'Open',
  priority: 'High',
  customer: { id: 1, name: 'Contoso Ltd', email: 'contoso@example.com', phone: null, tier: 'Premium' },
  category: { id: 2, name: 'Billing', requiresSpecialist: false, forcesCriticalPriority: false },
  assignedAgent: { id: 1, fullName: 'Alex Turner', email: 'alex@example.com' },
  createdAtUtc: '2026-10-15T10:00:00Z',
  updatedAtUtc: '2026-10-15T10:00:00Z',
  dueAtUtc: '2026-10-15T18:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders escalation form for eligible ticket', () => {
    render(<EscalationPanel ticket={baseTicket} onEscalated={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Escalate ticket' })).toBeInTheDocument();
    expect(screen.getByLabelText(/escalation reason/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escalate ticket' })).toBeInTheDocument();
  });

  it('shows disabled message when ticket is already Critical', () => {
    render(<EscalationPanel ticket={{ ...baseTicket, priority: 'Critical' }} onEscalated={vi.fn()} />);

    expect(screen.getByText(/already at Critical priority/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });

  it('shows disabled message when ticket is Resolved or Closed', () => {
    render(<EscalationPanel ticket={{ ...baseTicket, status: 'Resolved' }} onEscalated={vi.fn()} />);

    expect(screen.getByText(/is resolved and cannot be escalated/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Escalate ticket' })).not.toBeInTheDocument();
  });

  it('validates minimum 5 characters before submitting', async () => {
    render(<EscalationPanel ticket={baseTicket} onEscalated={vi.fn()} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    fireEvent.change(textarea, { target: { value: 'abc' } });

    const submitBtn = screen.getByRole('button', { name: 'Escalate ticket' });
    fireEvent.click(submitBtn);

    expect(await screen.findByText('Reason must be at least 5 characters.')).toBeInTheDocument();
    expect(mockEscalateTicket).not.toHaveBeenCalled();
  });

  it('calls API and triggers onEscalated on valid submission', async () => {
    const onEscalated = vi.fn();
    const updatedTicket: TicketDetail = { ...baseTicket, priority: 'Critical' };
    const escalation = {
      id: 1,
      ticketId: 42,
      fromPriority: 'High' as const,
      toPriority: 'Critical' as const,
      fromAgentId: 1,
      fromAgentName: 'Alex Turner',
      toAgentId: 1,
      toAgentName: 'Alex Turner',
      fromDueAtUtc: '2026-10-15T18:00:00Z',
      toDueAtUtc: '2026-10-15T14:00:00Z',
      reason: 'Urgent production outage',
      escalatedBy: 'Support Lead',
      escalatedAtUtc: '2026-10-15T12:00:00Z',
    };

    mockEscalateTicket.mockResolvedValueOnce({
      ticket: updatedTicket,
      escalation,
    });

    render(<EscalationPanel ticket={baseTicket} onEscalated={onEscalated} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    fireEvent.change(textarea, { target: { value: 'Urgent production outage' } });

    const submitBtn = screen.getByRole('button', { name: 'Escalate ticket' });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(mockEscalateTicket).toHaveBeenCalledWith(42, {
        reason: 'Urgent production outage',
        escalatedBy: 'Support Lead',
      });
      expect(onEscalated).toHaveBeenCalledWith(updatedTicket, escalation);
    });
  });

  it('displays 409 conflict error message from server', async () => {
    mockEscalateTicket.mockRejectedValueOnce(
      new ApiError(409, 'Ticket is already at Critical priority and cannot be escalated.'),
    );

    render(<EscalationPanel ticket={baseTicket} onEscalated={vi.fn()} />);

    const textarea = screen.getByLabelText(/escalation reason/i);
    fireEvent.change(textarea, { target: { value: 'Customer demanding immediate resolution' } });

    const submitBtn = screen.getByRole('button', { name: 'Escalate ticket' });
    fireEvent.click(submitBtn);

    expect(
      await screen.findByText('Ticket is already at Critical priority and cannot be escalated.'),
    ).toBeInTheDocument();
  });
});
