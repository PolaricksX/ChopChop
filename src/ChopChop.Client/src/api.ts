export type RecipeIngredient = {
  name: string
  quantity: number
  unit: string
  allergens: string[]
}

export type RecipeDraft = {
  title: string
  description: string
  servings: number
  prepMinutes: number
  cookMinutes: number
  ingredients: RecipeIngredient[]
  steps: string[]
  allergens: string[]
  equipment: string[]
  nutrition: {
    calories: number
    proteinGrams: number
    carbohydrateGrams: number
    fatGrams: number
  }
  isAiGenerated: boolean
  nutritionIsEstimate: boolean
}

export type Generation = {
  id: string
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed' | 'Confirmed'
  createdAtUtc: string
  completedAtUtc: string | null
  draft: RecipeDraft | null
  failureCode: string | null
}

export type Review = {
  id: string
  recipeId: string
  ownerId: string
  rating: number
  body: string
  suggestion: string | null
  status: 'Pending' | 'Published' | 'Rejected' | 'Hidden'
  createdAtUtc: string
  updatedAtUtc: string
}

export class ApiError extends Error {
  readonly status: number

  constructor(
    message: string,
    status: number,
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(
  baseUrl: string,
  path: string,
  accessToken: string,
  init: RequestInit = {},
): Promise<T> {
  const response = await fetch(new URL(path, baseUrl), {
    ...init,
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'Content-Type': 'application/json',
      'X-Correlation-Id': crypto.randomUUID(),
      ...init.headers,
    },
  })

  if (!response.ok) {
    const problem = await response.json().catch(() => undefined)
    throw new ApiError(
      typeof problem?.detail === 'string'
        ? problem.detail
        : typeof problem?.error === 'string'
          ? problem.error
          : `The request failed (${response.status}).`,
      response.status,
    )
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

const aiApi = import.meta.env.VITE_AI_RECIPE_API_URL ?? 'http://localhost:5081'
const reviewApi = import.meta.env.VITE_INTERACTIVE_REVIEW_API_URL ?? 'http://localhost:5082'

export function createGeneration(
  accessToken: string,
  body: { prompt: string; includePantry: boolean; pantryItems: string[] },
) {
  return request<Generation>(aiApi, '/api/recipe-generations', accessToken, {
    method: 'POST',
    headers: { 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify(body),
  })
}

export function getGeneration(accessToken: string, id: string) {
  return request<Generation>(aiApi, `/api/recipe-generations/${encodeURIComponent(id)}`, accessToken)
}

export function confirmGeneration(accessToken: string, id: string) {
  return request<void>(
    aiApi,
    `/api/recipe-generations/${encodeURIComponent(id)}/confirm`,
    accessToken,
    { method: 'POST' },
  )
}

export function createReview(
  accessToken: string,
  body: { recipeId: string; rating: number; body: string; suggestion: string | null },
) {
  return request<Review>(reviewApi, '/api/reviews', accessToken, {
    method: 'POST',
    body: JSON.stringify(body),
  })
}

export function getReviews(accessToken: string, recipeId: string, page = 1) {
  const query = new URLSearchParams({ page: String(page), pageSize: '20' })
  return request<Review[]>(
    reviewApi,
    `/api/recipes/${encodeURIComponent(recipeId)}/reviews?${query}`,
    accessToken,
  )
}

export function getModerationQueue(accessToken: string) {
  return request<Review[]>(reviewApi, '/api/moderation/reviews?limit=50', accessToken)
}

export function moderateReview(
  accessToken: string,
  reviewId: string,
  status: 'Published' | 'Rejected',
  reason: string,
) {
  return request<void>(
    reviewApi,
    `/api/moderation/reviews/${encodeURIComponent(reviewId)}`,
    accessToken,
    { method: 'POST', body: JSON.stringify({ status, reason }) },
  )
}
