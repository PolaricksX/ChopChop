import type {
  Equipment,
  EquipmentFilters,
  EquipmentPage,
  EquipmentUpsertRequest,
} from './types'

export class EquipmentApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'EquipmentApiError'
    this.status = status
  }
}

const baseUrl = import.meta.env.VITE_EQUIPMENT_API_URL ?? 'http://localhost:5018'

async function request<T>(
  accessToken: string,
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('X-Correlation-Id', crypto.randomUUID())
  if (init.body !== undefined) headers.set('Content-Type', 'application/json')

  const response = await fetch(new URL(path, baseUrl), { ...init, headers })
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as {
      detail?: unknown
      title?: unknown
      errors?: Record<string, unknown>
    } | undefined
    const validationMessages = Object.values(problem?.errors ?? {})
      .flatMap((value) => Array.isArray(value) ? value : [])
      .filter((value): value is string => typeof value === 'string')
    const message = typeof problem?.detail === 'string'
      ? problem.detail
      : validationMessages.length > 0
        ? validationMessages.join(' ')
        : typeof problem?.title === 'string'
          ? problem.title
          : `The request failed (${response.status}).`
    throw new EquipmentApiError(message, response.status)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function listEquipment(accessToken: string, filters: EquipmentFilters) {
  const query = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  })
  if (filters.search?.trim()) query.set('search', filters.search.trim())
  if (filters.type) query.set('type', filters.type)
  if (filters.condition) query.set('condition', filters.condition)
  if (filters.availability) query.set('availability', filters.availability)

  return request<EquipmentPage>(accessToken, `/api/v1/equipment?${query}`)
}

export function createEquipment(
  accessToken: string,
  equipment: EquipmentUpsertRequest,
  idempotencyKey: string = crypto.randomUUID(),
) {
  return request<Equipment>(accessToken, '/api/v1/equipment', {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify(equipment),
  })
}

export function updateEquipment(
  accessToken: string,
  equipmentId: string,
  version: string,
  equipment: EquipmentUpsertRequest,
) {
  return request<Equipment>(
    accessToken,
    `/api/v1/equipment/${encodeURIComponent(equipmentId)}`,
    {
      method: 'PUT',
      headers: { 'If-Match': `"${version}"` },
      body: JSON.stringify(equipment),
    },
  )
}

export function setEquipmentAvailability(
  accessToken: string,
  equipmentId: string,
  version: string,
  isAvailable: boolean,
) {
  return request<Equipment>(
    accessToken,
    `/api/v1/equipment/${encodeURIComponent(equipmentId)}/availability`,
    {
      method: 'PUT',
      headers: { 'If-Match': `"${version}"` },
      body: JSON.stringify({ isAvailable }),
    },
  )
}

export function deleteEquipment(accessToken: string, equipment: Equipment) {
  return request<void>(
    accessToken,
    `/api/v1/equipment/${encodeURIComponent(equipment.id)}`,
    {
      method: 'DELETE',
      headers: { 'If-Match': `"${equipment.version}"` },
    },
  )
}
