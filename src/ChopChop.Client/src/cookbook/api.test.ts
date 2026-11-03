import { beforeEach, describe, expect, it, vi } from 'vitest'
import { userManager } from '../auth'
import { createFolder, deleteFolder, listFolders } from './api'

vi.mock('../auth', () => ({
  userManager: { getUser: vi.fn(), signinSilent: vi.fn() },
}))

describe('Cookbook API', () => {
  beforeEach(() => {
    vi.mocked(userManager.getUser).mockResolvedValue({ access_token: 'token' } as never)
    vi.mocked(userManager.signinSilent).mockResolvedValue({ access_token: 'renewed' } as never)
    vi.stubGlobal('fetch', vi.fn())
  })

  it('uses the current token and sends no idempotency key', async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify([]), { status: 200 }))
    await listFolders()
    const request = vi.mocked(fetch).mock.calls[0][1] as RequestInit
    expect((request.headers as Headers).get('Authorization')).toBe('Bearer token')
    expect((request.headers as Headers).has('Idempotency-Key')).toBe(false)
  })

  it('renews once and retries a 401', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 }))
    await listFolders()
    expect(userManager.signinSilent).toHaveBeenCalledOnce()
    expect(fetch).toHaveBeenCalledTimes(2)
  })

  it('parses folder mutations with the public DTO shape', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(JSON.stringify({ id: 'folder', name: 'Dinner', createdAtUtc: '2026-01-01T00:00:00Z' }), { status: 201 }))
    await expect(createFolder('Dinner')).resolves.toMatchObject({ name: 'Dinner' })
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }))
    await expect(deleteFolder('folder')).resolves.toBeUndefined()
  })
})
