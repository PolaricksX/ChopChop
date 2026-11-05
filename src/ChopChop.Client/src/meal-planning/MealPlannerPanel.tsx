import { useEffect, useState, type FormEvent } from 'react'
import { ApiError, mealPlanApi } from './api'
import type {
  MealDraft,
  MealEntry,
  MealEntryStatus,
  MealPlan,
  MealPlanPage,
  MealEntryPage,
  MealPlanStatus,
  MealSlot,
  PlanDraft,
} from './types'
import './meal-planning.css'

const pageSize = 20
const mealSlots: MealSlot[] = ['Breakfast', 'Lunch', 'Dinner', 'Snack']

const planTransitions: Record<MealPlanStatus, MealPlanStatus[]> = {
  Draft: ['Active', 'Cancelled'],
  Active: ['Completed', 'Cancelled'],
  Completed: [],
  Cancelled: [],
}

const mealTransitions: Record<MealEntryStatus, MealEntryStatus[]> = {
  Planned: ['InProgress', 'Completed', 'Skipped'],
  InProgress: ['Completed', 'Skipped'],
  Completed: [],
  Skipped: [],
}

const planPageDefaults = {
  items: [],
  page: 1,
  pageSize,
  totalCount: 0,
}

const mealPageDefaults = {
  items: [],
  page: 1,
  pageSize,
  totalCount: 0,
}

function localDate(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function addCalendarDays(value: string, days: number): string {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(Date.UTC(year, month - 1, day + days))
  return date.toISOString().slice(0, 10)
}

function newPlanDraft(): PlanDraft {
  const startDate = localDate(new Date())
  return { name: '', startDate, endDate: addCalendarDays(startDate, 6) }
}

function newMealDraft(date?: string): MealDraft {
  return {
    date: date || localDate(new Date()),
    slot: 'Breakfast',
    title: '',
    recipeId: '',
    servings: 1,
  }
}

function planValidation(draft: PlanDraft): Record<string, string> {
  const errors: Record<string, string> = {}
  const name = draft.name.trim()
  if (!name) {
    errors.name = 'Enter a name for this plan.'
  } else if (name.length > 120) {
    errors.name = 'Plan names must be 120 characters or fewer.'
  }

  if (!draft.startDate || !draft.endDate) {
    errors.dates = 'Choose both a start date and an end date.'
  } else {
    const start = Date.parse(`${draft.startDate}T00:00:00Z`)
    const end = Date.parse(`${draft.endDate}T00:00:00Z`)
    if (!Number.isFinite(start) || !Number.isFinite(end)) {
      errors.dates = 'Enter valid calendar dates.'
    } else if (end < start) {
      errors.endDate = 'The end date must be on or after the start date.'
    } else if ((end - start) / 86_400_000 + 1 > 366) {
      errors.endDate = 'A plan can cover at most 366 calendar days.'
    }
  }

  return errors
}

function mealValidation(
  draft: MealDraft,
  plan: MealPlan,
  entries: MealEntry[],
  editingMealId: string | null,
): Record<string, string> {
  const errors: Record<string, string> = {}
  const title = draft.title.trim()
  if (!title) {
    errors.title = 'Enter a meal name.'
  } else if (title.length > 200) {
    errors.title = 'Meal names must be 200 characters or fewer.'
  }

  if (!draft.date) {
    errors.date = 'Choose a meal date.'
  } else if (draft.date < plan.startDate || draft.date > plan.endDate) {
    errors.date = 'Choose a date within this plan.'
  }

  if (draft.servings < 1 || draft.servings > 100 || !Number.isInteger(draft.servings)) {
    errors.servings = 'Servings must be a whole number from 1 to 100.'
  }

  if (draft.recipeId && !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(draft.recipeId)) {
    errors.recipeId = 'Enter a valid recipe ID, or leave this blank.'
  }

  if (
    draft.slot !== 'Snack' &&
    entries.some(
      (entry) =>
        entry.id !== editingMealId &&
        entry.date === draft.date &&
        entry.slot === draft.slot,
    )
  ) {
    errors.slot = `A ${draft.slot.toLowerCase()} is already planned for this date.`
  }

  return errors
}

function formatDate(value: string): string {
  const [year, month, day] = value.split('-').map(Number)
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month - 1, day)))
}

