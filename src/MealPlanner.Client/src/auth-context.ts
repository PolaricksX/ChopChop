import { createContext, useContext } from 'react'
import type { User } from 'oidc-client-ts'

export interface AuthManagerPort {
  getUser(): Promise<User | null>
  signinRedirect(): Promise<void>
  signinRedirectCallback(): Promise<User>
  signoutRedirect(): Promise<void>
  removeUser(): Promise<void>
  events: {
    addUserLoaded(callback: (user: User) => void): void
    removeUserLoaded(callback: (user: User) => void): void
    addUserUnloaded(callback: () => void): void
    removeUserUnloaded(callback: () => void): void
    addAccessTokenExpired(callback: () => void): void
    removeAccessTokenExpired(callback: () => void): void
  }
}

export type AuthPhase = 'loading' | 'ready' | 'error'

export interface AuthContextValue {
  phase: AuthPhase
  user: User | null
  error: string | null
  sessionExpired: boolean
  canSignIn: boolean
  signIn(): Promise<void>
  signOut(): Promise<void>
  expireSession(): Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider.')
  }
  return value
}
