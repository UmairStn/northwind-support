import { useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (updatedTicket: TicketDetail, newEscalation: TicketEscalation) => void;
}

/**
 * Panel on the ticket detail page for escalating a ticket with mandatory reason and audit recording.
 */
export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [clientError, setClientError] = useState<string>();
  const [serverError, setServerError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isResolvedOrClosed = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const isEscalationDisabled = isCritical || isResolvedOrClosed;

  let disabledReasonMessage: string | null = null;
  if (isCritical) {
    disabledReasonMessage = 'This ticket is already at Critical priority and cannot be escalated further.';
  } else if (isResolvedOrClosed) {
    disabledReasonMessage = `This ticket is ${ticket.status.toLowerCase()} and cannot be escalated.`;
  }

  const validate = (val: string): string | undefined => {
    const trimmed = val.trim();
    if (trimmed.length < 5) {
      return 'Reason must be at least 5 characters.';
    }
    if (trimmed.length > 500) {
      return 'Reason cannot exceed 500 characters.';
    }
    return undefined;
  };

  const handleReasonChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const next = e.target.value;
    setReason(next);
    if (clientError) {
      setClientError(validate(next));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isEscalationDisabled) {
      return;
    }

    const err = validate(reason);
    if (err) {
      setClientError(err);
      return;
    }

    setClientError(undefined);
    setServerError(undefined);
    setIsSubmitting(true);

    try {
      const result = await ticketsApi.escalateTicket(ticket.id, {
        reason: reason.trim(),
        escalatedBy: 'Support Lead',
      });

      setReason('');
      onEscalated(result.ticket, result.escalation);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setServerError(caught.message);
      } else {
        setServerError(toErrorMessage(caught, 'Failed to escalate ticket.'));
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card escalation-panel">
      <h2>Escalate ticket</h2>

      {disabledReasonMessage ? (
        <p className="summary__note" role="status">
          {disabledReasonMessage}
        </p>
      ) : (
        <form onSubmit={handleSubmit} noValidate>
          <Field
            id="escalate-reason"
            label="Escalation reason"
            error={clientError}
            hint="5 to 500 characters explaining why priority and response window need urgent escalation."
          >
            {(fieldProps) => (
              <textarea
                {...fieldProps}
                rows={3}
                value={reason}
                maxLength={500}
                disabled={isSubmitting}
                onChange={handleReasonChange}
                placeholder="Describe why this ticket requires immediate escalation..."
              />
            )}
          </Field>

          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              marginTop: '4px',
              fontSize: '0.8rem',
              color: 'var(--colour-muted)',
            }}
          >
            <span>{reason.trim().length} / 500 characters</span>
          </div>

          {serverError && (
            <p className="field__error" role="alert" style={{ marginTop: '8px' }}>
              {serverError}
            </p>
          )}

          <div className="button-row" style={{ marginTop: '12px' }}>
            <button
              type="submit"
              className="button button--primary"
              disabled={isSubmitting || isEscalationDisabled}
            >
              {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
