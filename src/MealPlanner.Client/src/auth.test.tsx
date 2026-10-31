import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { User } from 'oidc-client-ts'
import { AuthProvider } from './auth'
import type { AuthManagerPort } from './auth-context'
import { createOidcSettings } from './oidc'
import App from './App'

function fakeManager(): AuthManagerPort {
  return {
    getUser: vi.fn<() => Promise<User | null>>().mockResolvedValue(null),
    signinRedirect: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    signinRedirectCallback: vi.fn<() => Promise<User>>(),
    signoutRedirect: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    removeUser: vi.fn<() => Promise<void>>().mockResolvedValue(undefined),
    events: {
      addUserLoaded: vi.fn(),
      removeUserLoaded: vi.fn(),
      addUserUnloaded: vi.fn(),
      removeUserUnloaded: vi.fn(),
      addAccessTokenExpired: vi.fn(),
      removeAccessTokenExpired: vi.fn(),
    },
  }
}

describe('browser authentication', () => {
  it('uses authorization code flow and session-scoped storage without a client secret', () => {
    const settings = createOidcSettings({
      authority: 'https://identity.example.test/realms/chopchop',
      clientId: 'chopchop-browser',
      scope: 'openid profile mealplans.read mealplans.write',
      redirectUri: 'https://app.example.test/auth/callback',
      postLogoutRedirectUri: 'https://app.example.test/',
    })

    expect(settings.response_type).toBe('code')
    expect(settings.client_id).toBe('chopchop-browser')
    expect(settings.automaticSilentRenew).toBe(false)
    expect(settings).not.toHaveProperty('client_secret')
  })

  it('shows a clear authentication gate and starts provider sign-in', async () => {
    const manager = fakeManager()
    render(
      <AuthProvider manager={manager}>
        <App />
      </AuthProvider>,
    )

    const signInButton = await screen.findByRole('button', { name: 'Sign in' })
    expect(signInButton).toBeEnabled()
    fireEvent.click(signInButton)

    await waitFor(() => {
      expect(manager.signinRedirect).toHaveBeenCalledOnce()
    })
  })

  it('clears an expired token and asks the user to sign in again', async () => {
    const manager = fakeManager()
    render(
      <AuthProvider manager={manager}>
        <App />
      </AuthProvider>,
    )
    await screen.findByRole('button', { name: 'Sign in' })

    await act(async () => {
      const onExpired = vi.mocked(manager.events.addAccessTokenExpired).mock.calls[0]?.[0]
      onExpired?.()
      await Promise.resolve()
    })

    expect(await screen.findByRole('heading', { name: 'Your session expired' }))
      .toBeInTheDocument()
    expect(manager.removeUser).toHaveBeenCalledOnce()
  })
})
