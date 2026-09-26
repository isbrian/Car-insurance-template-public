import { authenticatedFetch, toApiError } from './http'

export type PolicyListItem = {
  id: number
  policyNumber: string
  customerName: string
  insuredName: string | null
  licensePlate: string
  status: string
  insuranceStartDate: string
  insuranceEndDate: string
  totalPremium: number
  issuedAtUtc: string
}

export type PolicyListResponse = {
  items: PolicyListItem[]
  total: number
  page: number
  pageSize: number
}

export type PolicyDetails = {
  id: number
  policyNumber: string
  applicationId: number
  applicationNumber: string
  paymentId: number
  paymentNumber: string
  quoteId: number
  quoteNumber: string
  customerId: number
  customerName: string
  customerNationalId: string
  customerPhone: string
  customerEmail: string | null
  customerAddress: string | null
  vehicleId: number
  licensePlate: string
  vin: string
  makeModelCode: string
  usageType: string
  insuredName: string | null
  insuredNationalId: string | null
  status: string
  insuranceStartDate: string
  insuranceEndDate: string
  totalPremium: number
  paymentFrequency: string
  paymentMethod: string
  salesAgent: string
  channel: string
  rateVersion: string
  templateVersion: string
  pdfSizeBytes: number
  pdfSha256: string
  issuedAtUtc: string
  createdAtUtc: string
  updatedAtUtc: string
}

export type PolicyTimelineEvent = {
  key: string
  title: string
  status: string
  description: string | null
  occurredAtUtc: string
}

export type PolicyTimeline = {
  policyId: number
  policyNumber: string
  currentLifecycleStatus: string | null
  events: PolicyTimelineEvent[]
}

async function readError(response: Response) {
  return toApiError(response)
}

export async function getPolicies(
  keyword: string,
  status: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<PolicyListResponse> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (keyword.trim()) params.set('keyword', keyword.trim())
  if (status.trim()) params.set('status', status.trim())

  const response = await authenticatedFetch(`/api/policies?${params.toString()}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PolicyListResponse
}

export async function getPolicy(id: number, signal?: AbortSignal): Promise<PolicyDetails> {
  const response = await authenticatedFetch(`/api/policies/${id}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PolicyDetails
}

export async function getPolicyTimeline(id: number, signal?: AbortSignal): Promise<PolicyTimeline> {
  const response = await authenticatedFetch(`/api/policies/${id}/timeline`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PolicyTimeline
}

export async function downloadPolicyDocument(id: number): Promise<{ blob: Blob; fileName: string }> {
  const response = await authenticatedFetch(`/api/policies/${id}/document`)
  if (!response.ok) throw await readError(response)

  const contentDisposition = response.headers.get('content-disposition') || ''
  const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)
  const plainMatch = contentDisposition.match(/filename="?([^";]+)"?/i)
  const encodedName = utf8Match?.[1] || plainMatch?.[1]
  const fileName = encodedName ? decodeURIComponent(encodedName) : `policy-${id}.pdf`
  return { blob: await response.blob(), fileName }
}
