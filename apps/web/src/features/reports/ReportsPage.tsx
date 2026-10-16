import { useCallback, useState } from 'react';
import { reportsApi } from '../../api/reports';
import { Badge } from '../../components/Badge';
import { EmptyState } from '../../components/EmptyState';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useAsyncData } from '../../hooks/useAsyncData';

export function ReportsPage() {
  const [groupBy, setGroupBy] = useState<'agent' | 'priority'>('agent');
  const [fromDate, setFromDate] = useState<string>('');
  const [toDate, setToDate] = useState<string>('');

  const loadReport = useCallback(() => {
    return reportsApi.getSlaSummary({
      groupBy,
      from: fromDate ? new Date(fromDate).toISOString() : undefined,
      to: toDate ? new Date(toDate).toISOString() : undefined,
    });
  }, [groupBy, fromDate, toDate]);

  const { data: report, isLoading, error, reload } = useAsyncData(
    loadReport,
    'Unable to load SLA summary report.',
  );

  const totalTicketsAll = report?.items.reduce((sum, item) => sum + item.totalTickets, 0) ?? 0;
  const totalBreachedAll = report?.items.reduce((sum, item) => sum + item.breachedCount, 0) ?? 0;
  const totalMetAll = report?.items.reduce((sum, item) => sum + item.metCount, 0) ?? 0;
  const overallBreachRate = totalTicketsAll > 0 ? (totalBreachedAll / totalTicketsAll) * 100 : 0;

  return (
    <section>
      <header className="page-header">
        <div>
          <h1>SLA Summary Report</h1>
          <p className="page-header__subtitle">
            Database-side aggregated SLA performance and resolution metrics.
          </p>
        </div>
      </header>

      <div className="filter-bar" style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <div>
          <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.25rem' }}>
            Group By
          </label>
          <div style={{ display: 'inline-flex', borderRadius: '6px', overflow: 'hidden', border: '1px solid var(--color-border, #cbd5e1)' }}>
            <button
              type="button"
              className={`button button--sm ${groupBy === 'agent' ? 'button--primary' : 'button--secondary'}`}
              onClick={() => setGroupBy('agent')}
              style={{ borderRadius: 0 }}
            >
              👤 Agent
            </button>
            <button
              type="button"
              className={`button button--sm ${groupBy === 'priority' ? 'button--primary' : 'button--secondary'}`}
              onClick={() => setGroupBy('priority')}
              style={{ borderRadius: 0 }}
            >
              ⚡ Priority
            </button>
          </div>
        </div>

        <div>
          <label htmlFor="report-from" style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.25rem' }}>
            From
          </label>
          <input
            id="report-from"
            type="date"
            className="input"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>

        <div>
          <label htmlFor="report-to" style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, marginBottom: '0.25rem' }}>
            To
          </label>
          <input
            id="report-to"
            type="date"
            className="input"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>

        {(fromDate || toDate) && (
          <button
            type="button"
            className="button button--secondary button--sm"
            onClick={() => {
              setFromDate('');
              setToDate('');
            }}
          >
            Clear Dates
          </button>
        )}
      </div>

      {error && <ErrorMessage message={error} onRetry={() => void reload()} />}

      {isLoading && !report && <Spinner label="Loading SLA summary report" />}

      {report && (
        <>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
            <div className="card" style={{ padding: '1rem', border: '1px solid var(--color-border, #e2e8f0)', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>Total Tickets</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>{totalTicketsAll}</div>
            </div>
            <div className="card" style={{ padding: '1rem', border: '1px solid var(--color-border, #e2e8f0)', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>Total Breached</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#dc2626' }}>{totalBreachedAll}</div>
            </div>
            <div className="card" style={{ padding: '1rem', border: '1px solid var(--color-border, #e2e8f0)', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>Total Met</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#16a34a' }}>{totalMetAll}</div>
            </div>
            <div className="card" style={{ padding: '1rem', border: '1px solid var(--color-border, #e2e8f0)', borderRadius: '8px' }}>
              <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>Overall Breach Rate</div>
              <div style={{ fontSize: '1.5rem', fontWeight: 700 }}>{overallBreachRate.toFixed(1)}%</div>
            </div>
          </div>

          {report.items.length === 0 ? (
            <EmptyState
              title="No data found"
              description="No tickets match the selected date range."
            />
          ) : (
            <div className="table-wrapper" aria-busy={isLoading}>
              <table className="table">
                <thead>
                  <tr>
                    <th>{groupBy === 'agent' ? 'Agent' : 'Priority'}</th>
                    <th style={{ textAlign: 'right' }}>Total</th>
                    <th style={{ textAlign: 'right' }}>Breached</th>
                    <th style={{ textAlign: 'right' }}>At Risk</th>
                    <th style={{ textAlign: 'right' }}>Within SLA</th>
                    <th style={{ textAlign: 'right' }}>Met</th>
                    <th style={{ textAlign: 'right' }}>Breach Rate</th>
                    <th style={{ textAlign: 'right' }}>Avg Resolution</th>
                  </tr>
                </thead>
                <tbody>
                  {report.items.map((row) => (
                    <tr key={row.groupKey}>
                      <td style={{ fontWeight: 600 }}>
                        {groupBy === 'priority' ? (
                          <Badge tone={row.groupLabel === 'Critical' ? 'danger' : row.groupLabel === 'High' ? 'warning' : 'neutral'}>
                            {row.groupLabel}
                          </Badge>
                        ) : (
                          <span>{row.groupLabel === 'Unassigned' ? '⚠️ Unassigned' : `👤 ${row.groupLabel}`}</span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 600 }}>{row.totalTickets}</td>
                      <td style={{ textAlign: 'right' }}>
                        {row.breachedCount > 0 ? (
                          <Badge tone="danger">{row.breachedCount}</Badge>
                        ) : (
                          <span>0</span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        {row.atRiskCount > 0 ? (
                          <Badge tone="warning">{row.atRiskCount}</Badge>
                        ) : (
                          <span>0</span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        {row.withinCount > 0 ? (
                          <Badge tone="info">{row.withinCount}</Badge>
                        ) : (
                          <span>0</span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        {row.metCount > 0 ? (
                          <Badge tone="success">{row.metCount}</Badge>
                        ) : (
                          <span>0</span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 600 }}>
                        {(row.breachRate * 100).toFixed(1)}%
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        {row.averageResolutionHours !== null
                          ? `${row.averageResolutionHours.toFixed(1)} h`
                          : '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </section>
  );
}
