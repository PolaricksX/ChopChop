import { getApiAccessToken } from '../auth'

export interface UserProfile {
  id: string
  displayName: string
  username: string
  email: string
}

export class ProfileApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ProfileApiError'
    this.status = status
  }
}

const baseUrl = import.meta.env.VITE_USER_SERVICE_API_URL ?? 'http://localhost:5199'

type ProfilePermission = 'users.read' | 'users.write'

async function send(path: string, permission: ProfilePermission, init: RequestInit = {}) {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${await getApiAccessToken('user', permission)}`)
  headers.set('X-Correlation-Id', crypto.randomUUID())

  return fetch(new URL(path, baseUrl), {
    ...init,
    headers,
  })
}

async function readError(response: Response) {
  const problem = await response.json().catch(() => undefined) as { detail?: unknown; error?: unknown } | undefined
  return typeof problem?.detail === 'string'
    ? problem.detail
    : typeof problem?.error === 'string'
      ? problem.error
      : `The request failed (${response.status}).`
}

export async function getMyProfile(): Promise<UserProfile | null> {
  const response = await send('/api/v1/users/me', 'users.read')
  if (response.status === 404) return null
  if (!response.ok) {
    throw new ProfileApiError(await readError(response), response.status)
  }
  return response.json() as Promise<UserProfile>
}

export async function getProfilePicture(id: string): Promise<Blob | null> {
  const response = await send(`/api/v1/users/${encodeURIComponent(id)}/profile-picture`, 'users.read')
  if (response.status === 404) return null
  if (!response.ok) {
    throw new ProfileApiError(await readError(response), response.status)
  }
  return response.blob()
}

export async function uploadProfilePicture(id: string, file: File): Promise<void> {
  const formData = new FormData()
  formData.set('file', file)
  const response = await send(`/api/v1/users/${encodeURIComponent(id)}/profile-picture`, 'users.write', {
    method: 'PUT',
    body: formData,
  })
  if (!response.ok) {
    throw new ProfileApiError(await readError(response), response.status)
  }
}
