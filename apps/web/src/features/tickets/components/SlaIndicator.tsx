import type { BadgeTone } from '../../../components/Badge';
import { Badge } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

interface SlaConfig {
  text: string;
  tone: BadgeTone;
  icon: string;
}

const slaConfigs: Record<SlaStatus, SlaConfig> = {
  WithinSla: {
    text: 'Within SLA',
    tone: 'info',
    icon: '●',
  },
  AtRisk: {
    text: 'At risk',
    tone: 'warning',
    icon: '▲',
  },
  Breached: {
    text: 'Breached',
    tone: 'danger',
    icon: '■',
  },
  Met: {
    text: 'Met',
    tone: 'success',
    icon: '✓',
  },
  NotApplicable: {
    text: 'No SLA',
    tone: 'neutral',
    icon: '—',
  },
};

interface SlaIndicatorProps {
  status: SlaStatus;
}

/**
 * Unified SLA indicator component.
 * Displays both descriptive text and a distinct icon so state is never conveyed by colour alone.
 */
export function SlaIndicator({ status }: SlaIndicatorProps) {
  const config = slaConfigs[status] ?? {
    text: status,
    tone: 'neutral' as BadgeTone,
    icon: '•',
  };

  return (
    <Badge tone={config.tone}>
      <span className="sla-indicator" data-status={status}>
        <span className="sla-indicator__icon" aria-hidden="true">
          {config.icon}
        </span>
        <span className="sla-indicator__text">{config.text}</span>
      </span>
    </Badge>
  );
}
