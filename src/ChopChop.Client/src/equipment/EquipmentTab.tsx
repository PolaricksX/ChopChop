import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import {
  createEquipment,
  deleteEquipment,
  EquipmentApiError,
  listEquipment,
  setEquipmentAvailability,
  updateEquipment,
} from './api'
import type {
  Equipment,
  EquipmentAvailability,
  EquipmentCondition,
  EquipmentFilters,
  EquipmentType,
  EquipmentUpsertRequest,
} from './types'
import { equipmentConditions, equipmentTypes } from './types'
import type { EquipmentPermission } from '../auth'

const pageSize = 20

type EquipmentFormState = Omit<EquipmentUpsertRequest, 'quantity'> & { quantity: string }
type FilterDraft = {
  search: string
  type: '' | EquipmentType
  condition: '' | EquipmentCondition
  availability: '' | EquipmentAvailability
}
type AppliedFilters = Pick<EquipmentFilters, 'search' | 'type' | 'condition' | 'availability'>
type EquipmentTabProps =
  | { accessToken: string; getAccessToken?: never }
  | { accessToken?: never; getAccessToken: (permission: EquipmentPermission) => Promise<string> }

const emptyForm: EquipmentFormState = {
  name: '',
  type: 'Cookware',
  quantity: '1',
  condition: 'Good',
  brand: '',
  size: '',
  notes: '',
  location: '',
}

const emptyFilters: FilterDraft = {
  search: '',
  type: '',
  condition: '',
  availability: '',
}

