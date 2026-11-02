import { useEffect, useState } from 'react'
import { deleteDraft, getDrafts, publishDraft, RecipeApiError, unpublishRecipe } from './api'
import type { DraftRecipe } from './types'
export function RecipeDrafts({ accessToken }: { accessToken: string }) {
  const [drafts, setDrafts] = useState<DraftRecipe[]>([]); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const load = () => getDrafts(accessToken).then((page) => setDrafts(page.items)).catch((e: unknown) => setError(e instanceof RecipeApiError ? e.message : 'Drafts could not be loaded.'))
  useEffect(() => {
    void load()
  }, [accessToken])
  const action = async (fn: () => Promise<unknown>, message: string) => { setBusy(true); setError(''); try { await fn(); await load(); window.history.replaceState({}, '', '/drafts'); } catch (e) { setError(e instanceof RecipeApiError ? e.message : message) } finally { setBusy(false) } }
  return <section className="panel"><h2>My drafts</h2>{error && <p role="alert" className="message error">{error}</p>}{!drafts.length && !error ? <p>No drafts found.</p> : <div className="recipe-grid">{drafts.map((draft) => <article className="recipe-card" key={draft.recipeId}><h3>{draft.version.title}</h3><p>{draft.status} · version {draft.version.version}</p><a href={`/recipes/${draft.recipeId}/edit`}>Edit</a><div className="form-grid"><button disabled={busy} onClick={() => action(() => deleteDraft(accessToken, draft.recipeId), 'Draft could not be deleted.')}>Delete</button><button disabled={busy} onClick={() => action(() => publishDraft(accessToken, draft.recipeId), 'Draft could not be published.')}>Publish</button>{draft.status === 'Published' && <button disabled={busy} onClick={() => action(() => unpublishRecipe(accessToken, draft.recipeId), 'Recipe could not be unpublished.')}>Unpublish</button>}</div></article>)}</div>}</section>
}
