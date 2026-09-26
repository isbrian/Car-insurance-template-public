import { requestJson } from './http'

export type AuditLog = {
  id: number
  occurredAtUtc: string
  actor: string
  role: string
  action: string
  objectType: string
  objectId: string
  beforeJson: string | null
  afterJson: string | null
  sourceIp: string
}

export type AuditLogListResponse = {
  items: AuditLog[]
  page: number
  pageSize: number
  total: number
}

export type AuditLogQuery = {
  fromUtc?: string
  toUtc?: string
  actor?: string
  role?: string
  action?: string
  objectType?: string
  objectId?: string
  page: number
  pageSize: number
}

export function getAuditLogs(query: AuditLogQuery, signal?: AbortSignal): Promise<AuditLogListResponse> {
  const params = new URLSearchParams()
  const append = (key: string, value?: string) => {
    const normalized = value?.trim()
    if (normalized) params.set(key, normalized)
  }

  append('fromUtc', query.fromUtc)
  append('toUtc', query.toUtc)
  append('actor', query.actor)
  append('role', query.role)
  append('action', query.action)
  append('objectType', query.objectType)
  append('objectId', query.objectId)
  params.set('page', String(query.page))
  params.set('pageSize', String(query.pageSize))

  return requestJson<AuditLogListResponse>(`/api/audit-logs?${params.toString()}`, { signal }, true)
}
