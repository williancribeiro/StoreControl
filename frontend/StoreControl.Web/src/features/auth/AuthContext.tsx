import { useCallback, useEffect, useMemo, useState } from 'react'
import { setAccessToken, setRefreshHandler } from '../../shared/api/httpClient'
import { login as requestLogin, logout as requestLogout, refreshSession } from './authService'
import type { AuthResponse } from './authTypes'
import { AuthContext, type AuthContextValue } from './authContextValue'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<AuthResponse | null>(null)
  const [isBootstrapping, setIsBootstrapping] = useState(true)

  const applySession = useCallback((nextSession: AuthResponse | null) => {
    setSession(nextSession)
    setAccessToken(nextSession?.accessToken ?? null)
  }, [])

  const refresh = useCallback(async () => {
    const nextSession = await refreshSession()
    applySession(nextSession)
    return nextSession?.accessToken ?? null
  }, [applySession])

  useEffect(() => {
    setRefreshHandler(refresh)
    // The refresh cookie is the external session state synchronized by this effect.
    // oxlint-disable-next-line react/set-state-in-effect
    void refresh().finally(() => setIsBootstrapping(false))
    return () => setRefreshHandler(null)
  }, [refresh])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      isBootstrapping,
      login: async (email, password) => applySession(await requestLogin(email, password)),
      logout: async () => {
        try {
          await requestLogout()
        } finally {
          applySession(null)
        }
      },
      hasPermission: (permission) => session?.user.permissions.includes(permission) ?? false,
    }),
    [applySession, isBootstrapping, session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
