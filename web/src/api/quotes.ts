import { authenticatedFetch } from './http'

export type RateVersion = {
  id: number
  insuranceProductId: number
  insuranceProductCode: string
  insuranceProductName: string
  version: string
  effectiveFrom: string
  effectiveTo: string | null
  confidence: string
  isActive: boolean
  createdAtUtc: string
}

export type PremiumCalculationRequest = {
  rateVersionId: number
  driverBirthDate: string
  driverGender: 'male' | 'female'
  claimRecordLevel: number
  claimCountLast3Years: number
  vehicleCategory: string
  makeModelCode: string
  manufactureDate: string
  usageType: string
  thirdPartyBodilyInjuryPerPerson: number | null
  thirdPartyPropertyDamagePerAccident: number | null
  excessLiabilityCoverage: number | null
  ownDamageType: string | null
  deductible: number | null
  theft: boolean
  driverAccidentCoverage: number | null
  riders: string[]
  isRenewal: boolean
  isMultiVehicle: boolean
  isOnline: boolean
}

export type PremiumBreakdownItem = {
  section: 'compulsory' | 'voluntary' | string
  code: string
  label: string
  operation: string
  value: number
  amount: number
  note: string | null
}

export type PremiumCalculationResponse = {
  rateVersionId: number
  sourceVersion: string
  isProvisional: boolean
  displayWarning: string
  compulsoryPremium: number
  voluntaryPremium: number
  totalPremium: number
  breakdown: PremiumBreakdownItem[]
}

export type QuoteCreateRequest = {
  customerId: number
  vehicleId: number
  insuranceStartDate: string
  salesAgent: string
  channel: string
  status: 'draft' | 'active'
  calculation: PremiumCalculationRequest
}

export type Quote = {
  id: number
  quoteNumber: string
  customerId: number
  customerName: string
  vehicleId: number
  licensePlate: string
  rateVersionId: number
  status: 'draft' | 'active' | 'expired' | string
  insuranceStartDate: string
  salesAgent: string
  channel: string
  compulsoryPremium: number
  voluntaryPremium: number
  totalPremium: number
  isProvisional: boolean
  displayWarning: string
  createdAtUtc: string
  updatedAtUtc: string
  validUntilUtc: string
  calculation: PremiumCalculationRequest
  breakdown: PremiumBreakdownItem[]
}

type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

async function readError(response: Response): Promise<Error> {
  let problem: ProblemDetails | undefined
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    // 非 JSON 回應使用 HTTP 狀態文字。
  }

  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().filter(Boolean).join('；')
    : undefined
  const message = problem?.detail || validationMessage || problem?.title || `請求失敗（${response.status}）`
  return new Error(message)
}

export async function getQuotes(signal?: AbortSignal): Promise<Quote[]> {
  const response = await authenticatedFetch('/api/quotes', { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Quote[]
}

export async function getRateVersions(signal?: AbortSignal): Promise<RateVersion[]> {
  const response = await authenticatedFetch('/api/rate-versions', { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as RateVersion[]
}

export async function calculatePremium(
  request: PremiumCalculationRequest,
  signal?: AbortSignal,
): Promise<PremiumCalculationResponse> {
  const response = await authenticatedFetch('/api/premium-calculations', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    signal,
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as PremiumCalculationResponse
}

export async function createQuote(request: QuoteCreateRequest): Promise<Quote> {
  const response = await authenticatedFetch('/api/quotes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Quote
}

export async function updateQuote(id: number, request: QuoteCreateRequest): Promise<Quote> {
  const response = await authenticatedFetch(`/api/quotes/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Quote
}
