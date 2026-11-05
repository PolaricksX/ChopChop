import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { MealEntry, MealPlan } from './types'
import { MealPlannerDashboard } from './App'

const firstPlan: MealPlan = {
  id: 'plan-1',
  name: 'Week of good food',
  startDate: '2026-11-01',
  endDate: '2026-11-07',
  status: 'Draft',
  createdAt: '2026-10-31T12:00:00Z',
  updatedAt: '2026-10-31T12:00:00Z',
  version: 'version-one',
}

const secondPlan: MealPlan = {
  ...firstPlan,
  id: 'plan-2',
  name: 'Next week',
}

const meal: MealEntry = {
  id: 'meal-1',
  mealPlanId: firstPlan.id,
  date: '2026-11-02',
  slot: 'Breakfast',
  title: 'Overnight oats',
  recipeId: null,
  servings: 2,
  status: 'Planned',
  createdAt: firstPlan.createdAt,
  updatedAt: firstPlan.updatedAt,
  version: firstPlan.version,
}

const fetchMock = vi.fn<typeof fetch>()
const sessionExpired = vi.fn<() => Promise<void>>().mockResolvedValue(undefined)
const signOut = vi.fn<() => Promise<void>>().mockResolvedValue(undefined)

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function page<T>(items: T[], totalCount = items.length, currentPage = 1) {
  return { items, page: currentPage, pageSize: 20, totalCount }
}

function renderDashboard() {
  return render(
    <MealPlannerDashboard
      accessToken="access-token"
      displayName="Sean"
      onSessionExpired={sessionExpired}
      onSignOut={signOut}
    />,
  )
}

