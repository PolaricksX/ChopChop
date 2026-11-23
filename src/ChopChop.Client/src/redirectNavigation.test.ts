import { afterEach, describe, expect, it } from 'vitest'
import { clearRedirectNavigation, getRedirectNavigation, handleAuthRedirect } from './redirectNavigation'

describe('Auth0 redirect navigation', () => {
  afterEach(() => window.history.replaceState(null, '', '/'))

  it.each([
    { activePanel: 'cookbook', recipeView: 'browse', selectedRecipeId: null },
    { activePanel: 'meal-planner', recipeView: 'browse', selectedRecipeId: null },
    { activePanel: 'recipes', recipeView: 'drafts', selectedRecipeId: null },
    { activePanel: 'recipes', recipeView: 'details', selectedRecipeId: 'recipe-123' },
    { activePanel: 'recipes', recipeView: 'edit', selectedRecipeId: 'recipe-456' },
  ])('restores and consumes $activePanel / $recipeView', (navigation) => {
    window.history.replaceState({ existing: 'kept' }, '', '/?code=example&state=example')
    handleAuthRedirect({ navigation })

    expect(window.location.search).toBe('')
    expect(getRedirectNavigation()).toEqual(navigation)
    clearRedirectNavigation()
    expect(getRedirectNavigation()).toBeUndefined()
    expect(window.history.state).toEqual({ existing: 'kept' })
  })

  it('ignores malformed navigation and a regular sign-in', () => {
    handleAuthRedirect({ navigation: { activePanel: 'unknown', recipeView: 'details', selectedRecipeId: null } })
    expect(getRedirectNavigation()).toBeUndefined()
    handleAuthRedirect()
    expect(getRedirectNavigation()).toBeUndefined()
  })
})
