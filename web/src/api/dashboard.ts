import { requestJson } from './http'

export type DashboardSummary = {
  customerTotal: number
  activePolicyTotal: number
  pendingUnderwritingTotal: number
  pendingClaimTotal: number
}

export function getDashboardSummary(signal?: AbortSignal): Promise<DashboardSummary> {
  return requestJson<DashboardSummary>('/api/dashboard/summary', { signal }, true)
}
