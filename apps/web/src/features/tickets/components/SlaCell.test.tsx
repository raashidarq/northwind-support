import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

describe('SlaCell', () => {
  it.each<[SlaStatus, string, string]>([
    ['WithinSla', 'Within SLA', 'badge--info'],
    ['AtRisk', 'At risk', 'badge--warning'],
    ['Breached', 'Breached', 'badge--danger'],
    ['Met', 'Met', 'badge--success'],
    ['NotApplicable', 'No SLA', 'badge--neutral'],
  ])('shows %s as its own words and colour', (status, text, toneClass) => {
    render(<SlaCell dueAtUtc={null} slaStatus={status} />);

    expect(screen.getByText(text)).toHaveClass(toneClass);
  });
});
