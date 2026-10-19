import { authenticatedFetch, toApiError } from './http'

export type UnderwritingRuleResult = {
  id: number
  applicationId: number
  ruleId: number
  ruleCode: string
  ruleName: string
  action: 'reject' | 'manual_review' | 'block_issue' | string
  status: 'passed' | 'hit' | 'unavailable' | string
  message: string
  detail: string
  evaluatedAtUtc: string
}

export type UnderwritingEvaluation = {
  applicationId: number
  overallAction: 'approve' | 'reject' | 'manual_review' | 'block_issue' | string
  hasReject: boolean
  requiresManualReview: boolean
  blocksIssue: boolean
  results: UnderwritingRuleResult[]
}

export type UnderwritingQueueStats = {
  pendingReviewCount: number
  autoApprovedCount: number
  pendingDocumentsCount: number
  rejectedCount: number
}

export type UnderwritingQueueItem = {
  id: number
  applicationId: number
  applicationNumber: string
  insuredName: string
  licensePlate: string
  productSummary: string
  totalPremium: number
  riskLevel: 'low' | 'medium' | 'high' | string
  status: string
  overallAction: string
  submittedAtUtc: string | null
  updatedAtUtc: string
}

export type UnderwritingQueueList = {
  items: UnderwritingQueueItem[]
  stats: UnderwritingQueueStats
  page: number
  pageSize: number
  total: number
}

export type UnderwritingCaseDetail = {
  id: number
  applicationId: number
  applicationNumber: string
  insuredName: string
  licensePlate: string
  productSummary: string
  totalPremium: number
  riskLevel: string
  status: string
  overallAction: string
  opinion: string | null
  decision: string | null
  reviewedAtUtc: string | null
  submittedAtUtc: string | null
  updatedAtUtc: string
  evaluation: UnderwritingEvaluation
}

export type UnderwritingQueueSyncResult = {
  processedCount: number
  createdCount: number
  updatedCount: number
  stats: UnderwritingQueueStats
}

async function readError(response: Response): Promise<Error> {
  return toApiError(response)
}

async function readJson<T>(response: Response): Promise<T> {
  if (!response.ok) throw await readError(response)
  return response.json() as Promise<T>
}

export async function syncUnderwritingQueue(): Promise<UnderwritingQueueSyncResult> {
  return readJson(await authenticatedFetch('/api/underwriting-queue/sync', { method: 'POST' }))
}

export async function getUnderwritingQueue(
  keyword = '',
  status = '',
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
): Promise<UnderwritingQueueList> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (keyword.trim()) params.set('keyword', keyword.trim())
  if (status) params.set('status', status)
  return readJson(await authenticatedFetch(`/api/underwriting-queue?${params.toString()}`, { signal }))
}

export async function getUnderwritingCase(id: number, signal?: AbortSignal): Promise<UnderwritingCaseDetail> {
  return readJson(await authenticatedFetch(`/api/underwriting-queue/${id}`, { signal }))
}

async function postDecision(
  id: number,
  action: 'approve' | 'request-documents' | 'reject',
  opinion: string,
): Promise<UnderwritingCaseDetail> {
  return readJson(await authenticatedFetch(`/api/underwriting-queue/${id}/${action}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ opinion }),
  }))
}

export function approveUnderwritingCase(id: number, opinion: string): Promise<UnderwritingCaseDetail> {
  return postDecision(id, 'approve', opinion)
}

export function requestUnderwritingDocuments(id: number, opinion: string): Promise<UnderwritingCaseDetail> {
  return postDecision(id, 'request-documents', opinion)
}

export function rejectUnderwritingCase(id: number, opinion: string): Promise<UnderwritingCaseDetail> {
  return postDecision(id, 'reject', opinion)
}

export async function reevaluateUnderwritingCase(id: number): Promise<UnderwritingCaseDetail> {
  return readJson(await authenticatedFetch(`/api/underwriting-queue/${id}/reevaluate`, { method: 'POST' }))
}
