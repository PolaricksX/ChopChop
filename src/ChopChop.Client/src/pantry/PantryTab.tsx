import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { PantryPermission } from '../auth'
import {
  createPantryItem,
  deletePantryItem,
  listPantry,
  pantryTypes,
  pantryUnits,
  setPantryItemAvailability,
  updatePantryItem,
} from './pantryClient'
import type {
  PantryFilters,
  PantryItem,
  PantryItemRequest,
  PantryType,
  PantryUnit,
} from './pantryClient'

const pageSize = 20
const emptyFilters: PantryFilters = { page: 1, pageSize }
const emptyForm: PantryItemRequest = {
  name: '',
  type: pantryTypes[0],
  quantity: 1,
  unit: 'Count',
  expiresAt: '',
  notes: '',
}

export function PantryTab({
  getAccessToken,
}: {
  getAccessToken: (permission: PantryPermission) => Promise<string>
}) {
  const [items, setItems] = useState<PantryItem[]>([])
  const [filters, setFilters] = useState<PantryFilters>(emptyFilters)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingItem, setEditingItem] = useState<PantryItem | null>(null)
  const [form, setForm] = useState<PantryItemRequest>(emptyForm)
  const [loadedRequestKey, setLoadedRequestKey] = useState<string | null>(null)
  const [refreshKey, setRefreshKey] = useState(0)
  const requestKey = JSON.stringify([filters, refreshKey])
  const loading = loadedRequestKey !== requestKey

  useEffect(() => {
    let active = true
    void listPantry(getAccessToken, filters)
      .then((result) => {
        if (active) setItems(result)
      })
      .catch((requestError: unknown) => {
        if (active) setError(messageFor(requestError))
      })
      .finally(() => {
        if (active) setLoadedRequestKey(requestKey)
      })

    return () => {
      active = false
    }
  }, [filters, getAccessToken, refreshKey, requestKey])

  const updateFilter = (key: 'search' | 'type' | 'availability' | 'expiresBefore', value: string) => {
    setError('')
    setFilters((current) => ({ ...current, [key]: value, page: 1 }))
  }

  const retryLoad = () => {
    setError('')
    setRefreshKey((key) => key + 1)
  }

  const startCreate = () => {
    setEditingItem(null)
    setForm({ ...emptyForm })
    setError('')
    setNotice('')
    setShowForm(true)
  }

  const edit = (item: PantryItem) => {
    setEditingItem(item)
    setForm({
      name: item.name,
      type: item.type ?? 'Custom',
      customCategory: item.customCategory ?? '',
      quantity: item.quantity,
      unit: item.unit,
      expiresAt: item.expiresAt?.slice(0, 10) ?? '',
      notes: item.notes ?? '',
    })
    setError('')
    setNotice('')
    setShowForm(true)
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (busy) return
    const name = form.name.trim()
    if (!name || !Number.isFinite(form.quantity) || form.quantity <= 0) {
      setError('Name and a quantity greater than zero are required.')
      return
    }

    setBusy(true)
    setError('')
    setNotice('')
    const item = {
      ...form,
      name,
      ...(form.type.toLowerCase() === 'custom'
        ? { customCategory: form.customCategory?.trim() }
        : { customCategory: undefined }),
    }
    try {
      if (editingItem) {
        await updatePantryItem(getAccessToken, editingItem.id, item, editingItem.version)
        setNotice('Pantry item updated.')
      } else {
        await createPantryItem(getAccessToken, item)
        setNotice('Pantry item added.')
      }
      setShowForm(false)
      setEditingItem(null)
      setForm({ ...emptyForm })
      setRefreshKey((key) => key + 1)
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setBusy(false)
    }
  }

  const actOnItem = async (item: PantryItem, action: 'consume' | 'unavailable' | 'delete') => {
    if (busy) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      if (action === 'delete') {
        await deletePantryItem(getAccessToken, item.id)
        setNotice('Pantry item deleted.')
      } else {
        await setPantryItemAvailability(getAccessToken, item.id, action, item.version)
        setNotice(action === 'consume' ? 'Pantry item consumed.' : 'Pantry item marked unavailable.')
      }
      setRefreshKey((key) => key + 1)
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setBusy(false)
    }
  }

  const currentAvailability = (item: PantryItem) => item.availability ?? item.state ?? 'Available'
  const updateForm = <K extends keyof PantryItemRequest>(field: K, value: PantryItemRequest[K]) => {
    setForm((current) => ({ ...current, [field]: value }))
  }

  return (
    <section className="panel pantry-panel" aria-labelledby="pantry-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">YOUR KITCHEN, AT A GLANCE</p>
          <h2 id="pantry-heading">My pantry</h2>
        </div>
        <button className="primary" type="button" onClick={startCreate}>Add item</button>
      </div>

      <form className="pantry-filters" aria-label="Filter pantry" onSubmit={(event) => event.preventDefault()}>
        <div className="pantry-filter-field">
          <label htmlFor="pantry-search">Search</label>
          <input id="pantry-search" value={filters.search ?? ''} onChange={(event) => updateFilter('search', event.target.value)} placeholder="Search pantry" />
        </div>
        <div className="pantry-filter-field">
          <label htmlFor="pantry-type-filter">Type</label>
          <select id="pantry-type-filter" value={filters.type ?? ''} onChange={(event) => updateFilter('type', event.target.value)}>
            <option value="">All types</option>
            {pantryTypes.map((type) => <option key={type} value={type}>{type}</option>)}
            <option value="Custom">Other</option>
          </select>
        </div>
        <div className="pantry-filter-field">
          <label htmlFor="pantry-availability-filter">Availability</label>
          <select id="pantry-availability-filter" value={filters.availability ?? ''} onChange={(event) => updateFilter('availability', event.target.value)}>
            <option value="">All statuses</option>
            <option value="Available">Available</option>
            <option value="Consumed">Consumed</option>
            <option value="Unavailable">Unavailable</option>
          </select>
        </div>
        <div className="pantry-filter-field">
          <label htmlFor="pantry-expiration-filter">Expires before</label>
          <input id="pantry-expiration-filter" type="date" value={filters.expiresBefore ?? ''} onChange={(event) => updateFilter('expiresBefore', event.target.value)} />
        </div>
      </form>

      {error && <p role="alert" className="message error">{error} <button className="quiet" type="button" onClick={retryLoad}>Try again</button></p>}
      {notice && <p role="status" className="message success">{notice}</p>}

      {loading
        ? <p role="status">Loading pantry…</p>
        : items.length === 0
          ? <p className="section-note">No pantry items match these filters.</p>
          : <div className="pantry-list" aria-label="Pantry items">
            {items.map((item) => {
              const availability = currentAvailability(item)
              return (
                <article className="pantry-card" key={item.id}>
                  <div className="pantry-card-heading">
                    <div>
                      <p className="eyebrow">{item.type ?? item.customCategory ?? 'Other'} · {availability}</p>
                      <h3>{item.name}</h3>
                    </div>
                    <span className="pill">{item.quantity} {item.unit}</span>
                  </div>
                  {item.expiresAt && <p className="pantry-details">Expires {new Date(item.expiresAt).toLocaleDateString()}</p>}
                  {item.notes && <p className="pantry-details">{item.notes}</p>}
                  <div className="pantry-actions">
                    <button className="secondary" type="button" disabled={busy} onClick={() => edit(item)}>Edit</button>
                    {availability === 'Available' && <>
                      <button className="secondary" type="button" disabled={busy} onClick={() => void actOnItem(item, 'consume')}>Consume</button>
                      <button className="secondary" type="button" disabled={busy} onClick={() => void actOnItem(item, 'unavailable')}>Mark unavailable</button>
                    </>}
                    <button className="quiet" type="button" disabled={busy} onClick={() => void actOnItem(item, 'delete')}>Delete</button>
                  </div>
                </article>
              )
            })}
          </div>}

      {items.length > 0 && (
        <nav className="pantry-pagination" aria-label="Pantry pagination">
          <button className="secondary" type="button" disabled={filters.page <= 1 || loading} onClick={() => setFilters((current) => ({ ...current, page: current.page - 1 }))}>Previous</button>
          <span>Page {filters.page}</span>
          <button className="secondary" type="button" disabled={loading || items.length < pageSize} onClick={() => setFilters((current) => ({ ...current, page: current.page + 1 }))}>Next</button>
        </nav>
      )}

      {showForm && (
        <dialog
          open
          aria-labelledby="pantry-form-heading"
          onCancel={(event) => {
            event.preventDefault()
            if (!busy) setShowForm(false)
          }}
        >
          <form onSubmit={submit} className="pantry-form">
            <h3 id="pantry-form-heading">{editingItem ? 'Edit pantry item' : 'Add pantry item'}</h3>
            <label htmlFor="pantry-name">Name</label>
            <input id="pantry-name" required maxLength={120} value={form.name} onChange={(event) => updateForm('name', event.target.value)} />
            <label htmlFor="pantry-type">Type</label>
            <select id="pantry-type" value={form.type} onChange={(event) => updateForm('type', event.target.value as PantryType)}>
              {pantryTypes.map((type) => <option key={type} value={type}>{type}</option>)}
              <option value="Custom">Other</option>
            </select>
            {form.type.toLowerCase() === 'custom' && <>
              <label htmlFor="pantry-custom-category">Custom category</label>
              <input id="pantry-custom-category" required maxLength={60} value={form.customCategory ?? ''} onChange={(event) => updateForm('customCategory', event.target.value)} />
            </>}
            <div className="pantry-form-row">
              <label htmlFor="pantry-quantity">Quantity
                <input id="pantry-quantity" required min="0.01" step="any" type="number" value={form.quantity} onChange={(event) => updateForm('quantity', Number(event.target.value))} />
              </label>
              <label htmlFor="pantry-unit">Unit
                <select id="pantry-unit" value={form.unit} onChange={(event) => updateForm('unit', event.target.value as PantryUnit)}>
                  {pantryUnits.map((unit) => <option key={unit} value={unit}>{unit}</option>)}
                </select>
              </label>
            </div>
            <label htmlFor="pantry-expires-at">Expiration date</label>
            <input id="pantry-expires-at" type="date" value={form.expiresAt ?? ''} onChange={(event) => updateForm('expiresAt', event.target.value)} />
            <label htmlFor="pantry-notes">Notes</label>
            <textarea id="pantry-notes" maxLength={500} value={form.notes ?? ''} onChange={(event) => updateForm('notes', event.target.value)} />
            {error && <p role="alert" className="message error">{error}</p>}
            <div className="button-row">
              <button className="secondary" type="button" disabled={busy} onClick={() => setShowForm(false)}>Cancel</button>
              <button className="primary" type="submit" disabled={busy}>{busy ? 'Saving…' : 'Save item'}</button>
            </div>
          </form>
        </dialog>
      )}
    </section>
  )
}

function messageFor(error: unknown) {
  return error instanceof Error ? error.message : 'The pantry request failed.'
}
