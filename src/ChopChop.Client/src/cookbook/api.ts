import { getApiAccessToken } from '../auth'
import type { Folder, SavedRecipe } from './types'

export class CookbookApiError extends Error {
  readonly status: number
  constructor(message: string, status: number) {
    super(message)
    this.name = 'CookbookApiError'
    this.status = status
  }
}

const baseUrl = import.meta.env.VITE_COOKBOOK_API_URL ?? 'http://localhost:20860'

type CookbookPermission = 'cookbook.read' | 'cookbook.write'

async function request<T>(
  path: string,
  init: RequestInit,
  permission: CookbookPermission,
  retry = true,
): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  headers.set('X-Correlation-Id', crypto.randomUUID())
  const user = { access_token: await getApiAccessToken('cookbook', permission, !retry) }
  headers.set('Authorization', `Bearer ${user.access_token}`)

  const response = await fetch(new URL(path, baseUrl), { ...init, headers })
  if (response.status === 401 && retry) {
    return request<T>(path, init, permission, false)
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as { detail?: unknown; error?: unknown } | undefined
    const message = typeof problem?.detail === 'string' ? problem.detail : typeof problem?.error === 'string' ? problem.error : `The request failed (${response.status}).`
    throw new CookbookApiError(message, response.status)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const listFolders = () => request<Folder[]>('/api/v1/folders', {}, 'cookbook.read')
export const createFolder = (name: string) => request<Folder>('/api/v1/folders', { method: 'POST', body: JSON.stringify({ name }) }, 'cookbook.write')
export const deleteFolder = (folderId: string) => request<void>(`/api/v1/folders/${encodeURIComponent(folderId)}`, { method: 'DELETE' }, 'cookbook.write')
export const listSavedRecipes = (folderId: string) => request<SavedRecipe[]>(`/api/v1/folders/${encodeURIComponent(folderId)}/saved-recipes`, {}, 'cookbook.read')
export const saveRecipe = (folderId: string, recipeId: string, recipeVersion: number) => request<SavedRecipe>(`/api/v1/folders/${encodeURIComponent(folderId)}/saved-recipes`, { method: 'POST', body: JSON.stringify({ recipeId, recipeVersion }) }, 'cookbook.write')
export const removeSavedRecipe = (folderId: string, recipeId: string, recipeVersion: number) => request<void>(`/api/v1/folders/${encodeURIComponent(folderId)}/saved-recipes/${encodeURIComponent(recipeId)}/${recipeVersion}`, { method: 'DELETE' }, 'cookbook.write')
