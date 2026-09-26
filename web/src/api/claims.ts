import { authenticatedFetch, toApiError } from './http'

export type ClaimStatus =
  | 'pending_intake'
  | 'reviewing'
  | 'pending_documents'
  | 'calculating'
  | 'pending_payment'
  | 'closed'
  | 'denied'

export type Claim = {
  id: number
  claimNumber: string
  policyId: number
  policyNumber: string
  claimantName: string
  licensePlate: string
  status: ClaimStatus
  accidentDate: string
  accidentLocation: string
  accidentDescription: string
  claimedAmount: number | null
  liabilityDecision: string | null
  liabilityPercentage: number | null
  approvedAmount: number | null
  paymentMethod: string | null
  paidAtUtc: string | null
  deniedReason: string | null
  assignedAdjuster: string | null
  lastTransition: string
  lastTransitionReason: string | null
  statusChangedAtUtc: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type ClaimTransitionRequest = {
  targetStatus: ClaimStatus
  assignedAdjuster?: string
  reason?: string
}

export type ClaimLiabilityRequest = {
  decision: string
  liabilityPercentage: number
  reason?: string
}

export type ClaimApproveRequest = {
  approvedAmount: number
  reason?: string
}

export type ClaimPayRequest = {
  paymentMethod: string
  reason?: string
}

export type ClaimDenyRequest = {
  reason: string
}

async function readError(response: Response) {
  return toApiError(response)
}

export async function getClaims(status?: string, signal?: AbortSignal): Promise<Claim[]> {
  const params = new URLSearchParams()
  if (status?.trim()) params.set('status', status.trim())
  const suffix = params.size > 0 ? `?${params.toString()}` : ''
  const response = await authenticatedFetch(`/api/claims${suffix}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Claim[]
}

export async function getClaim(id: number, signal?: AbortSignal): Promise<Claim> {
  const response = await authenticatedFetch(`/api/claims/${id}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Claim
}

export async function transitionClaim(id: number, request: ClaimTransitionRequest): Promise<Claim> {
  const response = await authenticatedFetch(`/api/claims/${id}/transition`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Claim
}

async function postClaimAction<TRequest>(id: number, action: string, request: TRequest): Promise<Claim> {
  const response = await authenticatedFetch(`/api/claims/${id}/${action}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Claim
}

export function completeClaimLiability(id: number, request: ClaimLiabilityRequest): Promise<Claim> {
  return postClaimAction(id, 'liability', request)
}

export function approveClaimAmount(id: number, request: ClaimApproveRequest): Promise<Claim> {
  return postClaimAction(id, 'approve', request)
}

export function completeClaimPayment(id: number, request: ClaimPayRequest): Promise<Claim> {
  return postClaimAction(id, 'pay', request)
}

export function denyClaim(id: number, request: ClaimDenyRequest): Promise<Claim> {
  return postClaimAction(id, 'deny', request)
}
