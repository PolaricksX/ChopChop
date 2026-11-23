import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getApiAccessToken } from './auth'
import { handleAuthRedirect } from './redirectNavigation'

const authState = vi.hoisted(() => ({
  loginWithRedirect: vi.fn(),
  getAccessTokenSilently: vi.fn(),
  getAccessTokenWithPopup: vi.fn(),
  logout: vi.fn(),
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
    logout: authState.logout,
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
    window.history.replaceState(null, '', '/')
    sessionStorage.clear()
    authState.loginWithRedirect.mockReset()
    authState.getAccessTokenSilently.mockReset().mockResolvedValue('access-token')
    authState.getAccessTokenWithPopup.mockReset().mockResolvedValue('popup-token')
    authState.logout.mockReset().mockResolvedValue(undefined)
    authState.isAuthenticated = false
    authState.isLoading = false
  })

  it('calls Auth0 logout with the existing returnTo parameter', async () => {
    authState.isAuthenticated = true
    render(<App />)

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    await waitFor(() => expect(authState.logout).toHaveBeenCalledWith({
      logoutParams: { returnTo: `${window.location.origin}/` },
    }))
  })

  it('cleans up the loaded profile before calling Auth0 logout', async () => {
    authState.isAuthenticated = true
    vi.stubEnv('VITE_AUTH0_USER_SERVICE_AUDIENCE', 'https://api.chopchop.local/user-service')
    const order: string[] = []
    const revokeObjectUrl = vi.fn(() => {
      order.push('cleanup')
    })
    const createObjectUrl = vi.fn().mockReturnValue('blob:profile-picture')
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      writable: true,
      value: createObjectUrl,
    })
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      writable: true,
      value: revokeObjectUrl,
    })
    authState.logout.mockImplementationOnce(async () => {
      order.push('logout')
      expect(revokeObjectUrl).toHaveBeenCalledWith('blob:profile-picture')
    })
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['pic'], { type: 'image/png' }), { status: 200 })))

    render(<App />)

    fireEvent.click(screen.getByRole('button', { name: 'Profile' }))
    expect(await screen.findByText('Taylor Cook')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Profile picture' })).toHaveAttribute('src', 'blob:profile-picture')

    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }))

    await waitFor(() => expect(authState.logout).toHaveBeenCalledTimes(1))
    expect(order).toEqual(['cleanup', 'logout'])
    expect(screen.queryByText('Taylor Cook')).toBeNull()
    expect(screen.queryByText('taylor@example.com')).toBeNull()
    expect(screen.queryByRole('img', { name: 'Profile picture' })).toBeNull()
  })

  it('keeps recipe and review tools behind OIDC sign-in', async () => {
    render(<App />)

    expect(await screen.findByRole('button', { name: 'Sign in to continue' })).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Create a recipe draft' })).toBeNull()
  })

  it('starts Auth0 Universal Login from the sign-in action', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in to continue' }))

    expect(authState.loginWithRedirect).toHaveBeenCalledWith({
      authorizationParams: { scope: 'cookbook.read' },
    })
  })

  it('starts Auth0 Universal Signup from the create-account action', async () => {
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Create an account' }))

    expect(authState.loginWithRedirect).toHaveBeenCalledWith({
      authorizationParams: { scope: 'cookbook.read', screen_hint: 'signup', requested_role: 'user' },
    })
  })

  it('sends the chosen Pro Chef account type with Auth0 Universal Signup', async () => {
    render(<App />)
    fireEvent.change(await screen.findByLabelText('Account type'), { target: { value: 'pro-chef' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create an account' }))

    expect(authState.loginWithRedirect).toHaveBeenCalledWith({
      authorizationParams: { scope: 'cookbook.read', screen_hint: 'signup', requested_role: 'pro-chef' },
    })
  })

  it('does not offer Admin as a sign-up account type', async () => {
    render(<App />)
    const select = await screen.findByLabelText('Account type')

    expect(within(select).queryByRole('option', { name: 'Admin' })).not.toBeInTheDocument()
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

  it('preserves distinct authorization requests while a popup is open', async () => {
    authState.isAuthenticated = true
    authState.getAccessTokenSilently.mockRejectedValue({ error: 'missing_refresh_token' })
    vi.stubEnv('VITE_AUTH0_AI_RECIPE_AUDIENCE', 'https://api.chopchop.local/ai')
    vi.stubEnv('VITE_AUTH0_COOKBOOK_AUDIENCE', 'https://api.chopchop.local/cookbook')
    let finishPopup: (token: string) => void = () => { throw new Error('Popup not started') }
    authState.getAccessTokenWithPopup.mockImplementationOnce(() => new Promise<string>((resolve) => {
      finishPopup = resolve
    }))
    render(<App />)

    await act(async () => {
      await expect(getApiAccessToken('cookbook', 'cookbook.read')).rejects.toThrow('Additional sign-in')
    })
    fireEvent.click(screen.getByRole('button', { name: 'Authorize access' }))
    await act(async () => {
      await expect(getApiAccessToken('ai', 'recipe-generations.read')).rejects.toThrow('Additional sign-in')
      await expect(getApiAccessToken('ai', 'recipe-generations.read')).rejects.toThrow('Additional sign-in')
    })
    expect(screen.getByText(/Authorize cookbook.read access/)).toBeInTheDocument()
    await act(async () => finishPopup('cookbook-token'))
    expect(screen.getByText(/Authorize recipe-generations.read access/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Authorize access' }))
    await waitFor(() => expect(authState.getAccessTokenWithPopup).toHaveBeenLastCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/ai',
        scope: 'recipe-generations.read',
      },
    }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Authorize access' })).not.toBeInTheDocument())
    expect(authState.getAccessTokenWithPopup).toHaveBeenCalledTimes(2)
  })

  it('keeps later authorization requests when the first is cancelled', async () => {
    authState.isAuthenticated = true
    authState.getAccessTokenSilently.mockRejectedValue({ error: 'missing_refresh_token' })
    vi.stubEnv('VITE_AUTH0_AI_RECIPE_AUDIENCE', 'https://api.chopchop.local/ai')
    vi.stubEnv('VITE_AUTH0_COOKBOOK_AUDIENCE', 'https://api.chopchop.local/cookbook')
    render(<App />)

    await act(async () => {
      await expect(getApiAccessToken('cookbook', 'cookbook.read')).rejects.toThrow('Additional sign-in')
      await expect(getApiAccessToken('ai', 'recipe-generations.read')).rejects.toThrow('Additional sign-in')
    })
    expect(screen.getByText(/Authorize cookbook.read access/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.getByText(/Authorize recipe-generations.read access/)).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(screen.queryByRole('button', { name: 'Authorize access' })).not.toBeInTheDocument()
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
