import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api'
import {
  createUserProfile,
  deleteUserProfile,
  getUserProfile,
  updateUserProfile,
} from './api'
import type { UserProfile } from './types'

const profile: UserProfile = {
  id: 'profile-id',
  username: 'alice',
  email: 'alice@example.com',
  displayName: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  version: 'version-one',
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('User Service API client', () => {
  it('creates profiles with bearer auth and an idempotency key, without ownership fields', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 201,
      json: async () => profile,
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('crypto', { randomUUID: () => 'correlation-id' })

    await createUserProfile('access-token', {
      username: 'alice',
      email: 'alice@example.com',
      displayName: null,
    }, 'stable-idempotency-key')

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit]
    const headers = new Headers(init.headers)
    expect(url.toString()).toBe('http://localhost:5199/api/v1/users/')
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('X-Correlation-Id')).toBe('correlation-id')
    expect(headers.get('Idempotency-Key')).toBe('stable-idempotency-key')
    expect(JSON.parse(String(init.body))).toEqual({
      username: 'alice',
      email: 'alice@example.com',
      displayName: null,
    })
  })

  it('gets and updates by the returned ID using the latest version', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => profile })
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ ...profile, version: 'version-two' }) })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('crypto', { randomUUID: () => 'correlation-id' })

    await getUserProfile('token', 'profile-id')
    await updateUserProfile('token', 'profile-id', {
      username: 'alice-new',
      email: 'alice@example.com',
      displayName: null,
    }, 'version-one')

    const [getUrl, getInit] = fetchMock.mock.calls[0] as [URL, RequestInit]
    const [updateUrl, updateInit] = fetchMock.mock.calls[1] as [URL, RequestInit]
    expect(getUrl.toString()).toBe('http://localhost:5199/api/v1/users/profile-id')
    expect(getInit.method).toBeUndefined()
    expect(updateUrl.toString()).toBe(getUrl.toString())
    expect(updateInit.method).toBe('PUT')
    expect(JSON.parse(String(updateInit.body))).toEqual({
      username: 'alice-new',
      email: 'alice@example.com',
      displayName: null,
      version: 'version-one',
    })
  })

  it('deletes the caller profile without sending ownership data', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 204 })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('crypto', { randomUUID: () => 'correlation-id' })

    await deleteUserProfile('token', 'profile-id')

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit]
    expect(url.toString()).toBe('http://localhost:5199/api/v1/users/profile-id')
    expect(init.method).toBe('DELETE')
    expect(init.body).toBeUndefined()
  })

  it('preserves validation errors and correlation IDs from problem details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      headers: new Headers(),
      text: async () => JSON.stringify({
        detail: 'Validation failed.',
        errors: { username: ['Username is required.'] },
        correlationId: 'problem-correlation',
      }),
    }))
    vi.stubGlobal('crypto', { randomUUID: () => 'request-correlation' })

    await expect(getUserProfile('token', 'profile-id')).rejects.toMatchObject({
      name: 'ApiError',
      status: 400,
      validationErrors: { username: ['Username is required.'] },
      correlationId: 'problem-correlation',
    } satisfies Partial<ApiError>)
  })

  it('uses the response correlation header if problem details omit it', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 503,
      headers: new Headers({ 'X-Correlation-Id': 'response-correlation' }),
      text: async () => '',
    }))
    vi.stubGlobal('crypto', { randomUUID: () => 'request-correlation' })

    await expect(getUserProfile('token', 'profile-id'))
      .rejects.toMatchObject({ status: 503, correlationId: 'response-correlation' })
  })
})
