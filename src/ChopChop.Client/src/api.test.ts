import { afterEach, describe, expect, it, vi } from 'vitest'
import { createGeneration, getGeneration } from './api'

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('API client', () => {
  it('sends bearer auth, a fresh idempotency key, and user-selected pantry data', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 202,
      json: async () => ({ id: 'request-id', status: 'Queued' }),
    })
    vi.stubGlobal('fetch', fetchMock)

    await createGeneration('access-token', {
      prompt: 'A quick dinner',
      includePantry: true,
      pantryItems: ['beans'],
    })

    const [, init] = fetchMock.mock.calls[0] as [URL, RequestInit]
    const headers = new Headers(init.headers)
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('Idempotency-Key')).toBeTruthy()
    expect(JSON.parse(String(init.body))).toEqual({
      prompt: 'A quick dinner',
      includePantry: true,
      pantryItems: ['beans'],
    })
  })

  it('surfaces RFC problem details rather than hiding API authorization errors', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({ detail: 'Moderator role required.' }),
    }))

    await expect(getGeneration('token', 'request-id'))
      .rejects.toMatchObject({ message: 'Moderator role required.', status: 403 })
  })
})
