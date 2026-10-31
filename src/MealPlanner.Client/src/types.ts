export type MealPlanStatus = 'Draft' | 'Active' | 'Completed' | 'Cancelled'
export type MealSlot = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snack'
export type MealEntryStatus = 'Planned' | 'InProgress' | 'Completed' | 'Skipped'

export interface MealPlan {
  id: string
  name: string
  startDate: string
  endDate: string
  status: MealPlanStatus
  createdAt: string
  updatedAt: string
  version: string
}

export interface MealPlanPage {
  items: MealPlan[]
  page: number
  pageSize: number
  totalCount: number
}

export interface MealEntry {
  id: string
  mealPlanId: string
  date: string
  slot: MealSlot
  title: string
  recipeId: string | null
  servings: number
  status: MealEntryStatus
  createdAt: string
  updatedAt: string
  version: string
}

export interface MealEntryPage {
  items: MealEntry[]
  page: number
  pageSize: number
  totalCount: number
}

export interface PlanDraft {
  name: string
  startDate: string
  endDate: string
}

export interface MealDraft {
  date: string
  slot: MealSlot
  title: string
  recipeId: string
  servings: number
}
