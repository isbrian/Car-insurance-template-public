import { authenticatedFetch, toApiError } from './http'

export type ApplicationBeneficiary = {
  id: number
  name: string
  nationalId: string
  relationshipToInsured: string
  percentage: number
}

export type ApplicationSignerRole = 'applicant' | 'insured' | 'agent'

export type ApplicationSignature = {
  id: number
  signerRole: ApplicationSignerRole
  signerName: string
  method: string
  sha256: string
  signedAtUtc: string
  ipAddress: string | null
  userAgent: string | null
}

export type ApplicationDocumentType = 'identity_document' | 'vehicle_registration'

export type ApplicationDocument = {
  id: number | null
  documentType: ApplicationDocumentType
  displayName: string
  status: 'missing' | 'uploaded' | 'returned' | string
  originalFileName: string | null
  contentType: string | null
  sizeBytes: number | null
  sha256: string | null
  returnReason: string | null
  uploadedAtUtc: string | null
  returnedAtUtc: string | null
}

export type InsuranceApplication = {
  id: number
  applicationNumber: string
  quoteId: number
  quoteNumber: string
  status: 'draft' | 'submitted' | string
  currentStep: number
  customerId: number
  customerName: string
  vehicleId: number
  licensePlate: string
  insuranceStartDate: string
  salesAgent: string
  channel: string
  totalPremium: number
  quoteValidUntilUtc: string
  applicantName: string | null
  applicantRelationshipToOwner: string | null
  paymentFrequency: string | null
  insuredName: string | null
  insuredNationalId: string | null
  insuredBirthDate: string | null
  insuredRelationshipToOwner: string | null
  insuredPhone: string | null
  insuredEmail: string | null
  insuredAddress: string | null
  beneficiaryDesignation: string | null
  beneficiaries: ApplicationBeneficiary[]
  signatures: ApplicationSignature[]
  documents: ApplicationDocument[]
  applicantSigned: boolean
  insuredSigned: boolean
  agentSigned: boolean
  termsAccepted: boolean
  electronicPolicyAccepted: boolean
  createdAtUtc: string
  updatedAtUtc: string
  submittedAtUtc: string | null
}

export type ApplicationCreateRequest = {
  quoteId: number
}

export type ApplicantStepRequest = {
  applicantName: string
  relationshipToOwner: string
  paymentFrequency: 'annual' | 'semiannual' | 'quarterly'
}

export type InsuredStepRequest = {
  name: string
  nationalId: string
  birthDate: string
  relationshipToOwner: string
  phone: string
  email: string | null
  address: string
}

export type BeneficiaryInput = {
  name: string
  nationalId: string
  relationshipToInsured: string
  percentage: number
}

export type BeneficiariesStepRequest = {
  designation: 'legal_heirs' | 'designated'
  beneficiaries: BeneficiaryInput[]
}

export type SignatureStatusStepRequest = {
  applicantSigned: boolean
  insuredSigned: boolean
  agentSigned: boolean
}

export type ApplicationSignatureCreateRequest = {
  signerRole: ApplicationSignerRole
  signatureDataUrl: string
}

export type DocumentConfirmationStepRequest = {
  termsAccepted: boolean
  electronicPolicyAccepted: boolean
}

async function readError(response: Response): Promise<Error> {
  return toApiError(response)
}

async function readApplication(response: Response): Promise<InsuranceApplication> {
  if (!response.ok) throw await readError(response)
  return (await response.json()) as InsuranceApplication
}

export async function getApplications(signal?: AbortSignal): Promise<InsuranceApplication[]> {
  const response = await authenticatedFetch('/api/applications', { signal })
  if (!response.ok) throw await readError(response)
  return (await response.json()) as InsuranceApplication[]
}

export async function getApplication(id: number, signal?: AbortSignal): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}`, { signal }))
}

export async function getApplicationByQuoteId(
  quoteId: number,
  signal?: AbortSignal,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/by-quote/${quoteId}`, { signal }))
}

export async function createApplication(request: ApplicationCreateRequest): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch('/api/applications', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function updateApplicant(
  id: number,
  request: ApplicantStepRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/applicant`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function updateInsured(
  id: number,
  request: InsuredStepRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/insured`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function updateBeneficiaries(
  id: number,
  request: BeneficiariesStepRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/beneficiaries`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function updateSignatureStatus(
  id: number,
  request: SignatureStatusStepRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/signature-status`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function signApplication(
  id: number,
  request: ApplicationSignatureCreateRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/signatures`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function clearApplicationSignature(
  id: number,
  signerRole: ApplicationSignerRole,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/signatures/${signerRole}`, {
    method: 'DELETE',
  }))
}

export function getApplicationSignatureImageUrl(id: number, signerRole: ApplicationSignerRole): string {
  return `/api/applications/${id}/signatures/${signerRole}/image`
}

export async function uploadApplicationDocument(
  id: number,
  documentType: ApplicationDocumentType,
  file: File,
): Promise<ApplicationDocument[]> {
  const formData = new FormData()
  formData.append('file', file)
  const response = await authenticatedFetch(`/api/applications/${id}/documents/${documentType}`, {
    method: 'POST',
    body: formData,
  })
  if (!response.ok) throw await readError(response)
  return response.json() as Promise<ApplicationDocument[]>
}

export function getApplicationDocumentContentUrl(
  id: number,
  documentType: ApplicationDocumentType,
): string {
  return `/api/applications/${id}/documents/${documentType}/content`
}

export async function returnApplicationDocument(
  id: number,
  documentType: ApplicationDocumentType,
  reason: string,
): Promise<ApplicationDocument[]> {
  const response = await authenticatedFetch(`/api/applications/${id}/documents/${documentType}/return`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  })
  if (!response.ok) throw await readError(response)
  return response.json() as Promise<ApplicationDocument[]>
}

export async function updateDocumentConfirmation(
  id: number,
  request: DocumentConfirmationStepRequest,
): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/document-confirmation`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  }))
}

export async function submitApplication(id: number): Promise<InsuranceApplication> {
  return readApplication(await authenticatedFetch(`/api/applications/${id}/submit`, { method: 'POST' }))
}
