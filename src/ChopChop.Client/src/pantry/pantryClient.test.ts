import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createPantryItem,
  listPantry,
  setPantryItemAvailability,
  updatePantryItem,
} from './pantryClient'
import type { GetPantryAccessToken } from './pantryClient'

const getAccessToken: GetPantryAccessToken = vi.fn().mockResolvedValue('pantry-token')

describe('Pantry API client', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
    vi.mocked(getAccessToken).mockReset().mockResolvedValue('pantry-token')
  })

  it('uses the read permission and encodes pantry filters and paging', async () => {
    vi.stubEnv('VITE_PANTRY_API_URL', 'https://pantry.example')
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify([])))
    vi.stubGlobal('fetch', fetcher)

    await listPantry(getAccessToken, {
      page: 2,
      pageSize: 10,
      search: 'red apple',
      type: 'Fruits',
      availability: 'Available',
      expiresBefore: '2026-12-01',
    })

    expect(getAccessToken).toHaveBeenCalledWith('pantry.read')
    expect(fetcher.mock.calls[0][0].toString()).toContain('search=red+apple')
    expect(fetcher.mock.calls[0][0].toString()).toContain('page=2')
    expect(fetcher.mock.calls[0][1].headers.get('Authorization')).toBeTruthy()
  })

  it('uses write permission and sends date-only expiration as an ISO timestamp', async () => {
    const fetcher = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'item-1' }), { status: 201 }))
    vi.stubGlobal('fetch', fetcher)

    await createPantryItem(getAccessToken, {
      name: 'Rice',
      type: 'Grains',
      quantity: 2,
      unit: 'Cup',
      expiresAt: '2026-12-01',
    })

    expect(getAccessToken).toHaveBeenCalledWith('pantry.write')
    expect(JSON.parse(fetcher.mock.calls[0][1].body)).toMatchObject({
      type: 'Grains',
      unit: 'Cup',
      expiresAt: '2026-12-01T00:00:00Z',
    })
  })

  it('sends the current version for update and availability changes', async () => {
    const fetcher = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetcher)

    await updatePantryItem(getAccessToken, 'item/1', {
      name: 'Rice',
      type: 'Grains',
      quantity: 2,
      unit: 'Cup',
    }, 'v1')
    await setPantryItemAvailability(getAccessToken, 'item/1', 'consume', 'v2')

    expect(fetcher.mock.calls[0][0].toString()).toContain('/api/v1/pantry/item%2F1')
    expect(fetcher.mock.calls[0][1].headers.get('If-Match')).toBe('v1')
    expect(fetcher.mock.calls[1][1].headers.get('If-Match')).toBe('v2')
    expect(getAccessToken).toHaveBeenCalledTimes(2)
    expect(getAccessToken).toHaveBeenNthCalledWith(2, 'pantry.write')
  })
})
