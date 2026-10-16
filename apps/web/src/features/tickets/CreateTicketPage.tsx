import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import { formatDateTime } from '../../lib/format';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <h2 style={{ marginTop: 0 }}>
            Raised <strong>{created.reference}</strong> for {created.customer.name}
          </h2>

          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              gap: '12px',
              margin: '16px 0',
              padding: '12px',
              borderRadius: '6px',
              background: 'var(--colour-page)',
              border: '1px solid var(--colour-border)',
            }}
          >
            <h3 style={{ margin: '0 0 4px', fontSize: '0.95rem' }}>Automated Triage Summary</h3>

            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
              <span style={{ fontWeight: 600, fontSize: '0.85rem' }}>Priority:</span>
              <PriorityBadge priority={created.priority} />
              {created.triage?.priorityReason && (
                <span className="summary__note">({created.triage.priorityReason})</span>
              )}
            </div>

            <div>
              <span style={{ fontWeight: 600, fontSize: '0.85rem' }}>Response Window: </span>
              <span>
                {created.triage ? `${created.triage.slaWindowHours}h` : 'Calculated'} (Due{' '}
                {formatDateTime(created.dueAtUtc)})
              </span>
              {(created.triage?.slaReason ?? created.triage?.slaCalculationReason) && (
                <p className="summary__note" style={{ margin: '2px 0 0' }}>
                  {created.triage?.slaReason ?? created.triage?.slaCalculationReason}
                </p>
              )}
            </div>

            <div>
              <span style={{ fontWeight: 600, fontSize: '0.85rem' }}>Assigned Agent: </span>
              <span>{created.assignedAgent?.fullName ?? 'Unassigned'}</span>
              {created.triage?.assignmentReason && (
                <p className="summary__note" style={{ margin: '2px 0 0' }}>
                  {created.triage.assignmentReason}
                </p>
              )}
            </div>
          </div>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
