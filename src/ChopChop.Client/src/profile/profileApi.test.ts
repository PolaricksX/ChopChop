import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getApiAccessToken } from '../auth'
import {
  createProfile,
  getMyProfile,
  getProfilePicture,
  uploadProfilePicture,
} from './profileApi'

vi.mock('../auth', () => ({
  getApiAccessToken: vi.fn(),
}))

describe('profileApi', () => {
  beforeEach(() => {
    vi.mocked(getApiAccessToken).mockReset().mockResolvedValue('access-token')
    vi.stubGlobal('fetch', vi.fn())
  })

  it('loads the caller profile with users.read and bearer authorization', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({
      id: 'a2d7e159-b7f6-493e-a070-9f7f75f8c74e',
      displayName: 'Taylor Cook',
      username: 'tcook',
      email: 'taylor@example.com',
    })))

    await expect(getMyProfile()).resolves.toMatchObject({
      displayName: 'Taylor Cook',
      username: 'tcook',
      email: 'taylor@example.com',
    })

    expect(getApiAccessToken).toHaveBeenCalledWith('user', 'users.read')
    const [url, init] = vi.mocked(fetch).mock.calls[0] as [URL, RequestInit]
    expect(new URL(url).pathname).toBe('/api/v1/users/me')
    expect(String(url)).not.toContain('access-token')
    expect(String(url).toLowerCase()).not.toContain('access_token')
    expect(new Headers(init.headers).get('Authorization')).toBe('Bearer access-token')
  })

  it('returns null for missing profile and missing profile picture', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))

    await expect(getMyProfile()).resolves.toBeNull()
    await expect(getProfilePicture('a2d7e159-b7f6-493e-a070-9f7f75f8c74e')).resolves.toBeNull()
  })

  it('uploads multipart profile picture with a file field', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }))
    const file = new File(['image'], 'avatar.png', { type: 'image/png' })

    await uploadProfilePicture('a2d7e159-b7f6-493e-a070-9f7f75f8c74e', file)

    const [url, init] = vi.mocked(fetch).mock.calls[0] as [URL, RequestInit]
    expect(new URL(url).pathname).toBe('/api/v1/users/a2d7e159-b7f6-493e-a070-9f7f75f8c74e/profile-picture')
    expect(init.method).toBe('PUT')
    expect(init.body).toBeInstanceOf(FormData)
    const formData = init.body as FormData
    expect(formData.get('file')).toBe(file)
    expect(getApiAccessToken).toHaveBeenCalledWith('user', 'users.write')
  })

  it('surfaces problem detail for upload validation/content errors', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(
      JSON.stringify({ detail: 'Only JPEG and PNG are allowed.' }),
      { status: 415, headers: { 'Content-Type': 'application/problem+json' } },
    ))

    await expect(uploadProfilePicture('a2d7e159-b7f6-493e-a070-9f7f75f8c74e', new File(['x'], 'x.gif', { type: 'image/gif' })))
      .rejects.toMatchObject({ status: 415, message: 'Only JPEG and PNG are allowed.' })
  })

  it('creates a profile with users.write and sends an Idempotency-Key header', async () => {
      const randomUuid = vi.spyOn(crypto, 'randomUUID').mockReturnValueOnce('00000000-0000-0000-0000-000000000001')
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({
      id: 'a2d7e159-b7f6-493e-a070-9f7f75f8c74e',
      displayName: 'Taylor Cook',
      username: 'tcook',
      email: 'taylor@example.com',
    }), { status: 201 }))

    await createProfile({
      username: 'tcook',
      email: 'taylor@example.com',
      displayName: 'Taylor Cook',
    })

    expect(getApiAccessToken).toHaveBeenCalledWith('user', 'users.write')
    const [url, init] = vi.mocked(fetch).mock.calls[0] as [URL, RequestInit]
    expect(new URL(url).pathname).toBe('/api/v1/users')
    expect(init.method).toBe('POST')
    const headers = new Headers(init.headers)
      expect(headers.get('Idempotency-Key')).toBe('00000000-0000-0000-0000-000000000001')
    randomUuid.mockRestore()
  })
})
