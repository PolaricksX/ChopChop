import { afterEach, describe, expect, it, vi } from 'vitest'
import { setTokenGetter } from '../auth'
import { getDrafts } from './api'

describe('recipe API authorization', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  it('explains which permission is missing when draft access is denied', async () => {
    vi.stubEnv('VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE', 'https://api.chopchop.local/recipe-management')
    setTokenGetter(vi.fn().mockResolvedValue('token'))
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 403 })))

    await expect(getDrafts()).rejects.toThrow('Your account needs the recipes.read permission in Auth0.')
  })
})
