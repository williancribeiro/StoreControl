import { createContext } from 'react'
import type { CurrentUser } from './authTypes'

export type AuthContextValue = {
  user: CurrentUser | null
  isBootstrapping: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => Promise<void>
  hasPermission: (permission: string) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)
