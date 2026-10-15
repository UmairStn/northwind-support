import { formatDateTime, formatRelativeToNow } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

interface EscalationHistoryProps {
  escalations: TicketEscalation[];
  isLoading?: boolean;
}

/**
 * Renders the immutable escalation history for a ticket, ordered newest first.
 */
export function EscalationHistory({ escalations, isLoading }: EscalationHistoryProps) {
  if (isLoading && escalations.length === 0) {
    return (
      <div className="card">
        <h2>Escalation history</h2>
        <p className="summary__note">Loading history...</p>
      </div>
    );
  }

  if (escalations.length === 0) {
    return (
      <div className="card">
        <h2>Escalation history</h2>
        <p className="summary__note">No escalations recorded for this ticket.</p>
      </div>
    );
  }

  return (
    <div className="card escalation-history">
      <h2>Escalation history ({escalations.length})</h2>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', marginTop: '12px' }}>
        {escalations.map((item) => (
          <article
            key={item.id}
            style={{
              padding: '12px',
              border: '1px solid var(--colour-border)',
              borderRadius: '6px',
              background: 'var(--colour-page)',
            }}
          >
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: '8px',
                marginBottom: '8px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Priority:</span>
                <PriorityBadge priority={item.fromPriority} />
                <span>→</span>
                <PriorityBadge priority={item.toPriority} />
              </div>

              <span style={{ fontSize: '0.8rem', color: 'var(--colour-muted)' }}>
                {formatRelativeToNow(item.escalatedAtUtc)} ({formatDateTime(item.escalatedAtUtc)})
              </span>
            </div>

            <p style={{ margin: '8px 0', fontSize: '0.9rem', fontStyle: 'italic' }}>
              &ldquo;{item.reason}&rdquo;
            </p>

            <dl
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
                gap: '8px',
                margin: '8px 0 0',
                fontSize: '0.8rem',
                color: 'var(--colour-muted)',
              }}
            >
              <div>
                <dt style={{ fontWeight: 600 }}>Escalated by</dt>
                <dd style={{ margin: 0 }}>{item.escalatedBy}</dd>
              </div>

              <div>
                <dt style={{ fontWeight: 600 }}>Assignee</dt>
                <dd style={{ margin: 0 }}>
                  {item.fromAgentName ?? 'Unassigned'} → {item.toAgentName ?? 'Unassigned'}
                </dd>
              </div>

              <div>
                <dt style={{ fontWeight: 600 }}>Due date</dt>
                <dd style={{ margin: 0 }}>
                  {formatDateTime(item.fromDueAtUtc)} → {formatDateTime(item.toDueAtUtc)}
                </dd>
              </div>
            </dl>
          </article>
        ))}
      </div>
    </div>
  );
}
