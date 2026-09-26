import { authenticatedFetch, toApiError } from './http'

export type Payment = {
  id: number
  paymentNumber: string
  applicationId: number
  applicationNumber: string
  quoteId: number
  quoteNumber: string
  customerId: number
  customerName: string
  vehicleId: number
  licensePlate: string
  status: string
  paymentFrequency: string
  paymentMethod: string
  amountDue: number
  installmentCount: number
  dueAtUtc: string
  paidAtUtc: string | null
  cancelledAtUtc: string | null
  referenceNumber: string | null
  note: string | null
  createdAtUtc: string
  updatedAtUtc: string
  policyLifecycleStatus: string | null
}

export type PaymentCreateRequest = {
  applicationId: number
  paymentFrequency: 'annual' | 'semiannual' | 'quarterly'
  paymentMethod: 'credit_card' | 'atm' | 'convenience_store' | 'cash_counter'
}

export type PaymentUpdateRequest = {
  paymentFrequency: 'annual' | 'semiannual' | 'quarterly'
  paymentMethod: 'credit_card' | 'atm' | 'convenience_store' | 'cash_counter'
  note?: string | null
}

export type PaymentConfirmRequest = {
  referenceNumber?: string | null
  note?: string | null
}

export type PolicySummary = {
  id: number
  policyNumber: string
  applicationId: number
  applicationNumber: string
  paymentId: number
  paymentNumber: string
  status: string
  totalPremium: number
  issuedAtUtc: string
}

async function readError(response: Response) {
  return toApiError(response)
}

export async function getPayments(signal?: AbortSignal): Promise<Payment[]> {
  const response = await authenticatedFetch('/api/payments', { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Payment[]
}

export async function createPayment(request: PaymentCreateRequest): Promise<Payment> {
  const response = await authenticatedFetch('/api/payments', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Payment
}

export async function updatePayment(id: number, request: PaymentUpdateRequest): Promise<Payment> {
  const response = await authenticatedFetch(`/api/payments/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Payment
}

export async function confirmPayment(id: number, request: PaymentConfirmRequest): Promise<Payment> {
  const response = await authenticatedFetch(`/api/payments/${id}/confirm`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Payment
}

export async function getPolicyByApplication(applicationId: number): Promise<PolicySummary | null> {
  const response = await authenticatedFetch(`/api/policies/by-application/${applicationId}`)
  if (response.status === 404) return null
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PolicySummary
}

export async function issuePolicy(applicationId: number): Promise<PolicySummary> {
  const existing = await getPolicyByApplication(applicationId)
  if (existing) return existing

  const response = await authenticatedFetch(`/api/policies/issue/${applicationId}`, { method: 'POST' })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PolicySummary
}
