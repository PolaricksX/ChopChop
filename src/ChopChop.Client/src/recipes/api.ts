import type { DraftRecipe, Page, PublishedRecipe, RecipeContent, RecipeSummary } from './types'
import { userManager } from '../auth'

export class RecipeApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) { super(message); this.name = 'RecipeApiError'; this.status = status }
}

const baseUrl = import.meta.env.VITE_RECIPE_MANAGEMENT_API_URL ?? 'http://localhost:5083'

const sessionExpiredMessage = 'Your session expired. Please sign in again.'

async function request<T>(path: string, init: RequestInit = {}, requiresAuth = false, retry = true): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  headers.set('X-Correlation-Id', crypto.randomUUID())
  if (requiresAuth) {
    const user = await userManager.getUser()
    if (!user?.access_token) throw new RecipeApiError(sessionExpiredMessage, 401)
    headers.set('Authorization', `Bearer ${user.access_token}`)
  }
  const response = await fetch(new URL(path, baseUrl), { ...init, headers })
  if (response.status === 401 && requiresAuth && retry) {
    try {
      await userManager.signinSilent()
      return request<T>(path, init, true, false)
    } catch {
      throw new RecipeApiError(sessionExpiredMessage, 401)
    }
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as { detail?: unknown; error?: unknown } | undefined
    const message = typeof problem?.detail === 'string' ? problem.detail : typeof problem?.error === 'string' ? problem.error : `The request failed (${response.status}).`
    throw new RecipeApiError(message, response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function listRecipes(filters: { page?: number; pageSize?: number; text?: string; tags?: string[] } = {}) {
  const query = new URLSearchParams()
  if (filters.page) query.set('page', String(filters.page)); if (filters.pageSize) query.set('pageSize', String(filters.pageSize))
  if (filters.text) query.set('text', filters.text); filters.tags?.forEach((tag) => query.append('tags', tag))
  return request<Page<RecipeSummary>>(`/api/v1/recipes?${query}`)
}
export const getPublishedRecipe = (id: string) => request<PublishedRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}`)
export const getDrafts = () => request<Page<DraftRecipe>>('/api/v1/recipes/drafts', {}, true)
export const getDraft = (id: string) => request<DraftRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, {}, true)
export const createDraft = (content: RecipeContent) => request<DraftRecipe>('/api/v1/recipes', { method: 'POST', body: JSON.stringify(content) }, true)
export const updateDraft = (id: string, expectedVersion: number, content: RecipeContent) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, { method: 'PUT', body: JSON.stringify({ expectedVersion, content }) }, true)
export const deleteDraft = (id: string) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, { method: 'DELETE' }, true)
export const publishDraft = (id: string) => request<PublishedRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}/publish`, { method: 'POST' }, true)
export const unpublishRecipe = (id: string) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/unpublish`, { method: 'POST' }, true)