function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) {
    if (error instanceof TypeError) {
      return 'Could not reach the Meal Planner service. Check the API URL, CORS settings, and network connection.'
    }
    return error instanceof Error
      ? `The request could not be completed: ${error.message}`
      : 'Something unexpected happened.'
  }

  if (error.status === 403) {
    return 'Your account does not have permission to do that.'
  }
  if (error.status === 404) {
    return 'That meal plan or meal is no longer available. Refresh to see the latest data.'
  }
  if (error.status === 409) {
    return 'This plan changed or conflicts with another update. Refresh the data before trying again.'
  }
  if (error.status === 429) {
    return `The service is receiving too many requests.${error.retryAfter ? ` Try again after ${error.retryAfter}.` : ' Try again shortly.'}`
  }
  if (error.status >= 500) {
    return 'The Meal Planner service is temporarily unavailable. Your changes were not confirmed.'
  }
  return error.message
}

function pageCount(totalCount: number, size: number): number {
  return Math.max(1, Math.ceil(totalCount / size))
}

function ApiAlert({
  error,
  onRefresh,
}: {
  error: unknown
  onRefresh: () => void
}) {
  if (!error) {
    return null
  }

  const apiError = error instanceof ApiError ? error : null
  const validationErrors = Object.entries(apiError?.errors || {})
  const canRetry =
    error instanceof TypeError || Boolean(apiError && (apiError.status === 429 || apiError.status >= 500))
  const showRefresh =
    canRetry || apiError?.status === 404 || apiError?.status === 409

  return (
    <div className="notice notice-error" role="alert">
      <div>
        <strong>{errorMessage(error)}</strong>
        {validationErrors.length > 0 && (
          <ul className="field-errors">
            {validationErrors.flatMap(([field, messages]) =>
              messages.map((message, index) => (
                <li key={`${field}-${index}`}>
                  {field}: {message}
                </li>
              )),
            )}
          </ul>
        )}
        {apiError?.correlationId && (
          <p className="correlation-id">
            Reference: <code>{apiError.correlationId}</code>
          </p>
        )}
      </div>
      {showRefresh && (
        <button className="button button-secondary" onClick={onRefresh} type="button">
          {canRetry ? 'Try again' : 'Refresh data'}
        </button>
      )}
    </div>
  )
}

