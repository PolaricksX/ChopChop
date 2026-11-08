import type {
  MealDraft,
  MealEntry,
  MealEntryPage,
  MealEntryStatus,
  MealPlan,
  MealPlanPage,
  MealPlanStatus,
  PlanDraft,
} from './types'

interface ProblemDetails {
  title?: string
  detail?: string
  correlationId?: string
  errors?: Record<string, string[]>
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readFieldErrors(value: unknown): Record<string, string[]> | undefined {
  if (!isRecord(value)) {
    return undefined
  }

  const errors: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages) && messages.every((message) => typeof message === 'string')) {
      errors[field] = messages
    }
  }
  return Object.keys(errors).length > 0 ? errors : undefined
}

export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly errors: Record<string, string[]>
  readonly correlationId?: string
  readonly retryAfter?: string

  constructor(
    status: number,
    problem: ProblemDetails,
    correlationId?: string,
    retryAfter?: string,
  ) {
    super(problem.detail || problem.title || `The API returned HTTP ${status}.`)
    this.name = 'ApiError'
    this.status = status
    this.title = problem.title || `Request failed (${status})`
    this.errors = problem.errors || {}
    this.correlationId = problem.correlationId || correlationId
    this.retryAfter = retryAfter
  }
}

export type MealPlannerPermission = 'mealplans.read' | 'mealplans.write'
export type MealPlannerAccessTokenProvider = (
  permission: MealPlannerPermission,
  forceRefresh?: boolean,
) => Promise<string>

const apiRoot = `${(import.meta.env.VITE_MEAL_PLANNER_API_URL || 'http://localhost:5033').replace(/\/+$/, '')}/api/v1/meal-plans`

