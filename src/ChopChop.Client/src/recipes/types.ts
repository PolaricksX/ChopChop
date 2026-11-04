export type Allergen = 'Peanuts' | 'TreeNuts' | 'SesameSeeds' | 'Milk' | 'Eggs' | 'Fish' | 'CrustaceansAndShellfish' | 'WheatAndTriticale' | 'Soy' | 'Mustard' | 'Sulphites'
export type AllergenStatus = 'Declared' | 'Unknown'
export type RecipeStatus = 'Draft' | 'Published' | 'Unpublished'

export type Ingredient = { name: string; quantity: number; unit: string }
export type InstructionStep = { order: number; content: string }
export type Equipment = { name: string; required: boolean; matchingMetadata?: string | null }
export type Nutrition = { calories: number; proteinGrams: number; carbohydratesGrams: number; fiberGrams: number; fatGrams: number; sodiumMilligrams: number }

export type RecipeContent = {
  title: string; description: string; servings: number; ingredients: Ingredient[]; steps: InstructionStep[]
  allergenStatus: AllergenStatus; allergens: Allergen[]; tags: string[]; dietaryLabels: string[]
  nutrition?: Nutrition | null; equipment: Equipment[]; prepMinutes: number; cookMinutes: number; totalMinutes: number
  isAiGenerated: boolean
}
export type RecipeVersion = RecipeContent & { recipeId: string; version: number; createdAtUtc: string; publishedAtUtc?: string | null }
export type DraftRecipe = { recipeId: string; ownerId: string; status: RecipeStatus; version: RecipeVersion; updatedAtUtc: string }
export type PublishedRecipe = { version: RecipeVersion }
export type RecipeSummary = { recipeId: string; version: number; title: string; servings: number; totalMinutes: number; publishedAtUtc?: string | null; allergenStatus: AllergenStatus; allergens: Allergen[]; tags: string[]; dietaryLabels: string[] }
export type Page<T> = { items: T[]; page: number; pageSize: number; totalCount: number; allergenExcludedCount?: number; preferenceExcludedCount?: number }
