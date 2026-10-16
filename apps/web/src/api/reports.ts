import type { SlaSummaryReport, SlaSummaryReportQuery } from '../types/api';
import { request } from './client';

export const reportsApi = {
  getSlaSummary(query: SlaSummaryReportQuery): Promise<SlaSummaryReport> {
    const params = new URLSearchParams({ groupBy: query.groupBy });
    if (query.from) {
      params.set('from', query.from);
    }
    if (query.to) {
      params.set('to', query.to);
    }
    return request<SlaSummaryReport>(`/reports/sla-summary?${params.toString()}`);
  },
};
