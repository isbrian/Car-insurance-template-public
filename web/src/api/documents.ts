import { authenticatedFetch, toApiError } from './http'

export type CaseDocument = {
  id: number
  applicationId: number
  applicationNumber: string
  customerId: number
  customerName: string
  vehicleId: number
  licensePlate: string
  documentType: string
  displayName: string
  status: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  returnReason: string | null
  uploadedAtUtc: string
  returnedAtUtc: string | null
  updatedAtUtc: string
}

export type CaseDocumentListResponse = {
  items: CaseDocument[]
  page: number
  pageSize: number
  total: number
}

export type CaseDocumentQuery = {
  keyword?: string
  status?: string
  documentType?: string
  page?: number
  pageSize?: number
}

export type DocumentTemplateSummary = {
  id: number
  code: string
  name: string
  description: string | null
  activeVersionId: number | null
  activeVersion: string | null
  versionCount: number
  createdAtUtc: string
  updatedAtUtc: string
}

export type DocumentTemplateVersion = {
  id: number
  version: string
  content: string | null
  sha256: string
  createdAtUtc: string
}

export type DocumentTemplateDetail = {
  id: number
  code: string
  name: string
  description: string | null
  activeVersionId: number | null
  activeVersion: string | null
  versions: DocumentTemplateVersion[]
  createdAtUtc: string
  updatedAtUtc: string
}

export type DocumentTemplateCreateRequest = {
  code: string
  name: string
  description?: string
}

export type DocumentTemplateVersionCreateRequest = {
  version: string
  content?: string
  activate: boolean
}


export async function getCaseDocuments(
  query: CaseDocumentQuery = {},
  signal?: AbortSignal,
): Promise<CaseDocumentListResponse> {
  const params = new URLSearchParams()
  if (query.keyword?.trim()) params.set('keyword', query.keyword.trim())
  if (query.status?.trim()) params.set('status', query.status.trim())
  if (query.documentType?.trim()) params.set('documentType', query.documentType.trim())
  if (query.page != null) params.set('page', String(query.page))
  if (query.pageSize != null) params.set('pageSize', String(query.pageSize))

  const suffix = params.size > 0 ? `?${params.toString()}` : ''
  const response = await authenticatedFetch(`/api/documents/cases${suffix}`, { signal })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as CaseDocumentListResponse
}

export function getCaseDocumentContentUrl(document: Pick<CaseDocument, 'applicationId' | 'documentType'>): string {
  return `/api/applications/${document.applicationId}/documents/${encodeURIComponent(document.documentType)}/content`
}

export async function getDocumentTemplates(signal?: AbortSignal): Promise<DocumentTemplateSummary[]> {
  const response = await authenticatedFetch('/api/document-templates', { signal })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as DocumentTemplateSummary[]
}

export async function getDocumentTemplate(id: number, signal?: AbortSignal): Promise<DocumentTemplateDetail> {
  const response = await authenticatedFetch(`/api/document-templates/${id}`, { signal })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as DocumentTemplateDetail
}

export async function createDocumentTemplate(
  request: DocumentTemplateCreateRequest,
): Promise<DocumentTemplateDetail> {
  const response = await authenticatedFetch('/api/document-templates', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as DocumentTemplateDetail
}

export async function addDocumentTemplateVersion(
  templateId: number,
  request: DocumentTemplateVersionCreateRequest,
): Promise<DocumentTemplateDetail> {
  const response = await authenticatedFetch(`/api/document-templates/${templateId}/versions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as DocumentTemplateDetail
}

export async function setDocumentTemplateActiveVersion(
  templateId: number,
  versionId: number,
): Promise<DocumentTemplateDetail> {
  const response = await authenticatedFetch(`/api/document-templates/${templateId}/active-version`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ versionId }),
  })
  if (!response.ok) throw await toApiError(response)
  return (await response.json()) as DocumentTemplateDetail
}
