import { requestJson } from './http'

export type PermissionMatrixRole = {
  role: string
  permissions: string[]
}

export type PermissionMatrix = {
  roles: string[]
  permissions: string[]
  matrix: PermissionMatrixRole[]
}

export function getPermissionMatrix(signal?: AbortSignal): Promise<PermissionMatrix> {
  return requestJson<PermissionMatrix>('/api/permissions/matrix', { signal }, true)
}
