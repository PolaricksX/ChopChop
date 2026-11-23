import type { PantryPermission } from '../auth'

export const pantryTypes = ['Meat', 'Grains', 'Fruits', 'Vegetables', 'Spices'] as const
export const pantryUnits = [
  'Count',
  'Gram',
  'Kilogram',
  'Milliliter',
  'Liter',
  'Ounce',
  'Pound',
  'Cup',
  'Tablespoon',
  'Teaspoon',
] as const
export type PantryType = (typeof pantryTypes)[number] | 'Custom' | (string & {})
export type PantryUnit = (typeof pantryUnits)[number]
export type PantryAvailability = 'Available' | 'Consumed' | 'Unavailable'

export interface PantryItem {
  id: string
  name: string
  type?: PantryType
  customCategory?: string
  notes?: string
  quantity: number
  unit: PantryUnit
  expiresAt?: string
  availability?: PantryAvailability
  state?: PantryAvailability
  version?: string
}

export interface PantryFilters {
  search?: string
  type?: string
  availability?: PantryAvailability | ''
  expiresBefore?: string
  page: number
  pageSize: number
}

export interface PantryItemRequest {
  name: string
  type: string
  customCategory?: string
  quantity: number
  unit: PantryUnit
  expiresAt?: string
  notes?: string
}

export type GetPantryAccessToken = (permission: PantryPermission) => Promise<string>

const baseUrl = import.meta.env.VITE_PANTRY_API_URL ?? 'http://localhost:5131'

async function request<T>(
  getAccessToken: GetPantryAccessToken,
  permission: PantryPermission,
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${await getAccessToken(permission)}`)
  headers.set('Accept', 'application/json')
  headers.set('X-Correlation-Id', crypto.randomUUID())
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')

  const response = await fetch(new URL(path, baseUrl), { ...init, headers })
  if (!response.ok) {
    let message = `Pantry request failed (${response.status}).`
    try {
      const problem: unknown = await response.json()
      if (typeof problem === 'object' && problem !== null && 'detail' in problem &&
        typeof problem.detail === 'string') {
        message = problem.detail
      }
    } catch {
      // The API may return a non-JSON error response.
    }
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

function requestBody(item: PantryItemRequest) {
  const expiresAt = item.expiresAt && /^\d{4}-\d{2}-\d{2}$/.test(item.expiresAt)
    ? `${item.expiresAt}T00:00:00Z`
    : item.expiresAt || null
  return JSON.stringify({ ...item, expiresAt })
}

export function listPantry(getAccessToken: GetPantryAccessToken, filters: PantryFilters) {
  const query = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  })
  if (filters.search?.trim()) query.set('search', filters.search.trim())
  if (filters.type) query.set('type', filters.type)
  if (filters.availability) query.set('availability', filters.availability)
  if (filters.expiresBefore) query.set('expiresBefore', filters.expiresBefore)

  return request<PantryItem[]>(getAccessToken, 'pantry.read', `/api/v1/pantry?${query}`)
}

export function createPantryItem(getAccessToken: GetPantryAccessToken, item: PantryItemRequest) {
  return request<PantryItem>(getAccessToken, 'pantry.write', '/api/v1/pantry', {
    method: 'POST',
    body: requestBody(item),
  })
}

export function updatePantryItem(
  getAccessToken: GetPantryAccessToken,
  itemId: string,
  item: PantryItemRequest,
  version?: string,
) {
  return request<void>(getAccessToken, 'pantry.write', `/api/v1/pantry/${encodeURIComponent(itemId)}`, {
    method: 'PUT',
    headers: version ? { 'If-Match': version } : undefined,
    body: requestBody(item),
  })
}

export function setPantryItemAvailability(
  getAccessToken: GetPantryAccessToken,
  itemId: string,
  action: 'consume' | 'unavailable',
  version?: string,
) {
  return request<void>(
    getAccessToken,
    'pantry.write',
    `/api/v1/pantry/${encodeURIComponent(itemId)}/${action}`,
    {
      method: 'POST',
      headers: version ? { 'If-Match': version } : undefined,
    },
  )
}

export function deletePantryItem(getAccessToken: GetPantryAccessToken, itemId: string) {
  return request<void>(
    getAccessToken,
    'pantry.write',
    `/api/v1/pantry/${encodeURIComponent(itemId)}`,
    { method: 'DELETE' },
  )
}
