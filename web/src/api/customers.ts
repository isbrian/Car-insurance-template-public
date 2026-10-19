import { authenticatedFetch, toApiError } from './http'

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

async function readError(response: Response): Promise<Error> {
  return toApiError(response)
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
