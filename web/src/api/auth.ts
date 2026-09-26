import { requestJson } from './http'

export type LoginRequest = {
  account: string
  password: string
  rememberDevice?: boolean
}

export type MfaChallenge = {
  requiresMfa: boolean
  mfaToken: string
  maskedPhone: string
  expiresAtUtc: string
  rememberDeviceSupported: boolean
}

export type MfaVerifyRequest = {
  mfaToken: string
  code: string
}

export type AuthenticatedUser = {
  id: number
  username: string
  displayName: string
  email: string
  role: string
}

export type AuthTokenResponse = {
  accessToken: string
  tokenType: string
  expiresAtUtc: string
  user: AuthenticatedUser
}

export type ForgotPasswordResponse = {
  message: string
  demoResetToken: string | null
  expiresAtUtc: string | null
}

export function login(request: LoginRequest): Promise<MfaChallenge> {
  return requestJson<MfaChallenge>('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ rememberDevice: false, ...request }),
  })
}

export function verifyMfa(request: MfaVerifyRequest): Promise<AuthTokenResponse> {
  return requestJson<AuthTokenResponse>('/api/auth/mfa/verify', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })
}

export function getCurrentUser(signal?: AbortSignal): Promise<AuthenticatedUser> {
  return requestJson<AuthenticatedUser>('/api/auth/me', { signal }, true)
}

export function forgotPassword(account: string): Promise<ForgotPasswordResponse> {
  return requestJson<ForgotPasswordResponse>('/api/auth/forgot-password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ account }),
  })
}

export function resetPassword(token: string, newPassword: string): Promise<{ message: string }> {
  return requestJson<{ message: string }>('/api/auth/reset-password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ token, newPassword }),
  })
}
