import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { User } from 'oidc-client-ts'
import App from './App'
import { userManager } from './auth'

vi.mock('./auth', () => ({
  userManager: {
    getUser: vi.fn(),
    signinRedirect: vi.fn(),
    signinRedirectCallback: vi.fn(),
    signoutRedirect: vi.fn(),
    events: {
      addUserLoaded: vi.fn(),
      removeUserLoaded: vi.fn(),
      addUserUnloaded: vi.fn(),
      removeUserUnloaded: vi.fn(),
      addAccessTokenExpired: vi.fn(),
      removeAccessTokenExpired: vi.fn(),
    },
  },
}))

describe('App authentication gate', () => {
  beforeEach(() => {
    vi.mocked(userManager.getUser).mockResolvedValue(null)
    window.localStorage.clear()
  })

  it('keeps recipe and review tools behind OIDC sign-in', async () => {
    render(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in to continue' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Create a recipe draft' })).toBeNull()
  })

  it('starts the authorization-code sign-in flow from the sign-in action', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in to continue' }))

    expect(userManager.signinRedirect).toHaveBeenCalledOnce()
  })

  it('shows profile onboarding inside the authenticated client', async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({
      access_token: 'access-token',
      expired: false,
      profile: {
        sub: 'subject-one',
        preferred_username: 'alice',
        email: 'alice@example.com',
        name: 'Alice',
      },
    } as User)
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Profile' }))

    expect(await screen.findByText(/Create your profile with a username/)).toBeTruthy()
    expect((screen.getByLabelText('Email') as HTMLInputElement).value).toBe('alice@example.com')
  })
})
