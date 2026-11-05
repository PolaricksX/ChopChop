import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const loginWithRedirect = vi.fn()

vi.mock('@auth0/auth0-react', () => ({
  useAuth0: () => ({
    error: undefined,
    getAccessTokenSilently: vi.fn(),
    isAuthenticated: false,
    isLoading: false,
    loginWithRedirect,
    logout: vi.fn(),
    user: undefined,
  }),
}))

describe('App authentication gate', () => {
  beforeEach(() => {
    loginWithRedirect.mockReset()
  })

  it('keeps recipe and review tools behind OIDC sign-in', async () => {
    render(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in to continue' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Create a recipe draft' })).toBeNull()
  })

  it('starts Auth0 Universal Login from the sign-in action', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in to continue' }))

    expect(loginWithRedirect).toHaveBeenCalledOnce()
  })
})
