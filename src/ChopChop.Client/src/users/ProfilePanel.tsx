import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError } from '../api'
import {
  createUserProfile,
  deleteUserProfile,
  getUserProfile,
  updateUserProfile,
} from './api'
import { userProfileDeletedStorageKey, userProfileStorageKey } from './profileStorage'
import type { UserProfile, UserProfileInput } from './types'

type ProfileStatus = 'loading' | 'onboarding' | 'profile' | 'not-found' | 'load-error' | 'deleted' | 'storage-error'
type ProfileField = keyof UserProfileInput

type ProfilePanelProps = {
  accessToken: string
  subject: string
  initialUsername?: string
  initialEmail?: string
  initialDisplayName?: string
  onSignIn: () => void
}

const emptyErrors: Record<string, string[]> = {}
const storageUnavailableMessage = 'Browser storage is unavailable. Enable local storage to access or create your profile.'

export function ProfilePanel({
  accessToken,
  subject,
  initialUsername = '',
  initialEmail = '',
  initialDisplayName = '',
  onSignIn,
}: ProfilePanelProps) {
  const storageKey = userProfileStorageKey(subject)
  const deletedStorageKey = userProfileDeletedStorageKey(subject)
  const [initialStorage] = useState(() => {
    try {
      return {
        id: window.localStorage.getItem(storageKey),
        deleted: window.localStorage.getItem(deletedStorageKey) === 'true',
        unavailable: false,
      }
    } catch {
      return { id: null, deleted: false, unavailable: true }
    }
  })
  const [status, setStatus] = useState<ProfileStatus>(
    initialStorage.unavailable
      ? 'storage-error'
      : initialStorage.deleted
        ? 'deleted'
        : initialStorage.id
          ? 'loading'
          : 'onboarding',
  )
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [profileId, setProfileId] = useState(initialStorage.id ?? '')
  const [form, setForm] = useState<UserProfileInput>({
    username: initialUsername,
    email: initialEmail,
    displayName: initialDisplayName,
  })
  const [errorMessage, setErrorMessage] = useState(initialStorage.unavailable ? storageUnavailableMessage : '')
  const [noticeMessage, setNoticeMessage] = useState('')
  const [validationErrors, setValidationErrors] = useState(emptyErrors)
  const [correlationId, setCorrelationId] = useState<string>()
  const [busy, setBusy] = useState(false)
  const [conflict, setConflict] = useState(false)
  const createAttempt = useRef<{ signature: string; key: string } | undefined>(undefined)
  const inFlight = useRef(false)

  useEffect(() => {
    let active = true
    if (initialStorage.deleted || !initialStorage.id) {
      return () => { active = false }
    }

    getUserProfile(accessToken, initialStorage.id)
      .then((loadedProfile) => {
        if (!active) return
        setProfile(loadedProfile)
        setForm(profileInput(loadedProfile))
        setErrorMessage('')
        setStatus('profile')
      })
      .catch((loadError: unknown) => {
        if (!active) return
        setApiError(loadError, 'load')
        setStatus(loadError instanceof ApiError && loadError.status === 404 ? 'not-found' : 'load-error')
      })

    return () => { active = false }
  }, [accessToken, initialStorage.deleted, initialStorage.id])

  const changeField = (field: ProfileField, value: string) => {
    setForm((current) => ({ ...current, [field]: value }))
    setValidationErrors((current) => {
      if (!current[field]) return current
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  const submitCreate = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (inFlight.current) return
    inFlight.current = true
    setBusy(true)
    setErrorMessage('')
    setNoticeMessage('')
    setValidationErrors(emptyErrors)
    setCorrelationId(undefined)
    setConflict(false)

    const requestBody = normalizeProfileInput(form)
    const signature = JSON.stringify(requestBody)
    if (!createAttempt.current || createAttempt.current.signature !== signature) {
      createAttempt.current = { signature, key: crypto.randomUUID() }
    }

    try {
      const created = await createUserProfile(accessToken, requestBody, createAttempt.current.key)
      setProfile(created)
      setProfileId(created.id)
      setForm(profileInput(created))
      setStatus('profile')
      createAttempt.current = undefined
      try {
        window.localStorage.removeItem(deletedStorageKey)
        window.localStorage.setItem(storageKey, created.id)
      } catch {
        setErrorMessage('Your profile was created, but this browser could not save its ID for your next visit.')
      }
    } catch (createError: unknown) {
      setApiError(createError, 'create')
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  const submitUpdate = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!profile || inFlight.current) return
    inFlight.current = true
    setBusy(true)
    setErrorMessage('')
    setNoticeMessage('')
    setValidationErrors(emptyErrors)
    setCorrelationId(undefined)
    setConflict(false)
    try {
      const updated = await updateUserProfile(
        accessToken,
        profile.id,
        normalizeProfileInput(form),
        profile.version,
      )
      setProfile(updated)
      setForm(profileInput(updated))
    } catch (updateError: unknown) {
      setApiError(updateError, 'update')
      setConflict(updateError instanceof ApiError && updateError.status === 409)
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  const reloadLatest = async () => {
    if (!profile || inFlight.current) return
    inFlight.current = true
    setBusy(true)
    setErrorMessage('')
    setNoticeMessage('')
    setCorrelationId(undefined)
    try {
      const latest = await getUserProfile(accessToken, profile.id)
      setProfile(latest)
      setForm(profileInput(latest))
      setConflict(false)
      setNoticeMessage('The latest saved profile is loaded. Review it and submit your changes again.')
    } catch (reloadError: unknown) {
      setApiError(reloadError, 'load')
      if (reloadError instanceof ApiError && reloadError.status === 404) setStatus('not-found')
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  const retryLoad = async () => {
    if (!profileId || inFlight.current) return
    inFlight.current = true
    setBusy(true)
    setErrorMessage('')
    setNoticeMessage('')
    setCorrelationId(undefined)
    try {
      const loaded = await getUserProfile(accessToken, profileId)
      setProfile(loaded)
      setForm(profileInput(loaded))
      setStatus('profile')
    } catch (loadError: unknown) {
      setApiError(loadError, 'load')
      if (loadError instanceof ApiError && loadError.status === 404) setStatus('not-found')
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  const removeProfile = async () => {
    if (!profile || inFlight.current) return
    const confirmed = window.confirm('Delete your profile? This cannot be undone through the browser client.')
    if (!confirmed) return

    inFlight.current = true
    setBusy(true)
    setErrorMessage('')
    setNoticeMessage('')
    setCorrelationId(undefined)
    try {
      await deleteUserProfile(accessToken, profile.id)
      setProfile(null)
      setForm({ username: '', email: '', displayName: '' })
      setProfileId('')
      setStatus('deleted')
      try {
        window.localStorage.setItem(deletedStorageKey, 'true')
        window.localStorage.removeItem(storageKey)
      } catch {
        setErrorMessage('The profile was deleted, but this browser could not save its deleted state.')
      }
    } catch (deleteError: unknown) {
      setApiError(deleteError, 'delete')
    } finally {
      inFlight.current = false
      setBusy(false)
    }
  }

  return (
    <section className="panel" aria-labelledby="profile-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">YOUR ACCOUNT</p>
          <h2 id="profile-heading">Profile</h2>
        </div>
      </div>

      {status === 'loading' && <p role="status">Loading your profile…</p>}

      {status === 'onboarding' && (
        <>
          <p className="section-note">Create your profile with a username and email address. Your profile belongs to your signed-in account.</p>
          <ProfileForm
            form={form}
            errors={validationErrors}
            busy={busy}
            submitLabel="Create profile"
            onChange={changeField}
            onSubmit={submitCreate}
          />
        </>
      )}

      {status === 'profile' && profile && (
        <>
          <p className="section-note">Update your profile fields. Your profile ID and version are managed by the service.</p>
          <ProfileForm
            form={form}
            errors={validationErrors}
            busy={busy}
            submitLabel="Save changes"
            onChange={changeField}
            onSubmit={submitUpdate}
          />
          {conflict && <button className="secondary" disabled={busy} onClick={reloadLatest}>Reload latest profile</button>}
          <div className="profile-meta">
            <p>Created <time dateTime={profile.createdAt}>{new Date(profile.createdAt).toLocaleString()}</time></p>
            <p>Last updated <time dateTime={profile.updatedAt}>{new Date(profile.updatedAt).toLocaleString()}</time></p>
          </div>
          <button className="danger" disabled={busy} onClick={removeProfile}>Delete profile</button>
        </>
      )}

      {status === 'not-found' && (
        <p className="message error" role="alert">
          The saved profile could not be found. It may have been deleted or is not accessible to this account.
          The service has no profile recovery endpoint; the saved reference has not been replaced with other data.
        </p>
      )}

      {status === 'load-error' && (
        <div className="message error" role="alert">
          <p>{errorMessage || 'The profile could not be loaded.'}</p>
          <button className="secondary" disabled={busy} onClick={retryLoad}>Try again</button>
        </div>
      )}

      {status === 'deleted' && (
        <p className="message success" role="status">Your profile was deleted. The service does not support restoring a deleted profile.</p>
      )}

      {status === 'storage-error' && errorMessage && <p className="message error" role="alert">{errorMessage}</p>}

      {errorMessage && status !== 'storage-error' && status !== 'load-error' && status !== 'not-found' && (
        <p className="message error" role="alert">{errorMessage}</p>
      )}
      {noticeMessage && <p className="message success" role="status">{noticeMessage}</p>}

      {validationErrors['Idempotency-Key']?.map((message) => <p className="message error" key={message}>{message}</p>)}

      {errorMessage && /sign-in session/i.test(errorMessage) && (
        <button className="secondary" onClick={onSignIn}>Sign in again</button>
      )}
      {correlationId && <p className="small-note">Request reference: {correlationId}</p>}
    </section>
  )

  function setApiError(value: unknown, operation: 'load' | 'create' | 'update' | 'delete') {
    setValidationErrors(value instanceof ApiError ? value.validationErrors : emptyErrors)
    setCorrelationId(value instanceof ApiError ? value.correlationId : undefined)
    if (!(value instanceof ApiError)) {
      setErrorMessage('The User Service could not be reached. Check the connection and try again.')
      return
    }

    const messages: Record<number, string> = {
      400: 'Check the highlighted fields and try again.',
      401: 'Your sign-in session may have expired. Sign in again to continue.',
      403: 'Your account is not authorized for this profile action.',
      404: 'The saved profile could not be found or is not accessible to this account.',
      409: operation === 'create'
        ? 'A profile may already exist for this account, or the create request conflicted. The service has no endpoint to recover an unknown profile ID.'
        : 'The profile changed or a field is already in use.',
      429: 'Too many profile requests were made. Wait a moment and try again.',
    }
    setErrorMessage(messages[value.status] ?? (
      value.status >= 500
        ? 'The User Service is temporarily unavailable. Try again later.'
        : 'The profile request could not be completed. Try again.'
    ))
  }
}

function ProfileForm({
  form,
  errors,
  busy,
  submitLabel,
  onChange,
  onSubmit,
}: {
  form: UserProfileInput
  errors: Record<string, string[]>
  busy: boolean
  submitLabel: string
  onChange: (field: ProfileField, value: string) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return (
    <form className="form-grid" onSubmit={onSubmit}>
      <label htmlFor="profile-username">Username</label>
      <input
        id="profile-username"
        autoComplete="username"
        required
        minLength={3}
        maxLength={30}
        aria-describedby={errors.username ? 'profile-username-error' : undefined}
        value={form.username}
        onChange={(event) => onChange('username', event.target.value)}
      />
      {errors.username?.map((message) => <p className="field-error" id="profile-username-error" key={message}>{message}</p>)}

      <label htmlFor="profile-email">Email</label>
      <input
        id="profile-email"
        type="email"
        autoComplete="email"
        required
        maxLength={254}
        aria-describedby={errors.email ? 'profile-email-error' : undefined}
        value={form.email}
        onChange={(event) => onChange('email', event.target.value)}
      />
      {errors.email?.map((message) => <p className="field-error" id="profile-email-error" key={message}>{message}</p>)}

      <label htmlFor="profile-display-name">Display name <span className="label-note">(optional)</span></label>
      <input
        id="profile-display-name"
        autoComplete="name"
        maxLength={100}
        aria-describedby={errors.displayName ? 'profile-display-name-error' : undefined}
        value={form.displayName ?? ''}
        onChange={(event) => onChange('displayName', event.target.value)}
      />
      {errors.displayName?.map((message) => <p className="field-error" id="profile-display-name-error" key={message}>{message}</p>)}

      <button className="primary" disabled={busy}>
        {busy ? 'Saving…' : submitLabel}
      </button>
    </form>
  )
}

function profileInput(profile: UserProfile): UserProfileInput {
  return {
    username: profile.username,
    email: profile.email,
    displayName: profile.displayName ?? '',
  }
}

function normalizeProfileInput(input: UserProfileInput): UserProfileInput {
  const displayName = input.displayName?.trim()
  return {
    username: input.username.trim(),
    email: input.email.trim(),
    displayName: displayName || null,
  }
}
