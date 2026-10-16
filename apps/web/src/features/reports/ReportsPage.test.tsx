import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { reportsApi } from '../../api/reports';
import type { SlaSummaryReport } from '../../types/api';
import { ReportsPage } from './ReportsPage';

vi.mock('../../api/reports');

const mockReportsApi = vi.mocked(reportsApi);

const sampleReport: SlaSummaryReport = {
  groupBy: 'agent',
  from: null,
  to: null,
  items: [
    {
      groupKey: '1',
      groupLabel: 'Alex Turner',
      agentId: 1,
      priority: null,
      totalTickets: 10,
      breachedCount: 2,
      atRiskCount: 1,
      withinCount: 5,
      metCount: 2,
      breachRate: 0.2,
      averageResolutionHours: 3.5,
    },
    {
      groupKey: 'unassigned',
      groupLabel: 'Unassigned',
      agentId: null,
      priority: null,
      totalTickets: 2,
      breachedCount: 1,
      atRiskCount: 0,
      withinCount: 1,
      metCount: 0,
      breachRate: 0.5,
      averageResolutionHours: null,
    },
  ],
};

describe('ReportsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders SLA metrics correctly when data is loaded', async () => {
    mockReportsApi.getSlaSummary.mockResolvedValue(sampleReport);

    render(<ReportsPage />);

    expect(screen.getByRole('status')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText('Alex Turner', { exact: false })).toBeInTheDocument();
    });

    // Check summary metric cards
    expect(screen.getByText('Total Tickets')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument(); // 10 + 2
    expect(screen.getByText('Total Breached')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument(); // 2 + 1
    expect(screen.getByText('25.0%')).toBeInTheDocument(); // 3 / 12 * 100

    // Check table rows
    expect(screen.getByText('Unassigned', { exact: false })).toBeInTheDocument();
    expect(screen.getByText('3.5 h')).toBeInTheDocument();
  });

  it('toggles groupBy mode from agent to priority', async () => {
    mockReportsApi.getSlaSummary.mockResolvedValue(sampleReport);

    render(<ReportsPage />);

    await waitFor(() => {
      expect(screen.getByText('Alex Turner', { exact: false })).toBeInTheDocument();
    });

    const priorityButton = screen.getByRole('button', { name: /priority/i });
    fireEvent.click(priorityButton);

    expect(mockReportsApi.getSlaSummary).toHaveBeenCalledWith(
      expect.objectContaining({ groupBy: 'priority' }),
    );
  });

  it('displays error message when report fails to load', async () => {
    mockReportsApi.getSlaSummary.mockRejectedValue(new Error('Network failure'));

    render(<ReportsPage />);

    await waitFor(() => {
      expect(screen.getByText(/Network failure/i)).toBeInTheDocument();
    });
  });
});
