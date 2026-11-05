import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api'
import {
  createUserProfile,
  deleteUserProfile,
  getUserProfile,
  updateUserProfile,
} from './api'
import { ProfilePanel } from './ProfilePanel'
import { userProfileDeletedStorageKey, userProfileStorageKey } from './profileStorage'
import type { UserProfile } from './types'

vi.mock('./api', () => ({
  createUserProfile: vi.fn(),
  deleteUserProfile: vi.fn(),
  getUserProfile: vi.fn(),
  updateUserProfile: vi.fn(),
}))

const profile: UserProfile = {
  id: 'profile-id',
  username: 'alice',
  email: 'alice@example.com',
  displayName: null,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  version: 'version-one',
}

function renderPanel() {
  return render(
    <ProfilePanel
      accessToken="access-token"
      subject="subject-one"
      initialUsername="alice"
      initialEmail="alice@example.com"
      onSignIn={vi.fn()}
    />,
  )
}

beforeEach(() => {
  window.localStorage.clear()
  vi.mocked(createUserProfile).mockReset()
  vi.mocked(deleteUserProfile).mockReset()
  vi.mocked(getUserProfile).mockReset()
  vi.mocked(updateUserProfile).mockReset()
  vi.stubGlobal('crypto', { randomUUID: vi.fn().mockReturnValue('stable-key') })
  vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))
})

