import {
  UserManager,
  type User,
} from 'oidc-client-ts'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { AuthContext, type AuthContextValue, type AuthManagerPort, type AuthPhase } from './auth-context'
import { createOidcSettings, type AuthConfiguration } from './oidc'

function readAuthConfiguration(): AuthConfiguration | string {
  const authority = import.meta.env.VITE_OIDC_AUTHORITY || 'http://localhost:8080/realms/chopchop'
  const clientId = import.meta.env.VITE_OIDC_CLIENT_ID?.trim() || ''
  const scope =
    import.meta.env.VITE_OIDC_SCOPE?.trim() ||
    'openid profile email mealplans.read mealplans.write'

  try {
    const url = new URL(authority)
    if (url.protocol !== 'https:' && url.protocol !== 'http:') {
      return 'The identity provider URL must use HTTP or HTTPS.'
    }
  } catch {
    return 'Set VITE_OIDC_AUTHORITY to a valid identity provider URL.'
  }

  if (!clientId) {
    return 'Set VITE_OIDC_CLIENT_ID to the public browser client registered with the identity provider.'
  }

  return {
    authority,
    clientId,
    scope,
    redirectUri: `${window.location.origin}/auth/callback`,
    postLogoutRedirectUri: window.location.origin,
  }
}

interface AuthProviderProps {
  children: ReactNode
  manager?: AuthManagerPort
}

export function AuthProvider({ children, manager: suppliedManager }: AuthProviderProps) {
  const [configuration] = useState(readAuthConfiguration)
  const [managerState] = useState(() => {
    if (suppliedManager) {
      return { manager: suppliedManager, error: null }
    }
    if (typeof configuration === 'string') {
      return { manager: null, error: configuration }
    }

    try {
      return { manager: new UserManager(createOidcSettings(configuration)), error: null }
    } catch (initializationError) {
      return {
        manager: null,
        error:
          initializationError instanceof Error
            ? initializationError.message
            : 'The identity provider could not be initialized.',
      }
    }
  })
  const manager = managerState.manager
  const [phase, setPhase] = useState<AuthPhase>(manager ? 'loading' : 'error')
  const [user, setUser] = useState<User | null>(null)
  const [error, setError] = useState<string | null>(managerState.error)
  const [sessionExpired, setSessionExpired] = useState(false)
  const callbackUser = useRef<Promise<User> | null>(null)

  useEffect(() => {
    if (!manager) {
      return
    }

    let active = true
    const applyUser = (nextUser: User | null) => {
      if (!active) {
        return
      }
      if (nextUser && !nextUser.expired && !nextUser.access_token) {
        setUser(null)
        setPhase('error')
        setError('The identity provider did not return an API access token.')
        return
      }
      setUser(nextUser && !nextUser.expired ? nextUser : null)
      setPhase('ready')
      setSessionExpired(false)
      setError(null)
    }
    const onUserLoaded = (nextUser: User) => applyUser(nextUser)
    const onUserUnloaded = () => applyUser(null)
    const onAccessTokenExpired = () => {
      void manager
        .removeUser()
        .then(() => {
          if (active) {
            setUser(null)
            setPhase('ready')
            setSessionExpired(true)
          }
        })
        .catch((expirationError: unknown) => {
          if (active) {
            setPhase('error')
            setError(
              expirationError instanceof Error
                ? expirationError.message
                : 'The expired sign-in session could not be cleared.',
            )
          }
        })
    }

    manager.events.addUserLoaded(onUserLoaded)
    manager.events.addUserUnloaded(onUserUnloaded)
    manager.events.addAccessTokenExpired(onAccessTokenExpired)

    const initialize = async () => {
      try {
        if (window.location.pathname.endsWith('/auth/callback')) {
          if (!callbackUser.current) {
            callbackUser.current = manager.signinRedirectCallback()
          }
          const signedInUser = await callbackUser.current
          applyUser(signedInUser)
          window.history.replaceState({}, document.title, '/')
          return
        }

        const storedUser = await manager.getUser()
        if (storedUser?.expired) {
          await manager.removeUser()
          if (active) {
            setUser(null)
            setPhase('ready')
            setSessionExpired(true)
            setError(null)
          }
        } else {
          applyUser(storedUser)
        }
      } catch (initializationError) {
        if (active) {
          setPhase('error')
          setError(
            initializationError instanceof Error
              ? initializationError.message
              : 'The sign-in session could not be restored.',
          )
        }
      } finally {
        if (window.location.pathname.endsWith('/auth/callback')) {
          window.history.replaceState({}, document.title, '/')
        }
      }
    }

    void initialize()

    return () => {
      active = false
      manager.events.removeUserLoaded(onUserLoaded)
      manager.events.removeUserUnloaded(onUserUnloaded)
      manager.events.removeAccessTokenExpired(onAccessTokenExpired)
    }
  }, [manager])

  const signIn = useCallback(async () => {
    if (!manager) {
      return
    }
    setError(null)
    try {
      await manager.signinRedirect()
    } catch (signInError) {
      setPhase('error')
      setError(
        signInError instanceof Error
          ? signInError.message
          : 'The identity provider could not start sign-in.',
      )
    }
  }, [manager])

  const signOut = useCallback(async () => {
    if (!manager) {
      return
    }
    setError(null)
    try {
      await manager.signoutRedirect()
    } catch (signOutError) {
      setPhase('error')
      setError(
        signOutError instanceof Error
          ? signOutError.message
          : 'The identity provider could not end the session.',
      )
    }
  }, [manager])

  const expireSession = useCallback(async () => {
    if (!manager) {
      return
    }
    try {
      await manager.removeUser()
      setUser(null)
      setPhase('ready')
      setSessionExpired(true)
      setError(null)
    } catch (expirationError) {
      setPhase('error')
      setError(
        expirationError instanceof Error
          ? expirationError.message
          : 'The expired sign-in session could not be cleared.',
      )
    }
  }, [manager])

  const value = useMemo<AuthContextValue>(
    () => ({
      phase,
      user,
      error,
      sessionExpired,
      canSignIn: manager !== null,
      signIn,
      signOut,
      expireSession,
    }),
    [phase, user, error, sessionExpired, manager, signIn, signOut, expireSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
