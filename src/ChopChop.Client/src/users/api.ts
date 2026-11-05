import { request } from '../api'
import type { UserProfile, UserProfileInput } from './types'

const userServiceApi = import.meta.env.VITE_USER_SERVICE_API_URL ?? 'http://localhost:5199'

export function createUserProfile(
  accessToken: string,
  body: UserProfileInput,
  idempotencyKey: string,
) {
  return request<UserProfile>(userServiceApi, '/api/v1/users/', accessToken, {
    method: 'POST',
    headers: { 'Idempotency-Key': idempotencyKey },
    body: JSON.stringify(body),
  })
}

export function getUserProfile(accessToken: string, id: string) {
  return request<UserProfile>(
    userServiceApi,
    `/api/v1/users/${encodeURIComponent(id)}`,
    accessToken,
  )
}

export function updateUserProfile(
  accessToken: string,
  id: string,
  body: UserProfileInput,
  version: string,
) {
  return request<UserProfile>(
    userServiceApi,
    `/api/v1/users/${encodeURIComponent(id)}`,
    accessToken,
    { method: 'PUT', body: JSON.stringify({ ...body, version }) },
  )
}

export function deleteUserProfile(accessToken: string, id: string) {
  return request<void>(
    userServiceApi,
    `/api/v1/users/${encodeURIComponent(id)}`,
    accessToken,
    { method: 'DELETE' },
  )
}
