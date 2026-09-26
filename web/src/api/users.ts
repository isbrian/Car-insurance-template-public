import { requestJson } from './http'

export const USER_ROLES = ['業務員', '核保員', '理賠員', '主管', '系統管理員'] as const

export type UserRole = (typeof USER_ROLES)[number]

export type UserAccount = {
  id: number
  username: string
  email: string
  displayName: string
  phone: string
  role: UserRole
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export function getUsers(signal?: AbortSignal): Promise<UserAccount[]> {
  return requestJson<UserAccount[]>('/api/users', { signal }, true)
}

export function updateUserActive(id: number, isActive: boolean): Promise<UserAccount> {
  return requestJson<UserAccount>(`/api/users/${id}/active`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ isActive }),
  }, true)
}

export function updateUserRole(id: number, role: UserRole): Promise<UserAccount> {
  return requestJson<UserAccount>(`/api/users/${id}/role`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ role }),
  }, true)
}