export function EquipmentTab({ accessToken, getAccessToken }: EquipmentTabProps) {
  const [items, setItems] = useState<Equipment[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [filterDraft, setFilterDraft] = useState<FilterDraft>(emptyFilters)
  const [filters, setFilters] = useState<AppliedFilters>({})
  const [loadedRequestKey, setLoadedRequestKey] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [editingItem, setEditingItem] = useState<Equipment | null>(null)
  const [form, setForm] = useState<EquipmentFormState>(emptyForm)
  // Reused for retries of an identical create so a lost response cannot create a duplicate.
  const createKeyRef = useRef<string | null>(null)
  const requestKey = JSON.stringify([accessToken, page, filters, refreshKey])
  const loading = loadedRequestKey !== requestKey
  const accessTokenFor = useCallback(
    (permission: EquipmentPermission): Promise<string> => {
      if (getAccessToken) return getAccessToken(permission)
      if (accessToken) return Promise.resolve(accessToken)
      throw new Error('An Equipment API access token is required.')
    },
    [accessToken, getAccessToken],
  )

  useEffect(() => {
    let active = true
    void accessTokenFor('equipment.read')
      .then((accessToken) => listEquipment(accessToken, { page, pageSize, ...filters }))
      .then((result) => {
        if (!active) return
        setTotalCount(result.totalCount)
        const lastPage = Math.max(1, Math.ceil(result.totalCount / pageSize))
        if (page > lastPage) {
          setPage(lastPage)
          return
        }

        setItems(result.items)
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
  }, [accessTokenFor, filters, page, refreshKey, requestKey])

  const applyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')
    setPage(1)
    setFilters({
      ...(filterDraft.search.trim() ? { search: filterDraft.search.trim() } : {}),
      ...(filterDraft.type ? { type: filterDraft.type } : {}),
      ...(filterDraft.condition ? { condition: filterDraft.condition } : {}),
      ...(filterDraft.availability ? { availability: filterDraft.availability } : {}),
    })
  }

  const beginCreate = () => {
    createKeyRef.current = null
    setEditingItem(null)
    setForm(emptyForm)
    setError('')
    setShowForm(true)
  }

  const beginEdit = (item: Equipment) => {
    setEditingItem(item)
    setForm({
      name: item.name,
      type: item.type,
      quantity: String(item.quantity),
      condition: item.condition,
      brand: item.brand ?? '',
      size: item.size ?? '',
      notes: item.notes ?? '',
      location: item.location ?? '',
    })
    setError('')
    setShowForm(true)
  }

  const submitEquipment = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (busy) return

    const errors = validateEquipmentForm(form)
    if (errors.length > 0) {
      setError(errors.join(' '))
      return
    }

    const payload: EquipmentUpsertRequest = {
      name: form.name.trim(),
      type: form.type,
      quantity: Number(form.quantity),
      condition: form.condition,
      brand: optionalValue(form.brand),
      size: optionalValue(form.size),
      notes: optionalValue(form.notes),
      location: optionalValue(form.location),
    }

    setBusy(true)
    setError('')
    setNotice('')
    try {
      if (editingItem) {
        await updateEquipment(await accessTokenFor('equipment.write'), editingItem.id, editingItem.version, payload)
        setNotice('Equipment updated.')
      } else {
        createKeyRef.current ??= crypto.randomUUID()
        await createEquipment(await accessTokenFor('equipment.write'), payload, createKeyRef.current)
        createKeyRef.current = null
        setNotice('Equipment added.')
      }

      setShowForm(false)
      setEditingItem(null)
      setForm(emptyForm)
      setPage(1)
      setRefreshKey((key) => key + 1)
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setBusy(false)
    }
  }

  const changeAvailability = async (item: Equipment) => {
    if (busy) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await setEquipmentAvailability(
        await accessTokenFor('equipment.write'),
        item.id,
        item.version,
        item.availability !== 'Available',
      )
      setNotice(item.availability === 'Available' ? 'Equipment marked unavailable.' : 'Equipment marked available.')
      setRefreshKey((key) => key + 1)
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setBusy(false)
    }
  }

  const removeEquipment = async (item: Equipment) => {
    if (busy || !window.confirm(`Delete ${item.name}? This cannot be undone.`)) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await deleteEquipment(await accessTokenFor('equipment.write'), item)
      setNotice('Equipment deleted.')
      setRefreshKey((key) => key + 1)
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setBusy(false)
    }
  }

  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  const updateForm = <K extends keyof EquipmentFormState>(field: K, value: EquipmentFormState[K]) => {
    createKeyRef.current = null
    setForm((current) => ({ ...current, [field]: value }))
  }

  return (
    <section className="panel equipment-panel" aria-labelledby="equipment-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">YOUR KITCHEN, AT A GLANCE</p>
          <h2 id="equipment-heading">Equipment</h2>
        </div>
        <button className="primary" type="button" onClick={beginCreate}>Add equipment</button>
      </div>

      <form className="equipment-filters" onSubmit={applyFilters} aria-label="Filter equipment">
        <div className="equipment-filter-field">
          <label htmlFor="equipment-search">Search</label>
          <input
            id="equipment-search"
            value={filterDraft.search}
            maxLength={100}
            onChange={(event) => setFilterDraft((current) => ({ ...current, search: event.target.value }))}
            placeholder="Name, brand, size, notes, or location"
          />
        </div>
        <div className="equipment-filter-field">
          <label htmlFor="equipment-type-filter">Type</label>
          <select
            id="equipment-type-filter"
            value={filterDraft.type}
            onChange={(event) => {
              const value = event.target.value
              setFilterDraft((current) => ({ ...current, type: isEquipmentType(value) ? value : '' }))
            }}
          >
            <option value="">All types</option>
            {equipmentTypes.map((type) => <option key={type} value={type}>{labelFor(type)}</option>)}
          </select>
        </div>
        <div className="equipment-filter-field">
          <label htmlFor="equipment-condition-filter">Condition</label>
          <select
            id="equipment-condition-filter"
            value={filterDraft.condition}
            onChange={(event) => {
              const value = event.target.value
              setFilterDraft((current) => ({ ...current, condition: isEquipmentCondition(value) ? value : '' }))
            }}
          >
            <option value="">All conditions</option>
            {equipmentConditions.map((condition) => <option key={condition} value={condition}>{condition}</option>)}
          </select>
        </div>
        <div className="equipment-filter-field">
          <label htmlFor="equipment-availability-filter">Availability</label>
          <select
            id="equipment-availability-filter"
            value={filterDraft.availability}
            onChange={(event) => {
              const value = event.target.value
              setFilterDraft((current) => ({ ...current, availability: isEquipmentAvailability(value) ? value : '' }))
            }}
          >
            <option value="">All availability</option>
            <option value="Available">Available</option>
            <option value="Unavailable">Unavailable</option>
          </select>
        </div>
        <button className="secondary" type="submit">Apply filters</button>
      </form>

      {error && <p role="alert" className="message error">{error}</p>}
      {notice && <p role="status" className="message success">{notice}</p>}

      {showForm && (
        <form className="form-grid equipment-form" onSubmit={submitEquipment} noValidate aria-label={editingItem ? 'Edit equipment' : 'Add equipment form'}>
          <h3>{editingItem ? 'Edit equipment' : 'Add equipment'}</h3>
          <label htmlFor="equipment-name">Name</label>
          <input
            id="equipment-name"
            value={form.name}
            maxLength={120}
            required
            onChange={(event) => updateForm('name', event.target.value)}
          />
          <label htmlFor="equipment-type">Type</label>
          <select
            id="equipment-type"
            value={form.type}
            onChange={(event) => {
              const value = event.target.value
              if (isEquipmentType(value)) updateForm('type', value)
            }}
          >
            {equipmentTypes.map((type) => <option key={type} value={type}>{labelFor(type)}</option>)}
          </select>
          <label htmlFor="equipment-quantity">Quantity</label>
          <input
            id="equipment-quantity"
            type="number"
            min={1}
            max={1000}
            step={1}
            value={form.quantity}
            required
            onChange={(event) => updateForm('quantity', event.target.value)}
          />
          <label htmlFor="equipment-condition">Condition</label>
          <select
            id="equipment-condition"
            value={form.condition}
            onChange={(event) => {
              const value = event.target.value
              if (isEquipmentCondition(value)) updateForm('condition', value)
            }}
          >
            {equipmentConditions.map((condition) => <option key={condition} value={condition}>{condition}</option>)}
          </select>
          <label htmlFor="equipment-brand">Brand <span className="label-note">(optional)</span></label>
          <input id="equipment-brand" value={form.brand ?? ''} maxLength={100} onChange={(event) => updateForm('brand', event.target.value)} />
          <label htmlFor="equipment-size">Size <span className="label-note">(optional)</span></label>
          <input id="equipment-size" value={form.size ?? ''} maxLength={100} onChange={(event) => updateForm('size', event.target.value)} />
          <label htmlFor="equipment-location">Location <span className="label-note">(optional)</span></label>
          <input id="equipment-location" value={form.location ?? ''} maxLength={120} onChange={(event) => updateForm('location', event.target.value)} />
          <label htmlFor="equipment-notes">Notes <span className="label-note">(optional)</span></label>
          <textarea id="equipment-notes" value={form.notes ?? ''} maxLength={500} onChange={(event) => updateForm('notes', event.target.value)} />
          <div className="button-row">
            <button className="primary" type="submit" disabled={busy}>
              {busy ? 'Saving…' : editingItem ? 'Save changes' : 'Save equipment'}
            </button>
            <button
              className="quiet"
              type="button"
              disabled={busy}
              onClick={() => { setShowForm(false); setEditingItem(null); setError('') }}
            >
              Cancel
            </button>
          </div>
        </form>
      )}

      {loading
        ? <p role="status">Loading equipment…</p>
        : items.length === 0
          ? <p className="section-note">{hasFilters(filters) ? 'No equipment matches these filters.' : 'No equipment yet. Add your first kitchen item.'}</p>
          : <div className="equipment-list" aria-label="Equipment items">
            {items.map((item) => (
              <article className="equipment-card" key={item.id} aria-labelledby={`equipment-${item.id}`}>
                <div className="equipment-card-heading">
                  <div>
                    <p className="eyebrow">{labelFor(item.type)} · {item.availability}</p>
                    <h3 id={`equipment-${item.id}`}>{item.name}</h3>
                  </div>
                  <span className="pill">Qty {item.quantity}</span>
                </div>
                <p className="equipment-details">
                  {item.condition}
                  {item.brand && ` · ${item.brand}`}
                  {item.size && ` · ${item.size}`}
                  {item.location && ` · ${item.location}`}
                </p>
                {item.notes && <p>{item.notes}</p>}
                <div className="button-row">
                  <button className="secondary" type="button" disabled={busy} onClick={() => beginEdit(item)}>Edit</button>
                  <button className="quiet" type="button" disabled={busy} onClick={() => void changeAvailability(item)}>
                    Mark {item.availability === 'Available' ? 'unavailable' : 'available'}
                  </button>
                  <button className="quiet" type="button" disabled={busy} onClick={() => void removeEquipment(item)}>Delete</button>
                </div>
              </article>
            ))}
          </div>}

      <div className="equipment-pagination" aria-label="Equipment pagination">
        <span>{totalCount} item{totalCount === 1 ? '' : 's'} · Page {page} of {pageCount}</span>
        <div className="button-row">
          <button className="secondary" type="button" disabled={loading || page <= 1} onClick={() => setPage((current) => current - 1)}>Previous</button>
          <button className="secondary" type="button" disabled={loading || page >= pageCount} onClick={() => setPage((current) => current + 1)}>Next</button>
        </div>
      </div>
    </section>
  )
}