describe('ProfilePanel', () => {
  it('shows onboarding, creates a profile, and persists the returned ID', async () => {
    vi.mocked(createUserProfile).mockResolvedValue(profile)
    renderPanel()

    expect(await screen.findByText(/Create your profile with a username/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Create profile' }))

    await screen.findByRole('button', { name: 'Delete profile' })
    expect(createUserProfile).toHaveBeenCalledWith(
      'access-token',
      { username: 'alice', email: 'alice@example.com', displayName: null },
      'stable-key',
    )
    expect(window.localStorage.getItem(userProfileStorageKey('subject-one'))).toBe('profile-id')
    expect((screen.getByLabelText('Username') as HTMLInputElement).value).toBe('alice')
  })

  it('retrieves a saved profile and sends its latest version on update', async () => {
    window.localStorage.setItem(userProfileStorageKey('subject-one'), 'profile-id')
    vi.mocked(getUserProfile).mockResolvedValue(profile)
    vi.mocked(updateUserProfile).mockResolvedValue({ ...profile, username: 'alice-new', version: 'version-two' })
    renderPanel()

    fireEvent.change(await screen.findByLabelText('Username'), { target: { value: 'alice-new' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(updateUserProfile).toHaveBeenCalledWith(
      'access-token',
      'profile-id',
      { username: 'alice-new', email: 'alice@example.com', displayName: null },
      'version-one',
    ))
    await waitFor(() => expect((screen.getByLabelText('Username') as HTMLInputElement).value).toBe('alice-new'))
  })

  it('offers an explicit reload after a stale-version conflict', async () => {
    window.localStorage.setItem(userProfileStorageKey('subject-one'), 'profile-id')
    vi.mocked(getUserProfile)
      .mockResolvedValueOnce(profile)
      .mockResolvedValueOnce({ ...profile, username: 'latest' })
    vi.mocked(updateUserProfile).mockRejectedValue(new ApiError('Conflict', 409))
    renderPanel()

    fireEvent.change(await screen.findByLabelText('Username'), { target: { value: 'local-change' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Reload latest profile' }))

    expect(await screen.findByText(/latest saved profile is loaded/i)).toBeTruthy()
    expect((screen.getByLabelText('Username') as HTMLInputElement).value).toBe('latest')
  })

  it('asks before deleting and clears the saved reference after success', async () => {
    window.localStorage.setItem(userProfileStorageKey('subject-one'), 'profile-id')
    vi.mocked(getUserProfile).mockResolvedValue(profile)
    vi.mocked(deleteUserProfile).mockResolvedValue(undefined)
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete profile' }))

    expect(window.confirm).toHaveBeenCalledOnce()
    expect(await screen.findByText(/Your profile was deleted/)).toBeTruthy()
    expect(deleteUserProfile).toHaveBeenCalledWith('access-token', 'profile-id')
    expect(window.localStorage.getItem(userProfileStorageKey('subject-one'))).toBeNull()
    expect(window.localStorage.getItem(userProfileDeletedStorageKey('subject-one'))).toBe('true')
  })

  it('does not offer create onboarding after a deleted profile is revisited', async () => {
    window.localStorage.setItem(userProfileDeletedStorageKey('subject-one'), 'true')
    renderPanel()

    expect(await screen.findByText(/Your profile was deleted/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create profile' })).toBeNull()
    expect(getUserProfile).not.toHaveBeenCalled()
  })

  it('shows API validation errors by field without trusting problem detail text', async () => {
    vi.mocked(createUserProfile).mockRejectedValue(new ApiError(
      'Internal implementation detail',
      400,
      { username: ['Username must be 3-30 characters.'] },
      'correlation-one',
    ))
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Create profile' }))

    expect(await screen.findByText('Username must be 3-30 characters.')).toBeTruthy()
    expect(screen.queryByText('Internal implementation detail')).toBeNull()
    expect(screen.getByText('Request reference: correlation-one')).toBeTruthy()
  })

  it('offers sign-in again after an expired-session response', async () => {
    const onSignIn = vi.fn()
    vi.mocked(createUserProfile).mockRejectedValue(new ApiError('Expired', 401))
    render(
      <ProfilePanel
        accessToken="token"
        subject="subject-one"
        initialUsername="alice"
        initialEmail="alice@example.com"
        onSignIn={onSignIn}
      />,
    )

    fireEvent.click(await screen.findByRole('button', { name: 'Create profile' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Sign in again' }))

    expect(onSignIn).toHaveBeenCalledOnce()
  })

  it.each([
    [403, 'not authorized'],
    [409, 'profile may already exist'],
    [429, 'Too many profile requests'],
    [503, 'temporarily unavailable'],
  ])('shows a clear message for HTTP %s', async (status, message) => {
    vi.mocked(createUserProfile).mockRejectedValue(new ApiError('Do not show details', status))
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Create profile' }))

    expect(await screen.findByText(new RegExp(message, 'i'))).toBeTruthy()
    expect(screen.queryByText('Do not show details')).toBeNull()
  })

  it('does not replace a missing saved profile with onboarding or fake data', async () => {
    window.localStorage.setItem(userProfileStorageKey('subject-one'), 'missing-id')
    vi.mocked(getUserProfile).mockRejectedValue(new ApiError('Not found', 404))
    renderPanel()

    expect(await screen.findByText(/The saved profile could not be found/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create profile' })).toBeNull()
    expect(window.localStorage.getItem(userProfileStorageKey('subject-one'))).toBe('missing-id')
  })

  it('reuses an idempotency key when retrying the same create request', async () => {
    vi.mocked(createUserProfile)
      .mockRejectedValueOnce(new ApiError('Unavailable', 503))
      .mockResolvedValueOnce(profile)
    renderPanel()

    fireEvent.click(await screen.findByRole('button', { name: 'Create profile' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Create profile' }))

    await screen.findByRole('button', { name: 'Delete profile' })
    expect(createUserProfile).toHaveBeenNthCalledWith(
      1,
      'access-token',
      { username: 'alice', email: 'alice@example.com', displayName: null },
      'stable-key',
    )
    expect(createUserProfile).toHaveBeenNthCalledWith(
      2,
      'access-token',
      { username: 'alice', email: 'alice@example.com', displayName: null },
      'stable-key',
    )
  })

  it('ignores a second submit while profile creation is in progress', async () => {
    let finishCreate!: (created: UserProfile) => void
    vi.mocked(createUserProfile).mockReturnValue(new Promise((resolve) => {
      finishCreate = resolve
    }))
    renderPanel()

    const button = await screen.findByRole('button', { name: 'Create profile' })
    fireEvent.click(button)
    fireEvent.click(button)

    expect(createUserProfile).toHaveBeenCalledOnce()
    finishCreate(profile)
    await screen.findByRole('button', { name: 'Delete profile' })
  })
})
