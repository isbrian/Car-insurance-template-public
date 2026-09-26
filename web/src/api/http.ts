import { clearAccessToken, getAccessToken } from './session'

export type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export type ApiErrorKind = 'unauthorized' | 'forbidden' | 'http'

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly kind: ApiErrorKind,
    message: string,
    public readonly problem?: ProblemDetails,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

async function parseProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.clone().json()) as ProblemDetails
  } catch {
    return undefined
  }
}

function getErrorMessage(response: Response, problem?: ProblemDetails): string {
  const validationMessage = problem?.errors
    ? Object.values(problem.errors).flat().filter(Boolean).join('；')
    : undefined

  return problem?.detail || validationMessage || problem?.title || `請求失敗（${response.status}）`
}

export async function toApiError(response: Response): Promise<ApiError> {
  const problem = await parseProblem(response)
  const kind: ApiErrorKind = response.status === 401
    ? 'unauthorized'
    : response.status === 403
      ? 'forbidden'
      : 'http'

  return new ApiError(response.status, kind, getErrorMessage(response, problem), problem)
}

export async function authenticatedFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers)
  const token = getAccessToken()

  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const response = await fetch(input, { ...init, headers })

  if (response.status === 401) {
    clearAccessToken()
    throw await toApiError(response)
  }

  if (response.status === 403) {
    throw await toApiError(response)
  }

  return response
}

export async function requestJson<T>(
  input: RequestInfo | URL,
  init: RequestInit = {},
  authenticated = false,
): Promise<T> {
  const response = authenticated
    ? await authenticatedFetch(input, init)
    : await fetch(input, init)

  if (!response.ok) {
    throw await toApiError(response)
  }

  return (await response.json()) as T
}