export function MealPlannerDashboard({
  accessToken,
  onSessionExpired,
}: {
  accessToken: string
  onSessionExpired: () => Promise<void>
}) {
  const [plans, setPlans] = useState<MealPlanPage>(planPageDefaults)
  const [plansLoading, setPlansLoading] = useState(true)
  const [plansPage, setPlansPage] = useState(1)
  const [plansRevision, setPlansRevision] = useState(0)
  const [selectedPlanId, setSelectedPlanId] = useState<string | null>(null)
  const [selectedPlan, setSelectedPlan] = useState<MealPlan | null>(null)
  const [planLoading, setPlanLoading] = useState(false)
  const [meals, setMeals] = useState<MealEntryPage>(mealPageDefaults)
  const [mealsPage, setMealsPage] = useState(1)
  const [mealDateFilter, setMealDateFilter] = useState('')
  const [detailsRevision, setDetailsRevision] = useState(0)
  const [error, setError] = useState<unknown>(null)
  const [pending, setPending] = useState(false)
  const [planFormOpen, setPlanFormOpen] = useState(false)
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null)
  const [planDraft, setPlanDraft] = useState<PlanDraft>(newPlanDraft)
  const [planErrors, setPlanErrors] = useState<Record<string, string>>({})
  const [mealFormOpen, setMealFormOpen] = useState(false)
  const [mealDraft, setMealDraft] = useState<MealDraft>(newMealDraft())
  const [mealErrors, setMealErrors] = useState<Record<string, string>>({})
  const [editingMealId, setEditingMealId] = useState<string | null>(null)
  const [confirmPlanDelete, setConfirmPlanDelete] = useState(false)
  const [confirmMealDeleteId, setConfirmMealDeleteId] = useState<string | null>(null)

  useEffect(() => {
    let active = true
    mealPlanApi
      .listPlans(accessToken, plansPage, pageSize)
      .then((response) => {
        if (!active) {
          return
        }
        setPlans(response)
        setSelectedPlanId((current) => current || response.items[0]?.id || null)
      })
      .catch((requestError: unknown) => {
        if (active) {
          if (requestError instanceof ApiError && requestError.status === 401) {
            void onSessionExpired()
          } else {
            setError(requestError)
          }
        }
      })
      .finally(() => {
        if (active) {
          setPlansLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [accessToken, plansPage, plansRevision, onSessionExpired])

  useEffect(() => {
    if (!selectedPlanId) {
      return
    }

    let active = true
    Promise.all([
      mealPlanApi.getPlan(accessToken, selectedPlanId),
      mealPlanApi.listMeals(accessToken, selectedPlanId, mealsPage, pageSize, mealDateFilter || undefined),
    ])
      .then(([plan, mealPage]) => {
        if (!active) {
          return
        }
        setSelectedPlan(plan)
        setMeals(mealPage)
      })
      .catch((requestError: unknown) => {
        if (active) {
          if (requestError instanceof ApiError && requestError.status === 401) {
            void onSessionExpired()
          } else {
            setError(requestError)
          }
        }
      })
      .finally(() => {
        if (active) {
          setPlanLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [
    accessToken,
    selectedPlanId,
    mealsPage,
    mealDateFilter,
    detailsRevision,
    onSessionExpired,
  ])

  const refresh = () => {
    setError(null)
    setPlansLoading(true)
    setPlanLoading(selectedPlanId !== null)
    setPlansRevision((revision) => revision + 1)
    setDetailsRevision((revision) => revision + 1)
  }

  const changePlansPage = (page: number) => {
    setError(null)
    setPlansLoading(true)
    setPlansPage(page)
  }

  const handleRequestError = (requestError: unknown) => {
    if (requestError instanceof ApiError && requestError.status === 401) {
      void onSessionExpired()
      return
    }
    setError(requestError)
  }

  const closePlanForm = () => {
    setPlanFormOpen(false)
    setEditingPlanId(null)
    setPlanErrors({})
  }

  const openNewPlanForm = () => {
    setEditingPlanId(null)
    setPlanDraft(newPlanDraft())
    setPlanErrors({})
    setPlanFormOpen(true)
    setError(null)
  }

  const openPlanEditForm = () => {
    if (!selectedPlan) {
      return
    }
    setEditingPlanId(selectedPlan.id)
    setPlanDraft({
      name: selectedPlan.name,
      startDate: selectedPlan.startDate,
      endDate: selectedPlan.endDate,
    })
    setPlanErrors({})
    setPlanFormOpen(true)
    setError(null)
  }

  const savePlan = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const validation = planValidation(planDraft)
    setPlanErrors(validation)
    if (Object.keys(validation).length > 0) {
      return
    }

    setPending(true)
    setError(null)
    try {
      const saved = editingPlanId && selectedPlan
        ? await mealPlanApi.updatePlan(accessToken, editingPlanId, {
            ...planDraft,
            name: planDraft.name.trim(),
          }, selectedPlan.version)
        : await mealPlanApi.createPlan(accessToken, {
            ...planDraft,
            name: planDraft.name.trim(),
          })
      setPlanFormOpen(false)
      setSelectedPlanId(saved.id)
      setSelectedPlan(saved)
      setPlanLoading(true)
      setPlansLoading(true)
      setPlansPage(1)
      setPlansRevision((revision) => revision + 1)
      setDetailsRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const changePlanStatus = async (status: MealPlanStatus) => {
    if (!selectedPlan) {
      return
    }
    setPending(true)
    setError(null)
    try {
      const updated = await mealPlanApi.changePlanStatus(
        accessToken,
        selectedPlan.id,
        status,
        selectedPlan.version,
      )
      setSelectedPlan(updated)
      setPlanLoading(true)
      setPlansLoading(true)
      setPlansRevision((revision) => revision + 1)
      setDetailsRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const deletePlan = async () => {
    if (!selectedPlan) {
      return
    }
    setPending(true)
    setError(null)
    try {
      await mealPlanApi.deletePlan(accessToken, selectedPlan.id, selectedPlan.version)
      const nextCount = Math.max(0, plans.totalCount - 1)
      const lastPage = Math.max(1, Math.ceil(nextCount / plans.pageSize))
      setSelectedPlanId(null)
      setSelectedPlan(null)
      setMeals(mealPageDefaults)
      setPlanLoading(false)
      setConfirmPlanDelete(false)
      setPlansLoading(true)
      setPlansPage((page) => Math.min(page, lastPage))
      setPlansRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const openNewMealForm = () => {
    setEditingMealId(null)
    setMealDraft(newMealDraft(selectedPlan?.startDate))
    setMealErrors({})
    setMealFormOpen(true)
    setError(null)
  }

  const openMealEditForm = (meal: MealEntry) => {
    setEditingMealId(meal.id)
    setMealDraft({
      date: meal.date,
      slot: meal.slot,
      title: meal.title,
      recipeId: meal.recipeId || '',
      servings: meal.servings,
    })
    setMealErrors({})
    setMealFormOpen(true)
    setError(null)
  }

  const closeMealForm = () => {
    setMealFormOpen(false)
    setEditingMealId(null)
    setMealErrors({})
  }

  const saveMeal = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selectedPlan) {
      return
    }
    const validation = mealValidation(mealDraft, selectedPlan, meals.items, editingMealId)
    setMealErrors(validation)
    if (Object.keys(validation).length > 0) {
      return
    }

    setPending(true)
    setError(null)
    try {
      const saved = editingMealId
        ? await mealPlanApi.updateMeal(accessToken, selectedPlan, editingMealId, {
            ...mealDraft,
            title: mealDraft.title.trim(),
          })
        : await mealPlanApi.createMeal(accessToken, selectedPlan, {
            ...mealDraft,
            title: mealDraft.title.trim(),
          })
      setSelectedPlan((current) => current ? { ...current, version: saved.version } : current)
      setMealFormOpen(false)
      setEditingMealId(null)
      setPlanLoading(true)
      setDetailsRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const changeMealStatus = async (meal: MealEntry, status: MealEntryStatus) => {
    if (!selectedPlan) {
      return
    }
    setPending(true)
    setError(null)
    try {
      const updated = await mealPlanApi.changeMealStatus(accessToken, selectedPlan, meal.id, status)
      setSelectedPlan((current) => current ? { ...current, version: updated.version } : current)
      setPlanLoading(true)
      setDetailsRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const deleteMeal = async (mealId: string) => {
    if (!selectedPlan) {
      return
    }
    setPending(true)
    setError(null)
    try {
      await mealPlanApi.deleteMeal(accessToken, selectedPlan, mealId)
      setSelectedPlan(null)
      setPlanLoading(true)
      setMealsPage((page) =>
        Math.min(page, Math.max(1, Math.ceil((meals.totalCount - 1) / meals.pageSize))),
      )
      setConfirmMealDeleteId(null)
      setDetailsRevision((revision) => revision + 1)
    } catch (requestError) {
      handleRequestError(requestError)
    } finally {
      setPending(false)
    }
  }

  const selectPlan = (planId: string) => {
    if (planId === selectedPlanId) {
      return
    }
    setSelectedPlanId(planId)
    setSelectedPlan(null)
    setPlanLoading(true)
    setMealDateFilter('')
    setMealsPage(1)
    setPlanFormOpen(false)
    setEditingPlanId(null)
    setMealFormOpen(false)
    setError(null)
  }

  const detailPending = Boolean(
    selectedPlanId && (planLoading || (!selectedPlan && !error)),
  )
  const currentPlanPageCount = pageCount(plans.totalCount, plans.pageSize)
  const currentMealPageCount = pageCount(meals.totalCount, meals.pageSize)
  const planIsTerminal =
    selectedPlan?.status === 'Completed' || selectedPlan?.status === 'Cancelled'

  return (
    <div className="meal-planning-feature">
      <section className="workspace">
        <section className="page-heading">
          <div>
            <p className="eyebrow">MAKE ROOM FOR GOOD FOOD</p>
            <h1>Your meal plans</h1>
            <p className="muted">Build a plan, make it yours, and keep track as you go.</p>
          </div>
          <button className="button button-primary" onClick={openNewPlanForm} type="button">
            <span aria-hidden="true">＋</span> New plan
          </button>
        </section>

        <ApiAlert error={error} onRefresh={refresh} />

        <div className="planner-layout">
          <aside className="plan-rail" aria-label="Meal plans">
            <div className="rail-heading">
              <h2>Plans</h2>
              <span className="count-badge">{plans.totalCount}</span>
            </div>
            {plansLoading ? (
              <p className="muted" role="status">Loading your plans…</p>
            ) : plans.items.length === 0 ? (
              <div className="empty-rail">
                <span className="empty-icon" aria-hidden="true">✳</span>
                <p>No plans yet.</p>
                <button className="text-button" onClick={openNewPlanForm} type="button">
                  Create your first plan
                </button>
              </div>
            ) : (
              <ul className="plan-list">
                {plans.items.map((plan) => (
                  <li key={plan.id}>
                    <button
                      className={`plan-link${plan.id === selectedPlanId ? ' is-selected' : ''}`}
                      aria-current={plan.id === selectedPlanId ? 'page' : undefined}
                      onClick={() => selectPlan(plan.id)}
                      type="button"
                    >
                      <span className="plan-link-name">{plan.name}</span>
                      <span className="plan-link-date">
                        {formatDate(plan.startDate)} – {formatDate(plan.endDate)}
                      </span>
                      <span className={`status-pill status-${plan.status.toLowerCase()}`}>
                        {plan.status}
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {plans.totalCount > plans.pageSize && (
              <Pagination
                label="Plans"
                page={plans.page}
                pages={currentPlanPageCount}
                onPageChange={changePlansPage}
              />
            )}
          </aside>

          <section className="plan-content" aria-label="Selected meal plan" aria-busy={detailPending}>
            {planFormOpen && (
              <PlanForm
                draft={planDraft}
                errors={planErrors}
                editing={editingPlanId !== null}
                pending={pending}
                onChange={setPlanDraft}
                onCancel={closePlanForm}
                onSubmit={savePlan}
              />
            )}

            {!planFormOpen && (detailPending || (plansLoading && !selectedPlanId)) && (
              <div className="loading-panel" role="status">Loading plan and meals…</div>
            )}

            {!planFormOpen && !plansLoading && !detailPending && !selectedPlan && (
              <div className="welcome-panel">
                <div className="welcome-art" aria-hidden="true">✳</div>
                <p className="eyebrow">A LITTLE PLANNING GOES A LONG WAY</p>
                <h2>{plans.totalCount ? 'Choose a plan to get started' : 'Start with a plan'}</h2>
                <p className="muted">
                  Set your dates, add meals, and keep your week flexible. Your plans are private to
                  your account.
                </p>
                <button className="button button-primary" onClick={openNewPlanForm} type="button">
                  Create a meal plan
                </button>
              </div>
            )}

            {!planFormOpen && !detailPending && selectedPlan && (
              <>
                <section className="plan-overview" aria-labelledby="selected-plan-title">
                  <div className="plan-overview-copy">
                    <div className="title-line">
                      <p className="eyebrow">YOUR PLAN</p>
                      <span className={`status-pill status-${selectedPlan.status.toLowerCase()}`}>
                        {selectedPlan.status}
                      </span>
                    </div>
                    <h2 id="selected-plan-title">{selectedPlan.name}</h2>
                    <p className="date-range">
                      <span aria-hidden="true">◷</span>
                      {formatDate(selectedPlan.startDate)} – {formatDate(selectedPlan.endDate)}
                      <span className="date-note">Calendar dates · no timezone conversion</span>
                    </p>
                  </div>
                  <div className="plan-actions">
                    {!planIsTerminal && (
                      <button className="button button-secondary" onClick={openPlanEditForm} type="button">
                        Edit plan
                      </button>
                    )}
                    <button
                      className="button button-quiet"
                      onClick={() => setConfirmPlanDelete(true)}
                      type="button"
                    >
                      Delete
                    </button>
                  </div>
                  {confirmPlanDelete && (
                    <div className="confirm-row" role="alert">
                      <span>Delete this plan and all its meals?</span>
                      <button
                        className="button button-danger"
                        disabled={pending}
                        onClick={() => void deletePlan()}
                        type="button"
                      >
                        Delete plan
                      </button>
                      <button
                        className="button button-quiet"
                        onClick={() => setConfirmPlanDelete(false)}
                        type="button"
                      >
                        Keep plan
                      </button>
                    </div>
                  )}
                  {planTransitions[selectedPlan.status].length > 0 && (
                    <div className="lifecycle-row">
                      <span className="muted">Move plan to</span>
                      {planTransitions[selectedPlan.status].map((status) => (
                        <button
                          className="text-button"
                          disabled={pending}
                          key={status}
                          onClick={() => void changePlanStatus(status)}
                          type="button"
                        >
                          {status}
                        </button>
                      ))}
                      <span className="rule-hint">
                        Completed and cancelled plans are read-only.
                      </span>
                    </div>
                  )}
                </section>

                <section className="meals-section" aria-labelledby="meals-title">
                  <div className="section-heading">
                    <div>
                      <p className="eyebrow">THE GOOD STUFF</p>
                      <h2 id="meals-title">Planned meals</h2>
                    </div>
                    {!planIsTerminal && (
                      <button className="button button-primary" onClick={openNewMealForm} type="button">
                        <span aria-hidden="true">＋</span> Add meal
                      </button>
                    )}
                  </div>

                  <div className="meal-toolbar">
                    <label className="filter-field">
                      <span>Filter by date</span>
                      <input
                        aria-label="Filter meals by date"
                        max={selectedPlan.endDate}
                        min={selectedPlan.startDate}
                        onChange={(event) => {
                          setMealDateFilter(event.target.value)
                          setMealsPage(1)
                        }}
                        type="date"
                        value={mealDateFilter}
                      />
                    </label>
                    {mealDateFilter && (
                      <button
                        className="text-button"
                        onClick={() => setMealDateFilter('')}
                        type="button"
                      >
                        Clear date filter
                      </button>
                    )}
                    <p className="muted">
                      Dates are calendar days. Breakfast, lunch, and dinner allow one meal per day;
                      snacks are unlimited.
                    </p>
                  </div>

                  {mealFormOpen && !planIsTerminal && (
                    <MealForm
                      draft={mealDraft}
                      errors={mealErrors}
                      editing={editingMealId !== null}
                      endDate={selectedPlan.endDate}
                      pending={pending}
                      startDate={selectedPlan.startDate}
                      onChange={setMealDraft}
                      onCancel={closeMealForm}
                      onSubmit={saveMeal}
                    />
                  )}

                  {meals.items.length === 0 && !detailPending ? (
                    <div className="empty-meals">
                      <span className="empty-icon" aria-hidden="true">✳</span>
                      <h3>{mealDateFilter ? 'No meals on this date' : 'A blank slate'}</h3>
                      <p className="muted">
                        {mealDateFilter
                          ? 'Try another date or clear the filter.'
                          : 'Add a meal when you are ready. You can change plans as the week unfolds.'}
                      </p>
                      {!planIsTerminal && !mealDateFilter && (
                        <button className="text-button" onClick={openNewMealForm} type="button">
                          Add your first meal
                        </button>
                      )}
                    </div>
                  ) : meals.items.length > 0 ? (
                    <div className="table-scroll">
                      <table className="meal-table">
                        <thead>
                          <tr>
                            <th scope="col">Date</th>
                            <th scope="col">Meal</th>
                            <th scope="col">Slot</th>
                            <th scope="col">Servings</th>
                            <th scope="col">Progress</th>
                            <th scope="col"><span className="visually-hidden">Actions</span></th>
                          </tr>
                        </thead>
                        <tbody>
                          {meals.items.map((meal) => (
                            <tr key={meal.id}>
                              <td className="meal-date">{formatDate(meal.date)}</td>
                              <td>
                                <span className="meal-title">{meal.title}</span>
                                {meal.recipeId ? (
                                  <span className="recipe-reference">Recipe ID: {meal.recipeId}</span>
                                ) : (
                                  <span className="recipe-reference">No linked recipe</span>
                                )}
                              </td>
                              <td>{meal.slot}</td>
                              <td>{meal.servings}</td>
                              <td>
                                <span className={`status-pill status-${meal.status.toLowerCase()}`}>
                                  {meal.status === 'InProgress' ? 'In progress' : meal.status}
                                </span>
                              </td>
                              <td className="meal-row-actions">
                                {!planIsTerminal && meal.status === 'Planned' && (
                                  <>
                                    <button
                                      aria-label={`Edit ${meal.title}`}
                                      className="icon-button"
                                      onClick={() => openMealEditForm(meal)}
                                      type="button"
                                    >
                                      Edit
                                    </button>
                                    <button
                                      aria-label={`Remove ${meal.title}`}
                                      className="icon-button icon-button-danger"
                                      onClick={() => setConfirmMealDeleteId(meal.id)}
                                      type="button"
                                    >
                                      Remove
                                    </button>
                                  </>
                                )}
                                {!planIsTerminal &&
                                  mealTransitions[meal.status].map((status) => (
                                    <button
                                      aria-label={`${status === 'InProgress' ? 'Mark in progress' : `Mark ${status.toLowerCase()}`} ${meal.title}`}
                                      className="icon-button"
                                      disabled={pending}
                                      key={status}
                                      onClick={() => void changeMealStatus(meal, status)}
                                      type="button"
                                    >
                                      {status === 'InProgress'
                                        ? 'Start'
                                        : status === 'Completed'
                                          ? 'Complete'
                                          : 'Skip'}
                                    </button>
                                  ))}
                                {confirmMealDeleteId === meal.id && (
                                  <span className="inline-confirm" role="alert">
                                    Remove?
                                    <button
                                      disabled={pending}
                                      onClick={() => void deleteMeal(meal.id)}
                                      type="button"
                                    >
                                      Yes
                                    </button>
                                    <button
                                      onClick={() => setConfirmMealDeleteId(null)}
                                      type="button"
                                    >
                                      No
                                    </button>
                                  </span>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : null}

                  {meals.totalCount > meals.pageSize && (
                    <Pagination
                      label="Meals"
                      page={meals.page}
                      pages={currentMealPageCount}
                      onPageChange={setMealsPage}
                    />
                  )}
                </section>
              </>
            )}
          </section>
        </div>
      </section>
    </div>
  )
}

function Pagination({
  label,
  page,
  pages,
  onPageChange,
}: {
  label: string
  page: number
  pages: number
  onPageChange: (page: number) => void
}) {
  return (
    <nav className="pagination" aria-label={`${label} pagination`}>
      <button
        aria-label={`Previous ${label.toLowerCase()} page`}
        className="icon-button"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        type="button"
      >
        ←
      </button>
      <span>
        Page <strong>{page}</strong> of {pages}
      </span>
      <button
        aria-label={`Next ${label.toLowerCase()} page`}
        className="icon-button"
        disabled={page >= pages}
        onClick={() => onPageChange(page + 1)}
        type="button"
      >
        →
      </button>
    </nav>
  )
}

function FieldError({ children }: { children?: string }) {
  return children ? <span className="field-error" role="alert">{children}</span> : null
}

function PlanForm({
  draft,
  errors,
  editing,
  pending,
  onChange,
  onCancel,
  onSubmit,
}: {
  draft: PlanDraft
  errors: Record<string, string>
  editing: boolean
  pending: boolean
  onChange: (draft: PlanDraft) => void
  onCancel: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return (
    <form className="editor-card" onSubmit={onSubmit} noValidate>
      <div className="section-heading">
        <div>
          <p className="eyebrow">{editing ? 'MAKE A CHANGE' : 'A FRESH START'}</p>
          <h2>{editing ? 'Edit your plan' : 'Create a meal plan'}</h2>
        </div>
        <span className="form-mark" aria-hidden="true">✳</span>
      </div>
      <label className="form-field">
        <span>Plan name</span>
        <input
          autoComplete="off"
          maxLength={120}
          onChange={(event) => onChange({ ...draft, name: event.target.value })}
          required
          value={draft.name}
        />
        <span className="field-hint">Up to 120 characters.</span>
        <FieldError>{errors.name}</FieldError>
      </label>
      <div className="form-grid">
        <label className="form-field">
          <span>Start date</span>
          <input
            onChange={(event) => onChange({ ...draft, startDate: event.target.value })}
            required
            type="date"
            value={draft.startDate}
          />
        </label>
        <label className="form-field">
          <span>End date</span>
          <input
            onChange={(event) => onChange({ ...draft, endDate: event.target.value })}
            required
            type="date"
            value={draft.endDate}
          />
          <FieldError>{errors.endDate || errors.dates}</FieldError>
        </label>
      </div>
      <p className="field-hint">Dates are inclusive calendar dates; a plan can be up to 366 days.</p>
      <div className="form-actions">
        <button className="button button-primary" disabled={pending} type="submit">
          {pending ? 'Saving…' : editing ? 'Save changes' : 'Create plan'}
        </button>
        <button className="button button-quiet" onClick={onCancel} type="button">
          Cancel
        </button>
      </div>
    </form>
  )
}

function MealForm({
  draft,
  errors,
  editing,
  startDate,
  endDate,
  pending,
  onChange,
  onCancel,
  onSubmit,
}: {
  draft: MealDraft
  errors: Record<string, string>
  editing: boolean
  startDate: string
  endDate: string
  pending: boolean
  onChange: (draft: MealDraft) => void
  onCancel: () => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return (
    <form className="editor-card meal-editor" onSubmit={onSubmit} noValidate>
      <div className="section-heading">
        <div>
          <p className="eyebrow">{editing ? 'UPDATE YOUR PLAN' : 'MAKE IT DELICIOUS'}</p>
          <h3>{editing ? 'Edit planned meal' : 'Add a planned meal'}</h3>
        </div>
      </div>
      <div className="form-grid form-grid-meal">
        <label className="form-field">
          <span>Meal name</span>
          <input
            autoComplete="off"
            maxLength={200}
            onChange={(event) => onChange({ ...draft, title: event.target.value })}
            required
            value={draft.title}
          />
          <FieldError>{errors.title}</FieldError>
        </label>
        <label className="form-field">
          <span>Date</span>
          <input
            max={endDate}
            min={startDate}
            onChange={(event) => onChange({ ...draft, date: event.target.value })}
            required
            type="date"
            value={draft.date}
          />
          <FieldError>{errors.date}</FieldError>
        </label>
        <label className="form-field">
          <span>Meal slot</span>
          <select
            onChange={(event) => onChange({ ...draft, slot: event.target.value as MealSlot })}
            value={draft.slot}
          >
            {mealSlots.map((slot) => <option key={slot} value={slot}>{slot}</option>)}
          </select>
          <FieldError>{errors.slot}</FieldError>
        </label>
        <label className="form-field">
          <span>Servings</span>
          <input
            max={100}
            min={1}
            onChange={(event) => onChange({ ...draft, servings: Number(event.target.value) })}
            required
            type="number"
            value={draft.servings}
          />
          <FieldError>{errors.servings}</FieldError>
        </label>
      </div>
      <label className="form-field">
        <span>Recipe ID <span className="optional">(optional)</span></span>
        <input
          autoComplete="off"
          onChange={(event) => onChange({ ...draft, recipeId: event.target.value })}
          placeholder="Paste a recipe UUID"
          value={draft.recipeId}
        />
        <span className="field-hint">
          The ID links public recipe data; the meal remains manageable if that recipe is unavailable.
        </span>
        <FieldError>{errors.recipeId}</FieldError>
      </label>
      <div className="form-actions">
        <button className="button button-primary" disabled={pending} type="submit">
          {pending ? 'Saving…' : editing ? 'Save meal' : 'Add meal'}
        </button>
        <button className="button button-quiet" onClick={onCancel} type="button">
          Cancel
        </button>
      </div>
    </form>
  )
}
