import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getApiAccessToken, getTokenOrReauthenticate, setTokenGetter } from './auth'

describe('Auth0 API access tokens', () => {
  const tokenGetter = vi.fn()

  beforeEach(() => {
    tokenGetter.mockReset().mockResolvedValue('access-token')
    setTokenGetter(tokenGetter)
    vi.stubEnv('VITE_AUTH0_AI_RECIPE_AUDIENCE', 'https://api.chopchop.local/ai-recipe-generation')
    vi.stubEnv('VITE_AUTH0_INTERACTIVE_REVIEW_AUDIENCE', 'https://api.chopchop.local/interactive-review')
    vi.stubEnv('VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE', 'https://api.chopchop.local/recipe-management')
    vi.stubEnv('VITE_AUTH0_COOKBOOK_AUDIENCE', 'https://api.chopchop.local/cookbook')
    vi.stubEnv('VITE_AUTH0_MEAL_PLANNER_AUDIENCE', 'https://api.chopchop.local/meal-planner')
  })

  afterEach(() => vi.unstubAllEnvs())

  it('requests only the AI permission needed for the operation', async () => {
    await expect(getApiAccessToken('ai', 'recipe-generations.create')).resolves.toBe('access-token')
    expect(tokenGetter).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/ai-recipe-generation',
        scope: 'recipe-generations.create',
      },
    })
  })

  it('requests moderator access only when a moderation operation needs it', async () => {
    await getApiAccessToken('review', 'reviews.moderate')
    expect(tokenGetter).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/interactive-review',
        scope: 'reviews.moderate',
      },
    })
  })

  it('forces a fresh token when retrying an unauthorized API request', async () => {
    await getApiAccessToken('recipes', 'recipes.write', true)
    expect(tokenGetter).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/recipe-management',
        scope: 'recipes.write',
      },
      cacheMode: 'off',
    })
  })

  it('requests only the Cookbook permission needed for the operation', async () => {
    await getApiAccessToken('cookbook', 'cookbook.read')
    expect(tokenGetter).toHaveBeenCalledWith({
      authorizationParams: {
        audience: 'https://api.chopchop.local/cookbook',
        scope: 'cookbook.read',
      },
    })
  })

  it('requests least-privilege Meal Planner API permissions from the shared Auth0 client', async () => {
    await getApiAccessToken('mealPlanner', 'mealplans.read')
    await getApiAccessToken('mealPlanner', 'mealplans.write')

    expect(tokenGetter).toHaveBeenNthCalledWith(1, {
      authorizationParams: {
        audience: 'https://api.chopchop.local/meal-planner',
        scope: 'mealplans.read',
      },
    })
    expect(tokenGetter).toHaveBeenNthCalledWith(2, {
      authorizationParams: {
        audience: 'https://api.chopchop.local/meal-planner',
        scope: 'mealplans.write',
      },
    })
  })

  it('does not request a permission from the wrong API', async () => {
    await expect(getApiAccessToken('review', 'cookbook.write')).rejects.toThrow(
      'Permission cookbook.write is not valid for the review API.',
    )
    expect(tokenGetter).not.toHaveBeenCalled()
  })

  it('does not request a token for an API with no configured audience', async () => {
    vi.stubEnv('VITE_AUTH0_COOKBOOK_AUDIENCE', '')
    await expect(getApiAccessToken('cookbook', 'cookbook.read')).rejects.toThrow('VITE_AUTH0_COOKBOOK_AUDIENCE')
    expect(tokenGetter).not.toHaveBeenCalled()
  })
})

describe('missing API refresh token recovery', () => {
  const options = { authorizationParams: { audience: 'https://api.chopchop.local/cookbook', scope: 'cookbook.read' } }

  it('obtains a token through interactive authorization when needed', async () => {
    const getToken = vi.fn().mockRejectedValue({ error: 'missing_refresh_token' })
    const reauthenticate = vi.fn().mockResolvedValue('popup-token')

    await expect(getTokenOrReauthenticate(options, getToken, reauthenticate)).resolves.toBe('popup-token')
    expect(reauthenticate).toHaveBeenCalledWith(options.authorizationParams)
  })

  it('does not sign in again for unrelated token errors', async () => {
    const failure = new Error('network down')
    const reauthenticate = vi.fn()

    await expect(
      getTokenOrReauthenticate(options, vi.fn().mockRejectedValue(failure), reauthenticate),
    ).rejects.toBe(failure)
    expect(reauthenticate).not.toHaveBeenCalled()
  })
})
