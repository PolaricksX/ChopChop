import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { getRecipeVersion } from '../recipes/api'
import { createFolder, deleteFolder, listFolders, listSavedRecipes, removeSavedRecipe, CookbookApiError } from './api'
import type { Folder, SavedRecipe } from './types'

type DisplayRecipe = SavedRecipe & { title: string }

export function CookbookTab() {
  const [folders, setFolders] = useState<Folder[]>([])
  const [selected, setSelected] = useState<Folder | null>(null)
  const [saved, setSaved] = useState<DisplayRecipe[]>([])
  const [name, setName] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  const loadFolders = async () => {
    setLoading(true); setError('')
    try { setFolders(await listFolders()) } catch (e) { setError(e instanceof CookbookApiError ? e.message : 'Folders could not be loaded.') } finally { setLoading(false) }
  }
  useEffect(() => { void loadFolders() }, [])

  const openFolder = async (folder: Folder) => {
    setSelected(folder); setError('')
    try {
      const recipes = await listSavedRecipes(folder.id)
      const displayed = await Promise.all(recipes.map(async (recipe) => {
        try { return { ...recipe, title: (await getRecipeVersion(recipe.recipeId, recipe.recipeVersion)).title } }
        catch { return { ...recipe, title: `${recipe.recipeId} (version ${recipe.recipeVersion})` } }
      }))
      setSaved(displayed)
    } catch (e) { setError(e instanceof CookbookApiError ? e.message : 'Saved recipes could not be loaded.') }
  }

  const submitFolder = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const trimmed = name.trim()
    if (!trimmed) { setError('Folder name is required.'); return }
    if (trimmed.length > 100) { setError('Folder name must be 100 characters or fewer.'); return }
    setBusy(true); setError('')
    try { const folder = await createFolder(trimmed); setFolders((items) => [...items, folder]); setName('') }
    catch (e) { setError(e instanceof CookbookApiError ? e.message : 'Folder could not be created.') }
    finally { setBusy(false) }
  }

  const removeFolder = async (folder: Folder) => {
    setBusy(true); setError('')
    try { await deleteFolder(folder.id); setFolders((items) => items.filter((item) => item.id !== folder.id)); if (selected?.id === folder.id) { setSelected(null); setSaved([]) } }
    catch (e) { setError(e instanceof CookbookApiError ? e.message : 'Folder could not be deleted.') }
    finally { setBusy(false) }
  }

  const removeRecipe = async (recipe: DisplayRecipe) => {
    if (!selected) return
    try { await removeSavedRecipe(selected.id, recipe.recipeId, recipe.recipeVersion); setSaved((items) => items.filter((item) => item !== recipe)) }
    catch (e) { setError(e instanceof CookbookApiError ? e.message : 'Saved recipe could not be removed.') }
  }

  return <section className="panel" aria-labelledby="cookbook-heading">
    <div className="panel-heading"><div><p className="eyebrow">YOUR COOKBOOK</p><h2 id="cookbook-heading">Cookbook</h2></div></div>
    {error && <p role="alert" className="message error">{error}</p>}
    <form noValidate onSubmit={submitFolder} className="form-grid"><label htmlFor="folder-name">New folder</label><input id="folder-name" value={name} maxLength={100} onChange={(e) => setName(e.target.value)} required /><button className="primary" disabled={busy}>Create folder</button></form>
    {loading ? <p role="status">Loading folders…</p> : folders.length === 0 ? <p>No folders yet. Create one to save recipes.</p> : <ul>{folders.map((folder) => <li key={folder.id}><button className="secondary" onClick={() => void openFolder(folder)}>{folder.name}</button> <button className="quiet" disabled={busy} onClick={() => void removeFolder(folder)}>Delete</button></li>)}</ul>}
    {selected && <div><h3>{selected.name}</h3>{saved.length === 0 ? <p>No saved recipes in this folder.</p> : <ul>{saved.map((recipe) => <li key={`${recipe.recipeId}-${recipe.recipeVersion}`}><span>{recipe.title}</span> <button className="quiet" onClick={() => void removeRecipe(recipe)}>Remove</button></li>)}</ul>}</div>}
  </section>
}
