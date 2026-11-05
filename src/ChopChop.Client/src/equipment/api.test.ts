import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  createEquipment,
  deleteEquipment,
  listEquipment,
  setEquipmentAvailability,
  updateEquipment,
} from './api'
import type { Equipment, EquipmentUpsertRequest } from './types'

afterEach(() => {
  vi.unstubAllGlobals()
})

const equipment: Equipment = {
  id: 'equipment-id',
  name: 'Saucepan',
  type: 'Cookware',
  quantity: 1,
  brand: null,
  size: null,
  notes: null,
  condition: 'Good',
  location: null,
  availability: 'Available',
  createdAtUtc: '2026-11-04T12:00:00Z',
  updatedAtUtc: '2026-11-04T12:00:00Z',
  version: 'AQIDBAUGBwg=',
}

const body: EquipmentUpsertRequest = {
  name: 'Saucepan',
  type: 'Cookware',
  quantity: 1,
  condition: 'Good',
  brand: null,
  size: null,
  notes: null,
  location: null,
}

describe('Equipment API client', () => {
  it('sends authenticated list filters as query parameters', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({
      items: [equipment],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    })))
    vi.stubGlobal('fetch', fetchMock)

    await listEquipment('access-token', {
      page: 2,
      pageSize: 20,
      search: '  pan  ',
      type: 'Cookware',
      condition: 'Good',
      availability: 'Available',
    })

    const [url, init] = fetchMock.mock.calls[0] as [URL, RequestInit]
    const requestUrl = new URL(url)
    const headers = new Headers(init.headers)
    expect(requestUrl.pathname).toBe('/api/v1/equipment')
    expect(requestUrl.searchParams.get('page')).toBe('2')
    expect(requestUrl.searchParams.get('pageSize')).toBe('20')
    expect(requestUrl.searchParams.get('search')).toBe('pan')
    expect(requestUrl.searchParams.get('type')).toBe('Cookware')
    expect(requestUrl.searchParams.get('condition')).toBe('Good')
    expect(requestUrl.searchParams.get('availability')).toBe('Available')
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('X-Correlation-Id')).toBeTruthy()
  })

  it('uses an idempotency key for creates and a strong ETag for updates', async () => {
    const fetchMock = vi.fn().mockImplementation(
      () => Promise.resolve(new Response(JSON.stringify(equipment))),
    )
    vi.stubGlobal('fetch', fetchMock)

    await createEquipment('access-token', body)
    const [, createInit] = fetchMock.mock.calls[0] as [URL, RequestInit]
    expect(new Headers(createInit.headers).get('Idempotency-Key')).toBeTruthy()
    expect(JSON.parse(String(createInit.body))).toEqual(body)

    await updateEquipment('access-token', equipment.id, equipment.version, body)
    const [, updateInit] = fetchMock.mock.calls[1] as [URL, RequestInit]
    expect(new Headers(updateInit.headers).get('If-Match')).toBe('"AQIDBAUGBwg="')
  })

  it('uses conditional mutation headers and handles no-content deletes', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(equipment)))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)

    await setEquipmentAvailability('access-token', equipment.id, equipment.version, false)
    await deleteEquipment('access-token', equipment)

    const [, availabilityInit] = fetchMock.mock.calls[0] as [URL, RequestInit]
    const [, deleteInit] = fetchMock.mock.calls[1] as [URL, RequestInit]
    expect(new Headers(availabilityInit.headers).get('If-Match')).toBe('"AQIDBAUGBwg="')
    expect(JSON.parse(String(availabilityInit.body))).toEqual({ isAvailable: false })
    expect(new Headers(deleteInit.headers).get('If-Match')).toBe('"AQIDBAUGBwg="')
    expect(deleteInit.method).toBe('DELETE')
  })

  it('surfaces API validation details as a typed error', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(
      JSON.stringify({ errors: { name: ['Name is required.'] } }),
      { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
    )))

    await expect(createEquipment('access-token', body))
      .rejects.toMatchObject({ message: 'Name is required.', status: 400 })
  })
})
