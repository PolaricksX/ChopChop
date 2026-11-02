import { useEffect, useState } from 'react'
import { listRecipes, RecipeApiError } from './api'
import type { RecipeSummary } from './types'

export function RecipeList() {
  const [page, setPage] = useState<{ items: RecipeSummary[]; page: number; pageSize: number; totalCount: number } | null>(null)
  const [error, setError] = useState(''); const [loading, setLoading] = useState(true)
  const load = (number: number) => { setLoading(true); setError(''); listRecipes({ page: number, pageSize: 12 }).then(setPage).catch((e: unknown) => setError(e instanceof RecipeApiError ? e.message : 'Recipes could not be loaded.')).finally(() => setLoading(false)) }
  useEffect(() => load(1), [])
  if (loading) return <section className="panel"><p role="status">Loading published recipes…</p></section>
  if (error) return <section className="panel"><p role="alert" className="message error">{error}</p><button onClick={() => load(page?.page ?? 1)}>Try again</button></section>
  if (!page?.items.length) return <section className="panel"><h2>Published recipes</h2><p>No published recipes yet.</p></section>
  return <section className="panel" aria-labelledby="published-recipes-heading"><h2 id="published-recipes-heading">Published recipes</h2><div className="recipe-grid">{page.items.map((recipe) => <article className="recipe-card" key={recipe.recipeId}><h3><a href={`/recipes/${recipe.recipeId}`}>{recipe.title}</a></h3><p>{recipe.servings} servings · {recipe.totalMinutes} minutes</p>{recipe.tags.length > 0 && <p className="small-note">{recipe.tags.join(', ')}</p>}</article>)}</div><div className="form-grid"><p>Page {page.page} of {Math.max(1, Math.ceil(page.totalCount / page.pageSize))}</p><button disabled={page.page <= 1} onClick={() => load(page.page - 1)}>Previous</button><button disabled={page.page * page.pageSize >= page.totalCount} onClick={() => load(page.page + 1)}>Next</button></div></section>
}
