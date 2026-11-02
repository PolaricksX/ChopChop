import type { DraftRecipe, Page, PublishedRecipe, RecipeContent, RecipeSummary } from './types'

export class RecipeApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) { super(message); this.name = 'RecipeApiError'; this.status = status }
}

const baseUrl = import.meta.env.VITE_RECIPE_MANAGEMENT_API_URL ?? 'http://localhost:5083'

async function request<T>(path: string, token?: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  headers.set('X-Correlation-Id', crypto.randomUUID())
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(new URL(path, baseUrl), { ...init, headers })
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
  return request<Page<RecipeSummary>>(`/api/v1/recipes?${query}`, undefined)
}
export const getPublishedRecipe = (id: string) => request<PublishedRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}`)
export const getDrafts = (token: string) => request<Page<DraftRecipe>>('/api/v1/recipes/drafts', token)
export const getDraft = (token: string, id: string) => request<DraftRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, token)
export const createDraft = (token: string, content: RecipeContent) => request<DraftRecipe>('/api/v1/recipes', token, { method: 'POST', headers: { 'Idempotency-Key': crypto.randomUUID() }, body: JSON.stringify(content) })
export const updateDraft = (token: string, id: string, expectedVersion: number, content: RecipeContent) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, token, { method: 'PUT', body: JSON.stringify({ expectedVersion, content }) })
export const deleteDraft = (token: string, id: string) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/draft`, token, { method: 'DELETE' })
export const publishDraft = (token: string, id: string) => request<PublishedRecipe>(`/api/v1/recipes/${encodeURIComponent(id)}/publish`, token, { method: 'POST' })
export const unpublishRecipe = (token: string, id: string) => request<void>(`/api/v1/recipes/${encodeURIComponent(id)}/unpublish`, token, { method: 'POST' })
