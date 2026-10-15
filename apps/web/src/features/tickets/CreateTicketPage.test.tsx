import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as refDataModule from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketPage } from './CreateTicketPage';

vi.mock('../../hooks/useReferenceData');

const mockUseReferenceData = vi.mocked(refDataModule.useReferenceData);

const sampleTicket: TicketDetail = {
  id: 50,
  reference: 'TCK-0050',
  title: 'Network outage in warehouse',
  description: 'Switches down',
  status: 'New',
  priority: 'Critical',
  customer: { id: 1, name: 'Acme Corp', email: 'acme@example.com', phone: null, tier: 'Premium' },
  category: { id: 3, name: 'Outage', requiresSpecialist: false, forcesCriticalPriority: true },
  assignedAgent: { id: 5, fullName: 'David Lee', email: 'david@example.com' },
  createdAtUtc: '2026-10-15T12:00:00Z',
  updatedAtUtc: '2026-10-15T12:00:00Z',
  dueAtUtc: '2026-10-15T14:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
  triage: {
    priority: 'Critical',
    priorityReason: 'Category Outage forces Critical priority',
    slaWindowHours: 2,
    slaCalculationReason: 'Premium customer halved base 4h to 2h',
    assignedAgentId: 5,
    assignmentReason: 'Fewest open tickets among eligible agents',
  },
};

vi.mock('./components/CreateTicketForm', () => ({
  CreateTicketForm: ({ onCreated }: { onCreated: (t: TicketDetail) => void }) => (
    <button type="button" onClick={() => onCreated(sampleTicket)}>
      Simulate Submit
    </button>
  ),
}));

describe('CreateTicketPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseReferenceData.mockReturnValue({
      data: {
        customers: [{ id: 1, name: 'Acme Corp', tier: 'Premium' }],
        categories: [{ id: 3, name: 'Outage', requiresSpecialist: false, forcesCriticalPriority: true }],
      },
      isLoading: false,
      error: undefined,
      reload: vi.fn(),
    });
  });

  it('renders the form initially', () => {
    render(
      <MemoryRouter>
        <CreateTicketPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole('heading', { name: 'Raise a ticket' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Simulate Submit' })).toBeInTheDocument();
  });

  it('renders the automated triage breakdown card after creation', () => {
    render(
      <MemoryRouter>
        <CreateTicketPage />
      </MemoryRouter>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Simulate Submit' }));

    expect(screen.getByText(/Automated Triage Summary/i)).toBeInTheDocument();
    expect(screen.getByText(/Category Outage forces Critical priority/i)).toBeInTheDocument();
    expect(screen.getByText(/2h/i)).toBeInTheDocument();
    expect(screen.getByText(/Premium customer halved base 4h to 2h/i)).toBeInTheDocument();
    expect(screen.getByText('David Lee')).toBeInTheDocument();
    expect(screen.getByText(/Fewest open tickets among eligible agents/i)).toBeInTheDocument();
  });
});
