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

const apiRoot = `${(import.meta.env.VITE_API_BASE_URL || 'http://localhost:5033').replace(/\/+$/, '')}/api/v1/meal-plans`

async function send(
  path: string,
  accessToken: string,
  init: RequestInit = {},
): Promise<Response> {
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
  accessToken: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await send(path, accessToken, init)
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
  listPlans(accessToken: string, page: number, pageSize: number): Promise<MealPlanPage> {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    return sendJson(`?${query}`, accessToken)
  },

  getPlan(accessToken: string, planId: string): Promise<MealPlan> {
    return sendJson(`/${encodedId(planId)}`, accessToken)
  },

  createPlan(accessToken: string, draft: PlanDraft): Promise<MealPlan> {
    return sendJson(
      '',
      accessToken,
      jsonBody('POST', draft, { 'Idempotency-Key': crypto.randomUUID() }),
    )
  },

  updatePlan(
    accessToken: string,
    planId: string,
    draft: PlanDraft,
    version: string,
  ): Promise<MealPlan> {
    return sendJson(
      `/${encodedId(planId)}`,
      accessToken,
      jsonBody('PUT', { ...draft, version }),
    )
  },

  changePlanStatus(
    accessToken: string,
    planId: string,
    status: MealPlanStatus,
    version: string,
  ): Promise<MealPlan> {
    return sendJson(
      `/${encodedId(planId)}/status`,
      accessToken,
      jsonBody('PATCH', { status, version }),
    )
  },

  async deletePlan(accessToken: string, planId: string, version: string): Promise<void> {
    await send(`/${encodedId(planId)}`, accessToken, {
      method: 'DELETE',
      headers: { 'If-Match': `"${version}"` },
    })
  },

  listMeals(
    accessToken: string,
    planId: string,
    page: number,
    pageSize: number,
    date?: string,
  ): Promise<MealEntryPage> {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    if (date) {
      query.set('date', date)
    }
    return sendJson(`/${encodedId(planId)}/meals?${query}`, accessToken)
  },

  createMeal(accessToken: string, plan: MealPlan, draft: MealDraft): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals`,
      accessToken,
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
    accessToken: string,
    plan: MealPlan,
    mealId: string,
    draft: MealDraft,
  ): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}`,
      accessToken,
      jsonBody('PUT', {
        ...draft,
        recipeId: draft.recipeId || null,
        version: plan.version,
      }),
    )
  },

  changeMealStatus(
    accessToken: string,
    plan: MealPlan,
    mealId: string,
    status: MealEntryStatus,
  ): Promise<MealEntry> {
    return sendJson(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}/status`,
      accessToken,
      jsonBody('PATCH', { status, version: plan.version }),
    )
  },

  async deleteMeal(
    accessToken: string,
    plan: MealPlan,
    mealId: string,
  ): Promise<void> {
    await send(
      `/${encodedId(plan.id)}/meals/${encodedId(mealId)}`,
      accessToken,
      {
        method: 'DELETE',
        headers: { 'If-Match': `"${plan.version}"` },
      },
    )
  },
}
