import { useAuth0 } from '@auth0/auth0-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { ChangeEvent } from 'react'
import {
  getMyProfile,
  getProfilePicture,
  uploadProfilePicture,
} from './profileApi'
import type { UserProfile } from './profileApi'

function messageFor(error: unknown) {
  return error instanceof Error ? error.message : 'Something went wrong. Please try again.'
}

export function ProfilePanel() {
  const { isAuthenticated } = useAuth0()
  const [loading, setLoading] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [error, setError] = useState('')
  const [pictureUrl, setPictureUrl] = useState<string | null>(null)
  const currentPictureUrl = useRef<string | null>(null)

  const replacePicture = useCallback((blob: Blob | null) => {
    const nextUrl = blob ? URL.createObjectURL(blob) : null
    if (currentPictureUrl.current) {
      URL.revokeObjectURL(currentPictureUrl.current)
    }
    currentPictureUrl.current = nextUrl
    setPictureUrl(nextUrl)
  }, [])

  useEffect(() => {
    return () => {
      if (currentPictureUrl.current) {
        URL.revokeObjectURL(currentPictureUrl.current)
      }
    }
  }, [])

  useEffect(() => {
    if (!isAuthenticated) return

    let active = true
    setLoading(true)
    setError('')

    void getMyProfile()
      .then(async (result) => {
        if (!active) return
        setProfile(result)
        if (!result) {
          replacePicture(null)
          return
        }

        const blob = await getProfilePicture(result.id)
        if (!active) return
        replacePicture(blob)
      })
      .catch((requestError: unknown) => {
        if (active) setError(messageFor(requestError))
      })
      .finally(() => {
        if (active) setLoading(false)
      })

    return () => {
      active = false
    }
  }, [isAuthenticated, replacePicture])

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

      {!loading && profile === null && <p>No profile yet</p>}

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