function validateEquipmentForm(form: EquipmentFormState) {
  const errors: string[] = []
  if (!form.name.trim()) errors.push('Name is required.')
  if (form.name.trim().length > 120) errors.push('Name must be 120 characters or fewer.')

  const quantity = Number(form.quantity)
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > 1000) {
    errors.push('Quantity must be a whole number from 1 to 1000.')
  }

  if (form.brand && form.brand.trim().length > 100) errors.push('Brand must be 100 characters or fewer.')
  if (form.size && form.size.trim().length > 100) errors.push('Size must be 100 characters or fewer.')
  if (form.notes && form.notes.trim().length > 500) errors.push('Notes must be 500 characters or fewer.')
  if (form.location && form.location.trim().length > 120) errors.push('Location must be 120 characters or fewer.')
  return errors
}

function isEquipmentType(value: string): value is EquipmentType {
  return equipmentTypes.some((type) => type === value)
}

function isEquipmentCondition(value: string): value is EquipmentCondition {
  return equipmentConditions.some((condition) => condition === value)
}

function isEquipmentAvailability(value: string): value is EquipmentAvailability {
  return value === 'Available' || value === 'Unavailable'
}

function optionalValue(value: string | null) {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

function hasFilters(filters: AppliedFilters) {
  return Object.values(filters).some(Boolean)
}

function labelFor(value: string) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2')
}

function messageFor(error: unknown) {
  return error instanceof EquipmentApiError
    ? error.message
    : error instanceof Error
      ? error.message
      : 'Equipment could not be loaded. Please try again.'
}
