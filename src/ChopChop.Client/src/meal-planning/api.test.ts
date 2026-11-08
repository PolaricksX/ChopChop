import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, mealPlanApi } from './api'
import type { MealPlannerAccessTokenProvider } from './api'
import type { MealDraft, MealPlan } from './types'

const plan: MealPlan = {
  id: 'plan-1',
  name: 'Week ahead',
  startDate: '2026-11-01',
  endDate: '2026-11-07',
  status: 'Draft',
  createdAt: '2026-10-31T12:00:00Z',
  updatedAt: '2026-10-31T12:00:00Z',
  version: 'cGxhbi12ZXJzaW9u',
}

function jsonResponse(
  body: unknown,
  status = 200,
  headers: Record<string, string> = {},
): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })
}

describe('mealPlanApi', () => {
  const fetchMock = vi.fn<typeof fetch>()
  const getAccessToken = vi.fn<MealPlannerAccessTokenProvider>()

  beforeEach(() => {
    fetchMock.mockReset()
    getAccessToken.mockReset().mockResolvedValue('access-token')
    vi.stubGlobal('fetch', fetchMock)
    vi.spyOn(crypto, 'randomUUID').mockReturnValue('11111111-1111-4111-8111-111111111111')
  })

  afterEach(() => {
    vi.restoreAllMocks()
    vi.unstubAllGlobals()
  })

  it('creates plans with an idempotency key and never sends client ownership', async () => {
    fetchMock.mockResolvedValue(jsonResponse(plan, 201))

    await mealPlanApi.createPlan(getAccessToken, {
      name: plan.name,
      startDate: plan.startDate,
      endDate: plan.endDate,
    })

    const [url, init] = fetchMock.mock.calls[0]!
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>

    expect(String(url)).toContain('/api/v1/meal-plans')
    expect(init?.method).toBe('POST')
    expect(headers.get('Authorization')).toBe('Bearer access-token')
    expect(headers.get('Idempotency-Key')).toBe('11111111-1111-4111-8111-111111111111')
    expect(headers.get('X-Correlation-Id')).toBe('11111111-1111-4111-8111-111111111111')
    expect(body).toEqual({
      name: plan.name,
      startDate: plan.startDate,
      endDate: plan.endDate,
    })
    expect(body).not.toHaveProperty('ownerId')
    expect(init?.credentials).toBe('omit')
  })

  it('uses the plan version for meal writes and supports optional recipe references', async () => {
    const draft: MealDraft = {
      date: '2026-11-01',
      slot: 'Breakfast',
      title: 'Overnight oats',
      recipeId: '',
      servings: 2,
    }
    fetchMock.mockResolvedValue(
      jsonResponse({
        id: 'meal-1',
        mealPlanId: plan.id,
        ...draft,
        recipeId: null,
        status: 'Planned',
        createdAt: plan.createdAt,
        updatedAt: plan.updatedAt,
        version: 'updated-plan-version',
      }, 201),
    )

    await mealPlanApi.createMeal(getAccessToken, plan, draft)

    const [url, init] = fetchMock.mock.calls[0]!
    const headers = new Headers(init?.headers)
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>

    expect(String(url)).toContain(`/api/v1/meal-plans/${plan.id}/meals`)
    expect(headers.get('Idempotency-Key')).toBe('11111111-1111-4111-8111-111111111111')
    expect(body.version).toBe(plan.version)
    expect(body.recipeId).toBeNull()
    expect(body).not.toHaveProperty('ownerId')
  })

  it('quotes the latest ETag when deleting a plan', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await mealPlanApi.deletePlan(getAccessToken, plan.id, plan.version)

    const [, init] = fetchMock.mock.calls[0]!
    expect(init?.method).toBe('DELETE')
    expect(new Headers(init?.headers).get('If-Match')).toBe(`"${plan.version}"`)
  })

  it('uses the current plan version for plan and meal updates, statuses, and meal deletion', async () => {
    fetchMock.mockImplementation(async (_input, init) => {
      if (init?.method === 'DELETE') {
        return new Response(null, { status: 204 })
      }
      return jsonResponse({
        ...plan,
        version: 'next-version',
      })
    })

    await mealPlanApi.updatePlan(getAccessToken, plan.id, {
      name: plan.name,
      startDate: plan.startDate,
      endDate: plan.endDate,
    }, plan.version)
    await mealPlanApi.changePlanStatus(getAccessToken, plan.id, 'Active', plan.version)
    await mealPlanApi.updateMeal(getAccessToken, plan, 'meal-1', {
      date: '2026-11-02',
      slot: 'Lunch',
      title: 'Soup',
      recipeId: '',
      servings: 3,
    })
    await mealPlanApi.changeMealStatus(getAccessToken, plan, 'meal-1', 'Completed')
    await mealPlanApi.deleteMeal(getAccessToken, plan, 'meal-1')

    const calls = fetchMock.mock.calls
    expect(calls.map(([, init]) => init?.method)).toEqual([
      'PUT',
      'PATCH',
      'PUT',
      'PATCH',
      'DELETE',
    ])
    expect(JSON.parse(String(calls[0]?.[1]?.body))).toMatchObject({ version: plan.version })
    expect(JSON.parse(String(calls[1]?.[1]?.body))).toMatchObject({
      status: 'Active',
      version: plan.version,
    })
    expect(JSON.parse(String(calls[2]?.[1]?.body))).toMatchObject({ version: plan.version })
    expect(JSON.parse(String(calls[3]?.[1]?.body))).toMatchObject({
      status: 'Completed',
      version: plan.version,
    })
    expect(new Headers(calls[4]?.[1]?.headers).get('If-Match')).toBe(`"${plan.version}"`)
  })

  it('preserves API problem details, correlation IDs, and rate-limit guidance', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(
        {
          title: 'Too many requests',
          detail: 'Please retry later.',
          correlationId: 'correlation-123',
          errors: { name: ['Name is required.'] },
        },
        429,
        { 'X-Correlation-Id': 'header-correlation', 'Retry-After': '30' },
      ),
    )

    const request = mealPlanApi.getPlan(getAccessToken, plan.id)
    await expect(request).rejects.toBeInstanceOf(ApiError)
    await expect(request).rejects.toMatchObject({
      status: 429,
      title: 'Too many requests',
      message: 'Please retry later.',
      correlationId: 'correlation-123',
      retryAfter: '30',
      errors: { name: ['Name is required.'] },
    })
  })

  it.each([401, 403, 404, 409, 429, 503])(
    'surfaces HTTP %i without a successful fallback',
    async (status) => {
      fetchMock.mockResolvedValue(
        jsonResponse({ title: `HTTP ${status}`, detail: 'Request was not completed.' }, status),
      )

      await expect(mealPlanApi.getPlan(getAccessToken, plan.id)).rejects.toMatchObject({
        status,
        message: 'Request was not completed.',
      })
    },
  )

  it('requests bounded paginated collections and forwards date-only filters', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ items: [], page: 2, pageSize: 20, totalCount: 0 }),
    )

    await mealPlanApi.listMeals(getAccessToken, plan.id, 2, 20, '2026-11-03')

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain(
      `/api/v1/meal-plans/${plan.id}/meals?page=2&pageSize=20&date=2026-11-03`,
    )
  })

  it('retries unauthorized requests once with a refreshed token and the same scope', async () => {
    getAccessToken
      .mockResolvedValueOnce('expired-token')
      .mockResolvedValueOnce('fresh-token')
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ title: 'Unauthorized' }, 401))
      .mockResolvedValueOnce(jsonResponse(plan))

    await mealPlanApi.getPlan(getAccessToken, plan.id)

    expect(getAccessToken.mock.calls).toEqual([
      ['mealplans.read'],
      ['mealplans.read', true],
    ])
    expect(new Headers(fetchMock.mock.calls[0]?.[1]?.headers).get('Authorization'))
      .toBe('Bearer expired-token')
    expect(new Headers(fetchMock.mock.calls[1]?.[1]?.headers).get('Authorization'))
      .toBe('Bearer fresh-token')
  })
})
