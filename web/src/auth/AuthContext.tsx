import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { getCurrentUser } from '../api/auth'
import type { AuthenticatedUser } from '../api/auth'
import { ApiError } from '../api/http'
import { clearAccessToken, getAccessToken, setAccessToken, subscribeToAccessTokenCleared } from '../api/session'

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

type AuthContextValue = {
  status: AuthStatus
  user: AuthenticatedUser | null
  initializationError: string | null
  completeLogin: (accessToken: string, user: AuthenticatedUser) => void
  logout: () => void
  retryInitialization: () => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

type AuthProviderProps = {
  children: ReactNode
}

function getInitializationMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  return '無法確認登入狀態，請稍後再試。'
}

export function AuthProvider({ children }: AuthProviderProps) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [initializationError, setInitializationError] = useState<string | null>(null)
  const [initializationAttempt, setInitializationAttempt] = useState(0)

  const clearPrivateQueries = useCallback(() => {
    void queryClient.cancelQueries().finally(() => queryClient.clear())
  }, [queryClient])

  useEffect(() => subscribeToAccessTokenCleared(() => {
    clearPrivateQueries()
    setUser(null)
    setInitializationError(null)
    setStatus('unauthenticated')
  }), [clearPrivateQueries])

  useEffect(() => {
    let active = true
    const token = getAccessToken()

    if (!token) {
      setUser(null)
      setInitializationError(null)
      setStatus('unauthenticated')
      return () => {
        active = false
      }
    }

    setStatus('loading')
    setInitializationError(null)

    void getCurrentUser()
      .then((currentUser) => {
        if (!active) return
        setUser(currentUser)
        setStatus('authenticated')
      })
      .catch((error: unknown) => {
        if (!active) return

        if (error instanceof ApiError && error.kind === 'unauthorized') {
          setUser(null)
          setStatus('unauthenticated')
          return
        }

        setInitializationError(getInitializationMessage(error))
        setStatus('loading')
      })

    return () => {
      active = false
    }
  }, [initializationAttempt])

  const completeLogin = useCallback((accessToken: string, currentUser: AuthenticatedUser) => {
    queryClient.clear()
    setAccessToken(accessToken)
    setUser(currentUser)
    setInitializationError(null)
    setStatus('authenticated')
  }, [queryClient])

  const logout = useCallback(() => {
    clearAccessToken()
    setUser(null)
    setInitializationError(null)
    setStatus('unauthenticated')
  }, [])

  const retryInitialization = useCallback(() => {
    setInitializationError(null)
    setInitializationAttempt((value) => value + 1)
  }, [])

  const value = useMemo<AuthContextValue>(() => ({
    status,
    user,
    initializationError,
    completeLogin,
    logout,
    retryInitialization,
  }), [completeLogin, initializationError, logout, retryInitialization, status, user])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth 必須在 AuthProvider 內使用')
  return context
}
