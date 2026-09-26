const ACCESS_TOKEN_KEY = 'car-insurance.access-token'

const accessTokenClearedListeners = new Set<() => void>()

function getStorage(): Storage | null {
  try {
    return typeof window !== 'undefined' ? window.sessionStorage : null
  } catch {
    return null
  }
}

export function getAccessToken(): string | null {
  return getStorage()?.getItem(ACCESS_TOKEN_KEY) ?? null
}

export function setAccessToken(token: string): void {
  const value = token.trim()
  if (!value) {
    clearAccessToken()
    return
  }

  getStorage()?.setItem(ACCESS_TOKEN_KEY, value)
}

export function clearAccessToken(): void {
  getStorage()?.removeItem(ACCESS_TOKEN_KEY)
  accessTokenClearedListeners.forEach((listener) => listener())
}

export function subscribeToAccessTokenCleared(listener: () => void): () => void {
  accessTokenClearedListeners.add(listener)
  return () => accessTokenClearedListeners.delete(listener)
}