async function send(
  path: string,
  getAccessToken: MealPlannerAccessTokenProvider,
  permission: MealPlannerPermission,
  init: RequestInit = {},
): Promise<Response> {
  const accessToken = await getAccessToken(permission)
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json, application/problem+json')
  headers.set('Authorization', `Bearer ${accessToken}`)
  headers.set('X-Correlation-Id', crypto.randomUUID())

  if (init.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${apiRoot}${path}`, {
    ...init,
    headers,
    credentials: 'omit',
    cache: 'no-store',
  })
  if (response.status === 401) {
    const requestHeaders = new Headers(headers)
    requestHeaders.set('Authorization', `Bearer ${await getAccessToken(permission, true)}`)
    const refreshedResponse = await fetch(`${apiRoot}${path}`, {
      ...init,
      headers: requestHeaders,
      credentials: 'omit',
      cache: 'no-store',
    })
    return handleResponse(refreshedResponse)
  }

  return handleResponse(response)
}

async function handleResponse(response: Response): Promise<Response> {
  if (!response.ok) {
    const raw = await response.text()
    let problem: ProblemDetails = {}

    if (raw) {
      try {
        const parsed: unknown = JSON.parse(raw)
        if (isRecord(parsed)) {
          problem = {
            title: typeof parsed.title === 'string' ? parsed.title : undefined,
            detail: typeof parsed.detail === 'string' ? parsed.detail : undefined,
            correlationId:
              typeof parsed.correlationId === 'string' ? parsed.correlationId : undefined,
            errors: readFieldErrors(parsed.errors),
          }
        }
      } catch {
        problem = {
          title: 'The service returned an unreadable error response.',
        }
      }
    }

    throw new ApiError(
      response.status,
      problem,
      response.headers.get('X-Correlation-Id') || undefined,
      response.headers.get('Retry-After') || undefined,
    )
  }

  return response
}

async function sendJson<T>(
  path: string,
  getAccessToken: MealPlannerAccessTokenProvider,
  permission: MealPlannerPermission,
  init: RequestInit = {},
): Promise<T> {
  const response = await send(path, getAccessToken, permission, init)
  try {
    return (await response.json()) as T
  } catch {
    throw new ApiError(response.status, {
      title: 'The service returned an unreadable success response.',
    })
  }
}

function jsonBody(method: NonNullable<RequestInit['method']>, value: unknown, headers?: HeadersInit): RequestInit {
  return {
    method,
    headers,
    body: JSON.stringify(value),
  }
}

function encodedId(id: string): string {
  return encodeURIComponent(id)
}

export const mealPlanApi = {
  listPlans(getAccessToken: MealPlannerAccessTokenProvider, page: number, pageSize: number): Promise<MealPlanPage> {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    return sendJson(`?${query}`, getAccessToken, 'mealplans.read')
  },

  getPlan(getAccessToken: MealPlannerAccessTokenProvider, planId: string): Promise<MealPlan> {
    return sendJson(`/${encodedId(planId)}`, getAccessToken, 'mealplans.read')
  },

  createPlan(getAccessToken: MealPlannerAccessTokenProvider, draft: PlanDraft): Promise<MealPlan> {
    return sendJson(
      '',
      getAccessToken,
      'mealplans.write',
      jsonBody('POST', draft, { 'Idempotency-Key': crypto.randomUUID() }),
    )
  },

  updatePlan(
    getAccessToken: MealPlannerAccessTokenProvider,
    planId: string,
    draft: PlanDraft,
    version: string,
  ): Promise<MealPlan> {
    return sendJson(
      `/${encodedId(planId)}`,
      getAccessToken,
      'mealplans.write',
      jsonBody('PUT', { ...draft, version }),
    )
  },

  changePlanStatus(
    getAccessToken: MealPlannerAccessTokenProvider,
    planId: string,
    status: MealPlanStatus,
    version: string,
  ): Promise<MealPlan> {
    return sendJson(
      `/${encodedId(planId)}/status`,
      getAccessToken,
      'mealplans.write',
      jsonBody('PATCH', { status, version }),
    )
  },

  async deletePlan(getAccessToken: MealPlannerAccessTokenProvider, planId: string, version: string): Promise<void> {
    await send(`/${encodedId(planId)}`, getAccessToken, 'mealplans.write', {
      method: 'DELETE',
      headers: { 'If-Match': `"${version}"` },
    })
  },

  listMeals(
    getAccessToken: MealPlannerAccessTokenProvider,
    planId: string,
    page: number,
    pageSize: number,
    date?: string,
  ): Promise<MealEntryPage> {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (date) {
      query.set('date', date)
    }
    return sendJson(`/${encodedId(planId)}/meals?${query}`, getAccessToken, 'mealplans.read')
  },

  createMeal(getAccessToken: MealPlannerAccessTokenProvider, plan: MealPlan, draft: MealDraft): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals`,
      getAccessToken,
      'mealplans.write',
      jsonBody(
        'POST',
        {
          ...draft,
          recipeId: draft.recipeId || null,
          version: plan.version,
        },
        { 'Idempotency-Key': crypto.randomUUID() },
      ),
    )
  },

  updateMeal(
    getAccessToken: MealPlannerAccessTokenProvider,
    plan: MealPlan,
    mealId: string,
    draft: MealDraft,
  ): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}`,
      getAccessToken,
      'mealplans.write',
      jsonBody('PUT', {
        ...draft,
        recipeId: draft.recipeId || null,
        version: plan.version,
      }),
    )
  },

  changeMealStatus(
    getAccessToken: MealPlannerAccessTokenProvider,
    plan: MealPlan,
    mealId: string,
    status: MealEntryStatus,
  ): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}/status`,
      getAccessToken,
      'mealplans.write',
      jsonBody('PATCH', { status, version: plan.version }),
    )
  },

  async deleteMeal(
    getAccessToken: MealPlannerAccessTokenProvider,
    plan: MealPlan,
    mealId: string,
  ): Promise<void> {
    await send(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}`,
      getAccessToken,
      'mealplans.write',
      {
        method: 'DELETE',
        headers: { 'If-Match': `"${plan.version}"` },
      },
    )
  },
}
