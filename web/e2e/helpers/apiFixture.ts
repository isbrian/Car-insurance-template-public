import { expect, type APIRequestContext } from '@playwright/test'
import { DEMO_MFA_CODE, DEMO_PASSWORD } from './auth'

const SIGNATURE_PNG_DATA_URL = 'data:image/png;base64,iVBORw0KGgo='

export type SubmittedApplicationFixture = {
  applicationId: number
  applicationNumber: string
  quoteId: number
  quoteNumber: string
  customerName: string
  licensePlate: string
}

type AuthTokenResponse = { accessToken: string }
type MfaChallenge = { mfaToken: string }
type RateVersion = {
  id: number
  insuranceProductCode: string
  version: string
  isActive: boolean
}
type Quote = { id: number; quoteNumber: string }
type InsuranceApplication = {
  id: number
  applicationNumber: string
  quoteId: number
  quoteNumber: string
  customerName: string
  licensePlate: string
  status: string
}

async function expectOk(response: Awaited<ReturnType<APIRequestContext['get']>>, label: string): Promise<void> {
  expect(response.ok(), `${label} 失敗：HTTP ${response.status()} ${await response.text()}`).toBeTruthy()
}

function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}` }
}

function futureTaipeiBusinessDate(days: number): string {
  const target = new Date(Date.now() + days * 24 * 60 * 60 * 1000)
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(target)
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]))
  return `${values.year}-${values.month}-${values.day}`
}

export async function loginApi(request: APIRequestContext, account: string): Promise<string> {
  const loginResponse = await request.post('/api/auth/login', {
    data: { account, password: DEMO_PASSWORD, rememberDevice: false },
  })
  await expectOk(loginResponse, `${account} API login`)
  const challenge = await loginResponse.json() as MfaChallenge

  const verifyResponse = await request.post('/api/auth/mfa/verify', {
    data: { mfaToken: challenge.mfaToken, code: DEMO_MFA_CODE },
  })
  await expectOk(verifyResponse, `${account} API MFA`)
  const token = await verifyResponse.json() as AuthTokenResponse
  return token.accessToken
}

export async function createSubmittedApplicationFixture(request: APIRequestContext): Promise<SubmittedApplicationFixture> {
  const salesToken = await loginApi(request, 'sales')
  const headers = authHeaders(salesToken)

  const rateResponse = await request.get('/api/rate-versions', { headers })
  await expectOk(rateResponse, '讀取費率版本')
  const rateVersions = await rateResponse.json() as RateVersion[]
  const rateVersion = rateVersions.find((item) => item.version === 'provisional-v0.1' && item.insuranceProductCode === 'CTP-AUTO' && item.isActive)
    ?? rateVersions.find((item) => item.isActive)
  expect(rateVersion, '找不到可用費率版本').toBeTruthy()

  const quoteResponse = await request.post('/api/quotes', {
    headers,
    data: {
      customerId: 1,
      vehicleId: 1,
      insuranceStartDate: futureTaipeiBusinessDate(1),
      salesAgent: 'T-051C E2E',
      channel: 'e2e',
      status: 'draft',
      calculation: {
        rateVersionId: rateVersion!.id,
        driverBirthDate: '1988-05-20',
        driverGender: 'male',
        claimRecordLevel: 4,
        claimCountLast3Years: 1,
        vehicleCategory: '自用小客車',
        makeModelCode: 'TOYOTA-COROLLA',
        manufactureDate: '2021-06-01',
        usageType: '自用',
        thirdPartyBodilyInjuryPerPerson: null,
        thirdPartyPropertyDamagePerAccident: null,
        excessLiabilityCoverage: null,
        ownDamageType: null,
        deductible: null,
        theft: false,
        driverAccidentCoverage: null,
        riders: [],
        isRenewal: false,
        isMultiVehicle: false,
        isOnline: false,
      },
    },
  })
  await expectOk(quoteResponse, '建立 E2E Quote')
  const quote = await quoteResponse.json() as Quote

  const applicationResponse = await request.post('/api/applications', { headers, data: { quoteId: quote.id } })
  await expectOk(applicationResponse, '建立 E2E Application')
  let application = await applicationResponse.json() as InsuranceApplication

  await expectOk(await request.put(`/api/applications/${application.id}/applicant`, {
    headers,
    data: { applicantName: '王小明', relationshipToOwner: '本人', paymentFrequency: 'annual' },
  }), '寫入要保人資料')

  await expectOk(await request.put(`/api/applications/${application.id}/insured`, {
    headers,
    data: {
      name: '王小明', nationalId: 'A123456789', birthDate: '1988-05-20', relationshipToOwner: '本人',
      phone: '0912345678', email: 'ming@example.com', address: '台北市中正區重慶南路一段 1 號',
    },
  }), '寫入被保險人資料')

  await expectOk(await request.put(`/api/applications/${application.id}/beneficiaries`, {
    headers,
    data: { designation: 'legal_heirs', beneficiaries: [] },
  }), '寫入受益人資料')

  for (const signerRole of ['applicant', 'insured', 'agent']) {
    await expectOk(await request.post(`/api/applications/${application.id}/signatures`, {
      headers,
      data: { signerRole, signatureDataUrl: SIGNATURE_PNG_DATA_URL },
    }), `建立 ${signerRole} 簽名`)
  }

  await expectOk(await request.post(`/api/applications/${application.id}/documents/vehicle_registration`, {
    headers,
    multipart: {
      file: {
        name: 't051c-vehicle-registration.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('T-051C deterministic vehicle registration fixture'),
      },
    },
  }), '上傳行照 fixture')

  await expectOk(await request.put(`/api/applications/${application.id}/document-confirmation`, {
    headers,
    data: { termsAccepted: true, electronicPolicyAccepted: true },
  }), '確認條款與電子保單')

  const submitResponse = await request.post(`/api/applications/${application.id}/submit`, { headers })
  await expectOk(submitResponse, '送出 Application fixture')
  application = await submitResponse.json() as InsuranceApplication
  expect(application.status).toBe('submitted')

  return {
    applicationId: application.id,
    applicationNumber: application.applicationNumber,
    quoteId: quote.id,
    quoteNumber: quote.quoteNumber,
    customerName: application.customerName,
    licensePlate: application.licensePlate,
  }
}
