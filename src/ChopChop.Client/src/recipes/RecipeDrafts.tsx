import { useEffect, useState } from 'react'
import { deleteDraft, getDrafts, publishDraft, unpublishRecipe } from './api'
import type { DraftRecipe } from './types'
export function RecipeDrafts({ onEdit }: { onEdit?: (recipeId: string) => void }) {
  const [drafts, setDrafts] = useState<DraftRecipe[]>([]); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const load = () => getDrafts().then((page) => { setDrafts(page.items); setError('') }).catch((e: unknown) => setError(e instanceof Error ? e.message : 'Drafts could not be loaded.'))
  useEffect(() => {
    void load()
  }, [])
  const action = async (fn: () => Promise<unknown>, message: string) => { setBusy(true); setError(''); try { await fn(); await load(); } catch (e) { setError(e instanceof Error ? e.message : message) } finally { setBusy(false) } }
  return <section className="panel"><h2>My drafts</h2>{error && <><p role="alert" className="message error">{error}</p><button type="button" onClick={() => void load()}>Retry loading drafts</button></>}{!drafts.length && !error ? <p>No drafts found.</p> : <div className="recipe-grid">{drafts.map((draft) => <article className="recipe-card" key={draft.recipeId}><h3>{draft.version.title}</h3><p>{draft.status} · version {draft.version.version}</p><div className="button-row"><button className="secondary" onClick={() => onEdit?.(draft.recipeId)}>Edit</button><button className="secondary" disabled={busy} onClick={() => action(() => deleteDraft(draft.recipeId), 'Draft could not be deleted.')}>Delete</button><button className="primary" disabled={busy} onClick={() => action(() => publishDraft(draft.recipeId), 'Draft could not be published.')}>Publish</button>{draft.status === 'Published' && <button className="secondary" disabled={busy} onClick={() => action(() => unpublishRecipe(draft.recipeId), 'Recipe could not be unpublished.')}>Unpublish</button>}</div></article>)}</div>}</section>
}
