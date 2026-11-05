import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { User } from 'oidc-client-ts'
import App from './App'
import { userManager } from './auth'

vi.mock('./auth', () => ({
  userManager: {
    getUser: vi.fn(),
    removeUser: vi.fn(),
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
    vi.mocked(userManager.removeUser).mockResolvedValue()
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

  it('opens Meal Planner inside the authenticated client with the existing access token', async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({
      access_token: 'access-token',
      expired: false,
      profile: { sub: 'subject-one', name: 'Alice' },
    } as User)
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(JSON.stringify({ items: [], page: 1, pageSize: 20, totalCount: 0 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    fireEvent.click(await screen.findByRole('button', { name: 'Meal Planner' }))

    expect(await screen.findByRole('heading', { name: 'Your meal plans' })).toBeTruthy()
    const requestHeaders = new Headers(fetchMock.mock.calls[0][1]?.headers)
    expect(requestHeaders.get('Authorization')).toBe('Bearer access-token')
  })

  it('clears the ChopChop session when the Meal Planner API rejects it', async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({
      access_token: 'access-token',
      expired: false,
      profile: { sub: 'subject-one', name: 'Alice' },
    } as User)
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(JSON.stringify({ title: 'Unauthorized' }), {
          status: 401,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    )
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Meal Planner' }))

    expect(await screen.findByRole('button', { name: 'Sign in to continue' })).toBeTruthy()
    expect(userManager.removeUser).toHaveBeenCalledOnce()
  })
})
