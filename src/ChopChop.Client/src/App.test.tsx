import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { userManager } from './auth'

vi.mock('./auth', () => ({
  userManager: {
    getUser: vi.fn(),
    signinRedirect: vi.fn(),
    signinRedirectCallback: vi.fn(),
    signoutRedirect: vi.fn(),
  },
}))

describe('App authentication gate', () => {
  beforeEach(() => {
    vi.mocked(userManager.getUser).mockResolvedValue(null)
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
})
