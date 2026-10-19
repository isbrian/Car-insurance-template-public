import { authenticatedFetch, toApiError } from './http'

export type Vehicle = {
  id: number
  licensePlate: string
  vin: string
  makeModelCode: string
  manufactureDate: string | null
  usageType: string
  customerId: number
  customerName: string
}

export type VehicleListResponse = {
  items: Vehicle[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type VehicleCreateRequest = {
  licensePlate: string
  vin: string
  makeModelCode: string
  manufactureDate?: string
  usageType: string
  customerId: number
}

async function readError(response: Response): Promise<Error> {
  return toApiError(response)
}

export async function getVehicles(
  keyword: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
): Promise<VehicleListResponse> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (keyword.trim()) params.set('keyword', keyword.trim())

  const response = await authenticatedFetch(`/api/vehicles?${params.toString()}`, { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as VehicleListResponse
}

export async function getVehiclesByCustomer(
  customerId: number,
  signal?: AbortSignal,
): Promise<Vehicle[]> {
  const params = new URLSearchParams({ customerId: String(customerId), page: '1', pageSize: '100' })
  const response = await authenticatedFetch(`/api/vehicles?${params.toString()}`, { signal })
  if (!response.ok) throw await readError(response)
  const result = (await response.json()) as VehicleListResponse
  return result.items
}

export async function getVehicle(id: number, signal?: AbortSignal): Promise<Vehicle | null> {
  const response = await authenticatedFetch(`/api/vehicles/${id}`, { signal })
  if (response.status === 404) return null
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Vehicle
}

export async function createVehicle(request: VehicleCreateRequest): Promise<Vehicle> {
  const response = await authenticatedFetch('/api/vehicles', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      ...request,
      manufactureDate: request.manufactureDate || null,
    }),
  })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as Vehicle
}
