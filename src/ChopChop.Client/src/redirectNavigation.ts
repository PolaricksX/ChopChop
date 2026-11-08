import type { AppState } from '@auth0/auth0-react'

const panels = ['recipe', 'recipes', 'cookbook', 'meal-planner', 'review', 'moderation'] as const
const recipeViews = ['browse', 'drafts', 'new', 'details', 'edit'] as const

export type Panel = (typeof panels)[number]
export type RecipeView = (typeof recipeViews)[number]

export interface RedirectNavigation {
  activePanel: Panel
  recipeView: RecipeView
  selectedRecipeId: string | null
}

function isRedirectNavigation(value: unknown): value is RedirectNavigation {
  if (typeof value !== 'object' || value === null) return false
  const navigation = value as Record<string, unknown>
  return typeof navigation.activePanel === 'string'
    && panels.some((panel) => panel === navigation.activePanel)
    && typeof navigation.recipeView === 'string'
    && recipeViews.some((view) => view === navigation.recipeView)
    && (navigation.selectedRecipeId === null || typeof navigation.selectedRecipeId === 'string')
    && (!['details', 'edit'].includes(navigation.recipeView) || !!navigation.selectedRecipeId)
}

export function handleAuthRedirect(appState?: AppState) {
  const state = window.history.state
  const historyState = typeof state === 'object' && state !== null ? state : {}
  window.history.replaceState(
    { ...historyState, chopchopNavigation: isRedirectNavigation(appState?.navigation) ? appState.navigation : null },
    document.title,
    window.location.pathname,
  )
}

export function getRedirectNavigation(): RedirectNavigation | undefined {
  const state = window.history.state
  if (typeof state !== 'object' || state === null || !('chopchopNavigation' in state)) return undefined
  return isRedirectNavigation(state.chopchopNavigation) ? state.chopchopNavigation : undefined
}

export function clearRedirectNavigation() {
  const state = window.history.state
  if (typeof state !== 'object' || state === null || !('chopchopNavigation' in state)) return
  const { chopchopNavigation: _navigation, ...remainingState } = state
  window.history.replaceState(remainingState, document.title)
}
