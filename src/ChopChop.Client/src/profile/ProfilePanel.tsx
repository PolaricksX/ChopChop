import { useAuth0 } from '@auth0/auth0-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import {
  createProfile,
  getMyProfile,
  getProfilePicture,
  uploadProfilePicture,
} from './profileApi'
import type { UserProfile } from './profileApi'

function messageFor(error: unknown) {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
}

type ProfilePanelProps = {
  registerSignOutCleanup?: (cleanup: (() => void) | null) => void
}

export function ProfilePanel({ registerSignOutCleanup }: ProfilePanelProps) {
  const { isAuthenticated, user } = useAuth0()
  const [loading, setLoading] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [creating, setCreating] = useState(false)
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [profileMissing, setProfileMissing] = useState(false)
  const [profileLoadFailed, setProfileLoadFailed] = useState(false)
  const [error, setError] = useState('')
  const [pictureUrl, setPictureUrl] = useState<string | null>(null)
  const [createUsername, setCreateUsername] = useState('')
  const [createEmail, setCreateEmail] = useState('')
  const [createDisplayName, setCreateDisplayName] = useState('')
  const currentPictureUrl = useRef<string | null>(null)

  const auth0Username =
    user?.nickname
    ?? (typeof user?.name === 'string' ? user.name : '')
    ?? (typeof user?.email === 'string' && user.email.includes('@') ? user.email.split('@')[0] : '')

  const replacePicture = useCallback((blob: Blob | null) => {
    const nextUrl = blob ? URL.createObjectURL(blob) : null
    if (currentPictureUrl.current) {
      URL.revokeObjectURL(currentPictureUrl.current)
    }
    currentPictureUrl.current = nextUrl
    setPictureUrl(nextUrl)
  }, [])

  const clearProfileState = useCallback(() => {
    setLoading(false)
    setUploading(false)
    setCreating(false)
    setProfileMissing(false)
    setProfileLoadFailed(false)
    setError('')
    setProfile(null)
    setCreateUsername('')
    setCreateEmail('')
    setCreateDisplayName('')
    replacePicture(null)
  }, [replacePicture])

  const loadProfile = useCallback(async (active: () => boolean = () => true) => {
    setLoading(true)
    setProfileMissing(false)
    setProfileLoadFailed(false)
    setError('')
    try {
      const result = await getMyProfile()
      if (!active()) return

      setProfile(result)
      if (!result) {
        setProfileMissing(true)
        replacePicture(null)
        return
      }

      try {
        const blob = await getProfilePicture(result.id)
        if (!active()) return
        replacePicture(blob)
      } catch (requestError: unknown) {
        if (active()) setError(messageFor(requestError))
      }
    } catch (requestError: unknown) {
      if (active()) {
        setProfile(null)
        setProfileLoadFailed(true)
        replacePicture(null)
        setError(messageFor(requestError))
      }
    } finally {
      if (active()) setLoading(false)
    }
  }, [replacePicture])

  useEffect(() => {
    if (!registerSignOutCleanup) return
    registerSignOutCleanup(clearProfileState)
    return () => registerSignOutCleanup(null)
  }, [clearProfileState, registerSignOutCleanup])

  useEffect(() => {
    return () => {
      if (currentPictureUrl.current) {
        URL.revokeObjectURL(currentPictureUrl.current)
      }
    }
  }, [])

  useEffect(() => {
    if (!isAuthenticated) {
      clearProfileState()
      return
    }

    setCreateUsername(auth0Username)
    setCreateEmail(user?.email ?? '')

    let active = true
    void loadProfile(() => active)

    return () => {
      active = false
    }
  }, [auth0Username, clearProfileState, isAuthenticated, loadProfile, user?.email])

  const upload = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file || !profile || uploading) return

    setUploading(true)
    setError('')
    try {
      await uploadProfilePicture(profile.id, file)
      const blob = await getProfilePicture(profile.id)
      replacePicture(blob)
      event.target.value = ''
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setUploading(false)
    }
  }

  const submitCreateProfile = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (creating) return

    setCreating(true)
    setError('')
    try {
      await createProfile({
        username: createUsername.trim(),
        email: createEmail.trim(),
        displayName: createDisplayName.trim() || undefined,
      })
      await loadProfile()
    } catch (requestError) {
      setError(messageFor(requestError))
    } finally {
      setCreating(false)
    }
  }

  if (!isAuthenticated) return null

  return (
    <section className="panel" aria-labelledby="profile-heading">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">YOUR ACCOUNT</p>
          <h2 id="profile-heading">My profile</h2>
        </div>
      </div>

      {error && <p role="alert" className="message error">{error}</p>}
      {loading && <p role="status">Loading profile…</p>}

      {!loading && profileLoadFailed && profile === null && (
        <button type="button" onClick={() => { void loadProfile() }}>Try again</button>
      )}

      {!loading && profileMissing && profile === null && (
        <>
          <p>No profile yet</p>
          <form onSubmit={(event) => { void submitCreateProfile(event) }}>
            <label htmlFor="profile-create-username">Username</label>
            <input
              id="profile-create-username"
              value={createUsername}
              onChange={(event) => setCreateUsername(event.target.value)}
              required
            />
            <label htmlFor="profile-create-email">Email</label>
            <input
              id="profile-create-email"
              type="email"
              value={createEmail}
              onChange={(event) => setCreateEmail(event.target.value)}
              required
            />
            <label htmlFor="profile-create-display-name">Display name (optional)</label>
            <input
              id="profile-create-display-name"
              value={createDisplayName}
              onChange={(event) => setCreateDisplayName(event.target.value)}
            />
            <button type="submit" disabled={creating}>{creating ? 'Creating profile…' : 'Create profile'}</button>
          </form>
        </>
      )}

      {!loading && profile && (
        <>
          <p><strong>Display name:</strong> {profile.displayName}</p>
          <p><strong>Username:</strong> {profile.username}</p>
          <p><strong>Email:</strong> {profile.email}</p>
          {pictureUrl
            ? <img src={pictureUrl} alt="Profile picture" />
            : <p>No profile picture yet.</p>}
          <label htmlFor="profile-picture-upload">Profile picture</label>
          <input
            id="profile-picture-upload"
            type="file"
            accept="image/jpeg,image/png"
            onChange={(event) => { void upload(event) }}
            disabled={uploading}
          />
        </>
      )}
    </section>
  )
}
