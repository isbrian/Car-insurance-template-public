import { authenticatedFetch } from './http'

export type Customer = {
  id: number
  name: string
  nationalId: string
  phone: string
  email: string | null
  birthDate: string | null
  address: string | null
  isVerified: boolean
  claimRecordLevel: number
  createdAtUtc: string
}

export type VehicleSummary = {
  id: number
  licensePlate: string
  vin: string
  makeModelCode: string
  manufactureYear: number
  usageType: string
}

export type CustomerDetails = Customer & {
  vehicles: VehicleSummary[]
}

export type CustomerListResponse = {
  items: Customer[]
  total: number
  page: number
  pageSize: number
}

export type CustomerCreateRequest = {
  name: string
  nationalId: string
  phone: string
  email?: string
  birthDate?: string
  address?: string
}

export type ProblemDetails = {
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

  const message = problem?.detail || problem?.title || `請求失敗（${response.status}）`
  return new Error(message)
}

export async function getCustomers(
  keyword: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<CustomerListResponse> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (keyword.trim()) params.set('keyword', keyword.trim())

  const response = await authenticatedFetch(`/api/customers?${params.toString()}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as CustomerListResponse
}

export async function getCustomer(id: number, signal?: AbortSignal): Promise<CustomerDetails | null> {
  const response = await authenticatedFetch(`/api/customers/${id}`, { signal })
  if (response.status === 404) return null
  if (!response.ok) throw await readError(response)
  return (await response.json()) as CustomerDetails
}

export async function createCustomer(request: CustomerCreateRequest): Promise<Customer> {
  const response = await authenticatedFetch('/api/customers', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Customer
}
