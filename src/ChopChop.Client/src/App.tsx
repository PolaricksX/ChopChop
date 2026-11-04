import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import type { User } from 'oidc-client-ts'
import {
  confirmGeneration,
  createGeneration,
  createReview,
  getGeneration,
  getModerationQueue,
  getReviews,
  moderateReview,
} from './api'
import type { Generation, Review } from './api'
import { userManager } from './auth'
import './App.css'

type Panel = 'recipe' | 'review' | 'moderation'

function App() {
  const [user, setUser] = useState<User | null>(null)
  const [authReady, setAuthReady] = useState(false)
  const [activePanel, setActivePanel] = useState<Panel>('recipe')
  const [generation, setGeneration] = useState<Generation | null>(null)
  const [prompt, setPrompt] = useState('')
  const [includePantry, setIncludePantry] = useState(false)
  const [pantryItems, setPantryItems] = useState('')
  const [recipeId, setRecipeId] = useState('')
  const [rating, setRating] = useState(5)
  const [reviewBody, setReviewBody] = useState('')
  const [suggestion, setSuggestion] = useState('')
  const [reviews, setReviews] = useState<Review[]>([])
  const [moderationQueue, setModerationQueue] = useState<Review[]>([])
  const [moderationReason, setModerationReason] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (window.location.pathname === '/auth/callback') {
      userManager.signinRedirectCallback()
        .then(() => window.location.replace('/'))
        .catch((callbackError: unknown) => {
          setError(errorMessage(callbackError))
          setAuthReady(true)
        })
      return
    }

    userManager.getUser()
      .then(setUser)
      .catch((authError: unknown) => setError(errorMessage(authError)))
      .finally(() => setAuthReady(true))
  }, [])

  const generationId = generation?.id
  const generationStatus = generation?.status

  useEffect(() => {
    if (!user?.access_token || !generationId || !generationStatus || !isPending(generationStatus)) return

    let active = true
    let timer: ReturnType<typeof setTimeout>
    const poll = async () => {
      try {
        const latest = await getGeneration(user.access_token, generationId)
        if (!active) return
        setGeneration(latest)
        if (isPending(latest.status)) timer = setTimeout(poll, 1800)
      } catch (pollError) {
        if (active) {
          setError(errorMessage(pollError))
          timer = setTimeout(poll, 3500)
        }
      }
    }

    timer = setTimeout(poll, 1200)
    return () => {
      active = false
      clearTimeout(timer)
    }
  }, [generationId, generationStatus, user?.access_token])

  const signIn = async () => {
    setError('')
    await userManager.signinRedirect()
  }

  const signOut = async () => {
    await userManager.signoutRedirect()
  }

  const submitGeneration = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!user) return
    setBusy(true)
    setError('')
    setNotice('')
    setGeneration(null)
    try {
      const result = await createGeneration(user.access_token, {
        prompt,
        includePantry,
        pantryItems: includePantry
          ? pantryItems.split(',').map((item) => item.trim()).filter(Boolean)
          : [],
      })
      setGeneration(result)
      setNotice('Your request was saved. Its status will update here.')
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const confirmDraft = async () => {
    if (!user || !generation) return
    setBusy(true)
    setError('')
    try {
      await confirmGeneration(user.access_token, generation.id)
      setGeneration({ ...generation, status: 'Confirmed' })
      setNotice('Draft confirmed. It remains labelled as AI-generated.')
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const submitReview = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!user) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      const created = await createReview(user.access_token, {
        recipeId,
        rating,
        body: reviewBody,
        suggestion: suggestion.trim() || null,
      })
      setNotice(`Review ${created.id} submitted for moderation.`)
      setReviewBody('')
      setSuggestion('')
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const loadReviews = async () => {
    if (!user) return
    setError('')
    try {
      setReviews(await getReviews(user.access_token, recipeId))
    } catch (requestError) {
      setError(errorMessage(requestError))
    }
  }

  const loadModerationQueue = async () => {
    if (!user) return
    setError('')
    try {
      setModerationQueue(await getModerationQueue(user.access_token))
    } catch (requestError) {
      setError(errorMessage(requestError))
    }
  }

  const moderate = async (review: Review, status: 'Published' | 'Rejected') => {
    if (!user) return
    setBusy(true)
    setError('')
    try {
      await moderateReview(user.access_token, review.id, status, moderationReason)
      setModerationQueue((queue) => queue.filter((item) => item.id !== review.id))
      setNotice(`Review ${status.toLowerCase()}.`)
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  if (!authReady) {
    return <main className="shell"><p role="status">Loading your secure session…</p></main>
  }

  if (!user) {
    return (
      <main className="shell auth-shell">
        <p className="eyebrow">CHOPCHOP · YOUR KITCHEN COMPANION</p>
        <h1>Good food starts with what you have.</h1>
        <p className="lede">Create a recipe draft, share thoughtful feedback, and keep every AI suggestion in your control.</p>
        {error && <p role="alert" className="message error">{error}</p>}
        <button className="primary" onClick={signIn}>Sign in to continue</button>
        <p className="small-note">Secure sign-in uses your organization's OpenID Connect provider.</p>
      </main>
    )
  }

  return (
    <main className="shell">
      <header className="topbar">
        <a className="brand" href="/">chop<span>chop</span></a>
        <div className="account">
          <span>{user.profile.name ?? user.profile.preferred_username ?? 'Signed in'}</span>
          <button className="quiet" onClick={signOut}>Sign out</button>
        </div>
      </header>

      <section className="intro">
        <p className="eyebrow">MAKE IT. SHARE IT. MAKE IT BETTER.</p>
        <h1>A little inspiration<br />goes a long way.</h1>
        <p className="lede">Build a recipe around your idea or help the community make a good one even better.</p>
      </section>

      <nav className="tabs" aria-label="Cookbook tools">
        <button className={activePanel === 'recipe' ? 'tab active' : 'tab'} onClick={() => setActivePanel('recipe')}>AI recipe</button>
        <button className={activePanel === 'review' ? 'tab active' : 'tab'} onClick={() => setActivePanel('review')}>Reviews</button>
        <button className={activePanel === 'moderation' ? 'tab active' : 'tab'} onClick={() => setActivePanel('moderation')}>Moderation</button>
      </nav>

      {error && <p role="alert" className="message error">{error}</p>}
      {notice && <p role="status" className="message success">{notice}</p>}

      {activePanel === 'recipe' && (
        <section className="panel" aria-labelledby="recipe-heading">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">YOUR IDEA, YOUR CALL</p>
              <h2 id="recipe-heading">Create a recipe draft</h2>
            </div>
            <span className="pill">AI-assisted</span>
          </div>
          <form onSubmit={submitGeneration} className="form-grid">
            <label htmlFor="prompt">What would you like to cook?</label>
            <textarea id="prompt" required maxLength={2000} value={prompt} onChange={(event) => setPrompt(event.target.value)} placeholder="A weeknight dinner with a bright, herby sauce…" />
            <label className="check-row">
              <input type="checkbox" checked={includePantry} onChange={(event) => setIncludePantry(event.target.checked)} />
              Use pantry items I choose
            </label>
            {includePantry && (
              <>
                <label htmlFor="pantry">Pantry items <span className="label-note">(comma separated)</span></label>
                <input id="pantry" value={pantryItems} onChange={(event) => setPantryItems(event.target.value)} placeholder="chickpeas, spinach, lemon" />
              </>
            )}
            <button className="primary" disabled={busy || prompt.trim().length === 0}>
              {busy ? 'Sending…' : 'Generate a draft'}
            </button>
          </form>

          {generation && (
            <div className="generation-result">
              <p className="status-line">Request status: <strong>{generation.status}</strong></p>
              {generation.status === 'Failed' && (
                <p className="message error">We couldn't create this draft ({generation.failureCode ?? 'generation_failed'}). Try again later.</p>
              )}
              {generation.draft && (
                <article className="recipe-card">
                  <div className="draft-label">AI-GENERATED DRAFT · REVIEW BEFORE COOKING</div>
                  <h3>{generation.draft.title}</h3>
                  <p>{generation.draft.description}</p>
                  <div className="recipe-facts">
                    <span>{generation.draft.servings} servings</span>
                    <span>{generation.draft.prepMinutes} min prep</span>
                    <span>{generation.draft.cookMinutes} min cook</span>
                  </div>
                  <div className="recipe-columns">
                    <div>
                      <h4>Ingredients</h4>
                      <ul>{generation.draft.ingredients.map((item, index) => (
                        <li key={`${item.name}-${index}`}>{item.quantity} {item.unit} {item.name}</li>
                      ))}</ul>
                    </div>
                    <div>
                      <h4>Method</h4>
                      <ol>{generation.draft.steps.map((step, index) => <li key={index}>{step}</li>)}</ol>
                    </div>
                  </div>
                  <p><strong>Allergens identified:</strong> {generation.draft.allergens.length ? generation.draft.allergens.join(', ') : 'None identified by the generator; verify every ingredient label.'}</p>
                  <p className="small-note">Nutrition per serving is an estimate: {generation.draft.nutrition.calories} kcal, {generation.draft.nutrition.proteinGrams}g protein, {generation.draft.nutrition.carbohydrateGrams}g carbohydrates, {generation.draft.nutrition.fatGrams}g fat.</p>
                  <p className="disclaimer">AI-generated recipes can be inaccurate. Check ingredients, allergens, cooking temperatures, and food-safety guidance independently. This is not medical or dietary advice.</p>
                  {generation.status === 'Completed' && (
                    <button className="primary" disabled={busy} onClick={confirmDraft}>Confirm this draft</button>
                  )}
                  {generation.status === 'Confirmed' && <p className="confirmed">Draft confirmed by you.</p>}
                </article>
              )}
            </div>
          )}
        </section>
      )}

      {activePanel === 'review' && (
        <section className="panel" aria-labelledby="review-heading">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">KIND, USEFUL FEEDBACK</p>
              <h2 id="review-heading">Share a recipe review</h2>
            </div>
            <span className="pill">Photos not enabled</span>
          </div>
          <p className="section-note">Use the recipe ID from the cookbook. New reviews are private until a moderator approves them.</p>
          <form className="form-grid" onSubmit={submitReview}>
            <label htmlFor="recipe-id">Recipe ID</label>
            <input id="recipe-id" required value={recipeId} onChange={(event) => setRecipeId(event.target.value)} placeholder="Recipe catalog ID" />
            <label htmlFor="rating">Your rating</label>
            <select id="rating" value={rating} onChange={(event) => setRating(Number(event.target.value))}>
              {[5, 4, 3, 2, 1].map((value) => <option key={value} value={value}>{value} / 5</option>)}
            </select>
            <label htmlFor="review">What worked for you?</label>
            <textarea id="review" required maxLength={2000} value={reviewBody} onChange={(event) => setReviewBody(event.target.value)} />
            <label htmlFor="suggestion">A helpful suggestion <span className="label-note">(optional)</span></label>
            <textarea id="suggestion" maxLength={1000} value={suggestion} onChange={(event) => setSuggestion(event.target.value)} />
            <button className="primary" disabled={busy}>Submit for moderation</button>
          </form>
          <div className="feed-header">
            <h3>Published reviews</h3>
            <button className="secondary" disabled={!recipeId} onClick={loadReviews}>Load reviews</button>
          </div>
          <ReviewList reviews={reviews} />
        </section>
      )}

      {activePanel === 'moderation' && (
        <section className="panel" aria-labelledby="moderation-heading">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">KEEP THE COOKBOOK HELPFUL</p>
              <h2 id="moderation-heading">Review moderation</h2>
            </div>
            <button className="secondary" onClick={loadModerationQueue}>Refresh queue</button>
          </div>
          <p className="section-note">This area requires the review-moderator role. Reviews remain hidden until approved.</p>
          <label htmlFor="moderation-reason">Moderation reason</label>
          <input id="moderation-reason" required maxLength={500} value={moderationReason} onChange={(event) => setModerationReason(event.target.value)} placeholder="Brief reason for the decision" />
          <div className="review-list">
            {moderationQueue.map((review) => (
              <article className="review-item" key={review.id}>
                <div className="review-meta"><span>{review.rating} / 5</span><code>{review.recipeId}</code></div>
                <p>{review.body}</p>
                {review.suggestion && <p><strong>Suggestion:</strong> {review.suggestion}</p>}
                <div className="button-row">
                  <button className="primary" disabled={busy || !moderationReason.trim()} onClick={() => moderate(review, 'Published')}>Approve</button>
                  <button className="secondary" disabled={busy || !moderationReason.trim()} onClick={() => moderate(review, 'Rejected')}>Reject</button>
                </div>
              </article>
            ))}
            {moderationQueue.length === 0 && <p className="section-note">No reviews loaded or no pending reviews.</p>}
          </div>
        </section>
      )}

      <footer>
        <span>Cook with curiosity.</span>
        <span>Your data stays yours.</span>
      </footer>
    </main>
  )
}

function ReviewList({ reviews }: { reviews: Review[] }) {
  if (!reviews.length) return <p className="section-note">No published reviews to show yet.</p>
  return (
    <div className="review-list">
      {reviews.map((review) => (
        <article className="review-item" key={review.id}>
          <div className="review-meta"><span>{review.rating} / 5</span><time>{new Date(review.createdAtUtc).toLocaleDateString()}</time></div>
          <p>{review.body}</p>
          {review.suggestion && <p><strong>Suggestion:</strong> {review.suggestion}</p>}
        </article>
      ))}
    </div>
  )
}

function isPending(status: Generation['status']) {
  return status === 'Queued' || status === 'Processing'
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
}

export default App
