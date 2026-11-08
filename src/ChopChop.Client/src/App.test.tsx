import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { handleAuthRedirect } from './redirectNavigation'

const authState = vi.hoisted(() => ({
  loginWithRedirect: vi.fn(),
  getAccessTokenSilently: vi.fn(),
  getAccessTokenWithPopup: vi.fn(),
  isAuthenticated: false,
  isLoading: false,
}))

vi.mock('@auth0/auth0-react', () => ({
  useAuth0: () => ({
    error: undefined,
    getAccessTokenSilently: authState.getAccessTokenSilently,
    getAccessTokenWithPopup: authState.getAccessTokenWithPopup,
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
    window.history.replaceState(null, '', '/')
    sessionStorage.clear()
    authState.loginWithRedirect.mockReset()
    authState.getAccessTokenSilently.mockReset().mockResolvedValue('access-token')
    authState.getAccessTokenWithPopup.mockReset().mockResolvedValue('popup-token')
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

  it('stops loading and asks for authorization without leaving the current page', async () => {
    authState.isAuthenticated = true
    authState.getAccessTokenSilently.mockRejectedValue({ error: 'missing_refresh_token' })
    authState.loginWithRedirect.mockResolvedValue(undefined)
    vi.stubEnv('VITE_AUTH0_MEAL_PLANNER_AUDIENCE', 'https://api.chopchop.local/meal-planner')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], page: 1, pageSize: 20, totalCount: 0 }), {
        headers: { 'Content-Type': 'application/json' },
      }),
    ))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Meal Plans' }))

    fireEvent.click(await screen.findByRole('button', { name: 'Authorize access' }))
    await waitFor(() => expect(authState.getAccessTokenWithPopup).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/meal-planner',
        scope: 'mealplans.read',
      },
    }))
    expect(await screen.findByText('Access authorized. Please try your action again.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Your meal plans' })).toBeInTheDocument()
    expect(authState.loginWithRedirect).not.toHaveBeenCalled()
  })

  it('reports a cancelled authorization instead of redirecting', async () => {
    authState.isAuthenticated = true
    authState.getAccessTokenSilently.mockRejectedValue({ error: 'missing_refresh_token' })
    vi.stubEnv('VITE_AUTH0_MEAL_PLANNER_AUDIENCE', 'https://api.chopchop.local/meal-planner')
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Meal Plans' }))
    await screen.findByRole('button', { name: 'Authorize access' })
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('button', { name: 'Authorize access' })).not.toBeInTheDocument()
    expect(authState.loginWithRedirect).not.toHaveBeenCalled()
    expect(authState.getAccessTokenWithPopup).not.toHaveBeenCalled()
  })

  it('restores navigation after an existing Auth0 redirect', async () => {
    authState.isAuthenticated = true
    authState.isLoading = true
    const returned = render(<App />)
    handleAuthRedirect({ navigation: {
      activePanel: 'recipes',
      recipeView: 'drafts',
      selectedRecipeId: null,
    } })
    authState.isLoading = false
    vi.stubEnv('VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE', 'https://api.chopchop.local/recipe-management')
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ items: [], page: 1, pageSize: 20, totalCount: 0 }),
      { headers: { 'Content-Type': 'application/json' } },
    )))
    returned.rerender(<App />)
    expect(await screen.findByText('No drafts found.')).toBeInTheDocument()
  })

  it('shows the saved draft in My drafts after a successful save', async () => {
    authState.isAuthenticated = true
    vi.stubEnv('VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE', 'https://api.chopchop.local/recipe-management')
    const fetchMock = vi.fn().mockImplementation((_input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return Promise.resolve(new Response(JSON.stringify({ recipeId: 'draft-1' }), { status: 201 }))
      }
      return Promise.resolve(new Response(JSON.stringify({
        items: [{ recipeId: 'draft-1', status: 'Draft', version: { title: 'Tomato soup', version: 1 } }],
        page: 1, pageSize: 20, totalCount: 1,
      }), { status: 200 }))
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Recipes' }))
    fireEvent.click(screen.getByRole('button', { name: 'New recipe' }))
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Tomato soup' } })
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'A simple soup' } })
    fireEvent.change(screen.getByLabelText('Ingredient 1 name'), { target: { value: 'Tomato' } })
    fireEvent.change(screen.getByLabelText('Ingredient 1 unit'), { target: { value: 'piece' } })
    fireEvent.change(screen.getByLabelText('Step 1'), { target: { value: 'Cook the tomato.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(await screen.findByText('Draft saved.')).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Tomato soup' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'My drafts' })).toBeInTheDocument()
  })

  it('keeps unsaved draft contents and stops loading when authorization is needed', async () => {
    authState.isAuthenticated = true
    authState.getAccessTokenSilently.mockRejectedValue({ error: 'missing_refresh_token' })
    vi.stubEnv('VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE', 'https://api.chopchop.local/recipe-management')
    vi.stubGlobal('fetch', vi.fn().mockImplementation((_input: RequestInfo | URL, init?: RequestInit) =>
      Promise.resolve(new Response(JSON.stringify(init?.method === 'POST'
        ? { recipeId: 'draft-1' }
        : { items: [{ recipeId: 'draft-1', status: 'Draft', version: { title: 'Tomato soup', version: 1 } }], page: 1, pageSize: 20, totalCount: 1 }),
      { headers: { 'Content-Type': 'application/json' } })),
    ))
    render(<App />)
    fireEvent.click(screen.getByRole('button', { name: 'Recipes' }))
    fireEvent.click(screen.getByRole('button', { name: 'New recipe' }))
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'Tomato soup' } })
    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'A simple soup' } })
    fireEvent.change(screen.getByLabelText('Ingredient 1 name'), { target: { value: 'Tomato' } })
    fireEvent.change(screen.getByLabelText('Ingredient 1 unit'), { target: { value: 'piece' } })
    fireEvent.change(screen.getByLabelText('Step 1'), { target: { value: 'Cook the tomato.' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(await screen.findByRole('button', { name: 'Authorize access' })).toBeInTheDocument()
    expect(await screen.findByText(/Additional sign-in is required/)).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Tomato soup')
    expect(screen.getByRole('button', { name: 'Save draft' })).toBeEnabled()
    fireEvent.click(screen.getByRole('button', { name: 'Authorize access' }))
    expect(await screen.findByText('Access authorized. Please try your action again.')).toBeInTheDocument()
    authState.getAccessTokenSilently.mockResolvedValue('access-token')
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))
    expect(await screen.findByRole('heading', { name: 'Tomato soup' })).toBeInTheDocument()
  })

  it('opens the authenticated Equipment tab', async () => {
    vi.mocked(userManager.getUser).mockResolvedValue({
      access_token: 'equipment-token',
      profile: { name: 'Test cook', preferred_username: 'cook' },
    } as never)
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    }))))

    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Equipment' }))

    expect(await screen.findByRole('heading', { name: 'Equipment' })).toBeTruthy()
    expect(await screen.findByText('No equipment yet. Add your first kitchen item.')).toBeTruthy()
  })
})