describe('meal planner dashboard', () => {
  beforeEach(() => {
    fetchMock.mockReset()
    sessionExpired.mockClear()
    signOut.mockClear()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads plans and meals using the API pagination contract', async () => {
    fetchMock.mockImplementation(async (input) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page(url.searchParams.get('page') === '2' ? [secondPlan] : [firstPlan], 22, Number(url.searchParams.get('page'))))
      }
      if (url.pathname === `/api/v1/meal-plans/${firstPlan.id}`) {
        return jsonResponse(firstPlan)
      }
      if (url.pathname.endsWith('/meals')) {
        return jsonResponse(page(url.searchParams.get('page') === '2' ? [] : [meal], 22, Number(url.searchParams.get('page'))))
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()

    expect(await screen.findByRole('heading', { name: firstPlan.name })).toBeInTheDocument()
    expect(await screen.findByText(meal.title)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Next plans page' }))
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([input]) => String(input).includes('page=2&pageSize=20'))).toBe(true)
    })
    expect(await screen.findByRole('button', { name: /Next week/ })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Next meals page' }))
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([input]) =>
        String(input).includes(`/meals?page=2&pageSize=20`),
      )).toBe(true)
    })
  })

  it('creates a plan with validated dates and no client-supplied owner', async () => {
    let createdRequest: Record<string, unknown> | null = null
    let hasCreatedPlan = false
    const createdPlan = { ...firstPlan, id: 'plan-created', name: 'Family week' }
    fetchMock.mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans' && init?.method === 'POST') {
        createdRequest = JSON.parse(String(init.body)) as Record<string, unknown>
        hasCreatedPlan = true
        return jsonResponse(createdPlan, 201)
      }
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page(hasCreatedPlan ? [createdPlan] : []))
      }
      if (url.pathname === `/api/v1/meal-plans/${createdPlan.id}`) {
        return jsonResponse(createdPlan)
      }
      if (url.pathname.endsWith('/meals')) {
        return jsonResponse(page([]))
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()
    await screen.findByText('Start with a plan')
    await user.click(screen.getByRole('button', { name: /New plan/ }))
    await user.type(screen.getByLabelText(/Plan name/), 'Family week')
    await user.click(screen.getByRole('button', { name: 'Create plan' }))

    expect(await screen.findByRole('heading', { name: 'Family week' })).toBeInTheDocument()
    await waitFor(() => expect(createdRequest).not.toBeNull())
    expect(createdRequest).toMatchObject({
      name: 'Family week',
      startDate: expect.any(String),
      endDate: expect.any(String),
    })
    expect(createdRequest).not.toHaveProperty('ownerId')
    expect(fetchMock.mock.calls.some(([, init]) => new Headers(init?.headers).has('Idempotency-Key'))).toBe(true)
  })

  it('shows client-side range validation without sending an invalid plan', async () => {
    fetchMock.mockImplementation(async (input) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page([]))
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()
    await screen.findByText('Start with a plan')
    await user.click(screen.getByRole('button', { name: /New plan/ }))
    await user.type(screen.getByLabelText(/Plan name/), 'Long range')
    fireEvent.change(screen.getByLabelText(/End date/), { target: { value: '2028-12-31' } })
    await user.click(screen.getByRole('button', { name: 'Create plan' }))

    expect(await screen.findByText('A plan can cover at most 366 calendar days.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('adds a meal with the current plan version and an idempotency key', async () => {
    let currentPlan = firstPlan
    let createRequest: Record<string, unknown> | null = null
    fetchMock.mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page([currentPlan]))
      }
      if (url.pathname === `/api/v1/meal-plans/${firstPlan.id}`) {
        return jsonResponse(currentPlan)
      }
      if (url.pathname.endsWith('/meals') && init?.method === 'POST') {
        createRequest = JSON.parse(String(init.body)) as Record<string, unknown>
        currentPlan = { ...currentPlan, version: 'version-two' }
        return jsonResponse({
          ...meal,
          title: 'Tomato soup',
          version: currentPlan.version,
        }, 201)
      }
      if (url.pathname.endsWith('/meals')) {
        return jsonResponse(page(currentPlan.version === 'version-two' ? [{ ...meal, title: 'Tomato soup' }] : []))
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()
    await screen.findByRole('heading', { name: firstPlan.name })
    await user.click(screen.getByRole('button', { name: 'Add meal' }))
    await user.type(screen.getByLabelText(/Meal name/), 'Tomato soup')
    await user.click(screen.getAllByRole('button', { name: 'Add meal' }).at(-1)!)

    expect(await screen.findByText('Tomato soup')).toBeInTheDocument()
    expect(createRequest).toMatchObject({
      date: firstPlan.startDate,
      slot: 'Breakfast',
      title: 'Tomato soup',
      recipeId: null,
      servings: 1,
      version: firstPlan.version,
    })
    expect(createRequest).not.toHaveProperty('ownerId')
    expect(fetchMock.mock.calls.some(([, init]) =>
      init?.method === 'POST' && new Headers(init.headers).has('Idempotency-Key'),
    )).toBe(true)
  })

  it('updates meal progress with the current plan version', async () => {
    let currentPlan = firstPlan
    let statusRequest: Record<string, unknown> | null = null
    fetchMock.mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page([currentPlan]))
      }
      if (url.pathname === `/api/v1/meal-plans/${firstPlan.id}` && !init?.method) {
        return jsonResponse(currentPlan)
      }
      if (url.pathname.endsWith(`/meals/${meal.id}/status`)) {
        statusRequest = JSON.parse(String(init?.body)) as Record<string, unknown>
        currentPlan = { ...currentPlan, version: 'version-two' }
        return jsonResponse({ ...meal, status: 'InProgress', version: currentPlan.version })
      }
      if (url.pathname.endsWith('/meals')) {
        return jsonResponse(page([{ ...meal, status: currentPlan.version === 'version-two' ? 'InProgress' : 'Planned' }]))
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()
    await screen.findByText(meal.title)
    await user.click(screen.getByRole('button', { name: `Mark in progress ${meal.title}` }))

    await waitFor(() => expect(statusRequest).not.toBeNull())
    expect(statusRequest).toEqual({ status: 'InProgress', version: firstPlan.version })
    expect(await screen.findByText('In progress')).toBeInTheDocument()
  })

  it('ends the session on 401 and clearly reports authorization and conflict failures', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'Unauthorized' }, 401))
    renderDashboard()

    await waitFor(() => {
      expect(sessionExpired).toHaveBeenCalledOnce()
    })
  })

  it('shows an explicit service-unavailable message instead of sample data', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse({ title: 'Service unavailable' }, 503),
    )
    renderDashboard()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The Meal Planner service is temporarily unavailable.',
    )
    expect(screen.queryByText(meal.title)).not.toBeInTheDocument()
  })

  it('explains network failures without showing placeholder plans', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    renderDashboard()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Could not reach the Meal Planner service.',
    )
    expect(screen.queryByText(meal.title)).not.toBeInTheDocument()
  })

  it('shows rate-limit guidance and allows a deliberate retry', async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ title: 'Too many requests' }), {
          status: 429,
          headers: { 'Content-Type': 'application/problem+json', 'Retry-After': '20' },
        }),
      )
      .mockResolvedValueOnce(jsonResponse(page([])))
    const user = userEvent.setup()
    renderDashboard()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The service is receiving too many requests. Try again after 20.',
    )
    await user.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByText('Start with a plan')
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('explains authorization and conflict errors and offers a refresh action', async () => {
    fetchMock.mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/v1/meal-plans') {
        return jsonResponse(page([firstPlan]))
      }
      if (url.pathname === `/api/v1/meal-plans/${firstPlan.id}` && !init?.method) {
        return jsonResponse(firstPlan)
      }
      if (url.pathname.endsWith('/meals')) {
        return jsonResponse(page([]))
      }
      if (url.pathname === `/api/v1/meal-plans/${firstPlan.id}` && init?.method === 'PUT') {
        return jsonResponse({ title: 'Forbidden' }, 403)
      }
      if (url.pathname.endsWith('/status')) {
        return jsonResponse({ title: 'Conflict' }, 409)
      }
      throw new Error(`Unexpected request: ${url}`)
    })

    const user = userEvent.setup()
    renderDashboard()
    await screen.findByRole('heading', { name: firstPlan.name })
    await user.click(screen.getByRole('button', { name: 'Edit plan' }))
    await user.click(screen.getByRole('button', { name: 'Save changes' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Your account does not have permission to do that.',
    )

    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.click(screen.getByRole('button', { name: /Active/ }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'This plan changed or conflicts with another update.',
    )
    expect(screen.getByRole('button', { name: 'Refresh data' })).toBeInTheDocument()
  })
})
