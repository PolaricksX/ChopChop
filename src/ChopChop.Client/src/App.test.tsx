import { fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const authState = vi.hoisted(() => ({
  loginWithRedirect: vi.fn(),
  getAccessTokenSilently: vi.fn(),
  isAuthenticated: false,
  isLoading: false,
}))

vi.mock('@auth0/auth0-react', () => ({
  useAuth0: () => ({
    error: undefined,
    getAccessTokenSilently: authState.getAccessTokenSilently,
    isAuthenticated: authState.isAuthenticated,
    isLoading: authState.isLoading,
    loginWithRedirect: authState.loginWithRedirect,
    logout: vi.fn(),
    user: undefined,
  }),
}))

describe('App authentication gate', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  beforeEach(() => {
    vi.stubEnv('VITE_DEV_AUTH', 'false')
    authState.loginWithRedirect.mockReset()
    authState.getAccessTokenSilently.mockReset().mockResolvedValue('access-token')
    authState.isAuthenticated = false
    authState.isLoading = false
  })

  it('keeps recipe and review tools behind OIDC sign-in', async () => {
    render(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in to continue' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Create a recipe draft' })).toBeNull()
  })

  it('starts Auth0 Universal Login from the sign-in action', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in to continue' }))

    expect(authState.loginWithRedirect).toHaveBeenCalledOnce()
  })

  it('opens the app without Auth0 when local development auth is enabled', async () => {
    vi.stubEnv('VITE_DEV_AUTH', 'true')
    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Create a recipe draft' })).toBeInTheDocument()
    expect(screen.getByText('Development mode')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Sign in to continue' })).toBeNull()
  })

  it('opens meal planning within the shared authenticated client', async () => {
    authState.isAuthenticated = true
    vi.stubEnv('VITE_AUTH0_MEAL_PLANNER_AUDIENCE', 'https://api.chopchop.local/meal-planner')
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ items: [], page: 1, pageSize: 20, totalCount: 0 }), {
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    )
    render(<App />)

    fireEvent.click(screen.getByRole('button', { name: 'Meal Plans' }))

    expect(await screen.findByRole('heading', { name: 'Your meal plans' })).toBeInTheDocument()
    expect(await screen.findByText('Start with a plan')).toBeInTheDocument()
    expect(authState.getAccessTokenSilently).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/meal-planner',
        scope: 'mealplans.read',
      },
    })
  })

  it('opens the authenticated Pantry tab with a Pantry API token', async () => {
    authState.isAuthenticated = true
    vi.stubEnv('VITE_AUTH0_PANTRY_AUDIENCE', 'https://api.chopchop.local/pantry')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([]))))

    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'My Pantry' }))

    expect(await screen.findByRole('heading', { name: 'My pantry' })).toBeInTheDocument()
    expect(await screen.findByText('No pantry items match these filters.')).toBeInTheDocument()
    expect(authState.getAccessTokenSilently).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/pantry',
        scope: 'pantry.read',
      },
    })
  })

  it('opens the authenticated Equipment tab with an Equipment API token', async () => {
    authState.isAuthenticated = true
    vi.stubEnv('VITE_AUTH0_EQUIPMENT_AUDIENCE', 'https://api.chopchop.local/equipment')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    }))))

    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Equipment' }))

    expect(await screen.findByRole('heading', { name: 'Equipment' })).toBeInTheDocument()
    expect(await screen.findByText('No equipment yet. Add your first kitchen item.')).toBeInTheDocument()
    expect(authState.getAccessTokenSilently).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/equipment',
        scope: 'equipment.read',
      },
    })
  })
})
