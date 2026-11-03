import { useEffect, useState } from 'react'
import { getPublishedRecipe, RecipeApiError } from './api'
import type { PublishedRecipe } from './types'
export function RecipeDetails({ recipeId, onBack }: { recipeId: string; onBack?: () => void }) {
  const [recipe, setRecipe] = useState<PublishedRecipe | null>(null); const [error, setError] = useState('');
  useEffect(() => { getPublishedRecipe(recipeId).then(setRecipe).catch((e: unknown) => setError(e instanceof RecipeApiError ? e.message : 'Recipe could not be loaded.')) }, [recipeId])
  if (error) return <section className="panel"><p role="alert" className="message error">{error}</p></section>
  if (!recipe) return <section className="panel"><p role="status">Loading recipe…</p></section>
  const v = recipe.version
  return <article className="panel"><button className="secondary" onClick={onBack}>Back to recipes</button><p className="eyebrow">PUBLISHED RECIPE</p><h2>{v.title}</h2><p>{v.description}</p><p>{v.servings} servings · {v.totalMinutes} minutes</p><div className="recipe-columns"><div><h3>Ingredients</h3><ul>{v.ingredients.map((i, n) => <li key={`${i.name}-${n}`}>{i.quantity} {i.unit} {i.name}</li>)}</ul><h3>Equipment</h3><ul>{v.equipment.map((e) => <li key={e.name}>{e.name}</li>)}</ul></div><div><h3>Method</h3><ol>{v.steps.sort((a, b) => a.order - b.order).map((s) => <li key={s.order}>{s.content}</li>)}</ol></div></div><p><strong>Allergen information:</strong> {v.allergenStatus === 'Unknown' ? 'Unknown; verify every ingredient label.' : v.allergens.length ? v.allergens.join(', ') : 'None declared.'}</p>{v.isAiGenerated && <p className="disclaimer">AI-generated content. Verify ingredients, allergens, cooking temperatures, and food-safety guidance independently; this is not medical advice.</p>}</article>
}
