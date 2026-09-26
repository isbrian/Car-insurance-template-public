import { authenticatedFetch, toApiError } from './http'

export type NotificationItem = {
  id: number
  eventType: string
  channel: 'in_app' | 'sms' | 'email'
  recipient: string
  title: string
  message: string
  status: 'pending' | 'sent' | 'failed'
  attemptCount: number
  lastError: string | null
  referenceType: string | null
  referenceId: string | null
  createdAtUtc: string
  lastAttemptAtUtc: string | null
  sentAtUtc: string | null
  readAtUtc: string | null
}

export type NotificationListResponse = {
  items: NotificationItem[]
  page: number
  pageSize: number
  total: number
}

export type NotificationQuery = {
  keyword?: string
  channel?: string
  status?: string
  eventType?: string
  page?: number
  pageSize?: number
}

export type NotificationRetryRequest = {
  recipient?: string
}


export async function getNotifications(
  query: NotificationQuery = {},
  signal?: AbortSignal,
): Promise<NotificationListResponse> {
  const params = new URLSearchParams()
  if (query.keyword) params.set('keyword', query.keyword)
  if (query.channel) params.set('channel', query.channel)
  if (query.status) params.set('status', query.status)
  if (query.eventType) params.set('eventType', query.eventType)
  if (query.page) params.set('page', String(query.page))
  if (query.pageSize) params.set('pageSize', String(query.pageSize))

  const suffix = params.size ? `?${params.toString()}` : ''
  const response = await authenticatedFetch(`/api/notifications${suffix}`, { signal })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as NotificationListResponse
}

export async function retryNotification(
  id: number,
  request: NotificationRetryRequest,
): Promise<NotificationItem> {
  const response = await authenticatedFetch(`/api/notifications/${id}/retry`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as NotificationItem
}

export async function markNotificationRead(id: number): Promise<NotificationItem> {
  const response = await authenticatedFetch(`/api/notifications/${id}/read`, {
    method: 'POST',
  })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as NotificationItem
}
