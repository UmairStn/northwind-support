import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator } from './SlaIndicator';

describe('SlaIndicator', () => {
  const statuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];

  it.each(statuses)('renders distinct label and glyph for %s', (status) => {
    const { container } = render(<SlaIndicator status={status} />);
    const indicator = container.querySelector('.sla-indicator');
    expect(indicator).toBeInTheDocument();
    expect(indicator).toHaveAttribute('data-status', status);

    const icon = container.querySelector('.sla-indicator__icon');
    expect(icon).toBeInTheDocument();
    expect(icon?.textContent?.trim()).not.toBe('');

    const text = container.querySelector('.sla-indicator__text');
    expect(text).toBeInTheDocument();
    expect(text?.textContent?.trim()).not.toBe('');
  });

  it('renders distinct icons for all statuses so state is not conveyed by color alone', () => {
    const icons = new Set<string>();
    for (const status of statuses) {
      const { container } = render(<SlaIndicator status={status} />);
      const icon = container.querySelector('.sla-indicator__icon')?.textContent?.trim();
      expect(icon).toBeDefined();
      expect(icons.has(icon!)).toBe(false);
      icons.add(icon!);
    }
    expect(icons.size).toBe(5);
  });
});
