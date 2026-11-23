import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuth0 } from '@auth0/auth0-react'
import { getApiAccessToken } from '../auth'
import { ProfilePanel } from './ProfilePanel'

vi.mock('@auth0/auth0-react', () => ({
  useAuth0: vi.fn(),
}))

vi.mock('../auth', () => ({
  getApiAccessToken: vi.fn(),
}))

describe('ProfilePanel', () => {
  const createObjectUrl = vi.fn()
  const revokeObjectUrl = vi.fn()

    type TestUser = { email: string; nickname: string; name: string }

    const auth0State: { isAuthenticated: boolean; user: TestUser | undefined } = {
        isAuthenticated: true,
        user: {
            email: 'taylor@example.com',
            nickname: 'tcook',
            name: 'Taylor Cook',
        },
    }

  beforeEach(() => {
    vi.mocked(useAuth0).mockImplementation(() => auth0State as never)
    vi.mocked(getApiAccessToken).mockReset().mockResolvedValue('top-secret-token')
    createObjectUrl.mockReset().mockReturnValue('blob:picture-url')
    revokeObjectUrl.mockReset()
    vi.stubGlobal('fetch', vi.fn())
    Object.defineProperty(URL, 'createObjectURL', {
      configurable: true,
      writable: true,
      value: createObjectUrl,
    })
    Object.defineProperty(URL, 'revokeObjectURL', {
      configurable: true,
      writable: true,
      value: revokeObjectUrl,
    })

    auth0State.isAuthenticated = true
    auth0State.user = {
      email: 'taylor@example.com',
      nickname: 'tcook',
      name: 'Taylor Cook',
    }
  })

  it('loads and displays profile name, username, and email', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))

    render(<ProfilePanel />)

    expect(await screen.findByText('Display name:')).toBeInTheDocument()
    expect(screen.getByText('Taylor Cook')).toBeInTheDocument()
    expect(screen.getByText('Username:')).toBeInTheDocument()
    expect(screen.getByText('tcook')).toBeInTheDocument()
    expect(screen.getByText('Email:')).toBeInTheDocument()
    expect(screen.getByText('taylor@example.com')).toBeInTheDocument()
  })

  it('shows "No profile yet" on /users/me 404 and skips picture request', async () => {
    vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 404 }))

    render(<ProfilePanel />)

    expect(await screen.findByText('No profile yet')).toBeInTheDocument()
    expect(screen.getByLabelText('Username')).toHaveValue('tcook')
    expect(screen.getByLabelText('Email')).toHaveValue('taylor@example.com')
    expect(vi.mocked(fetch)).toHaveBeenCalledTimes(1)
    const [url] = vi.mocked(fetch).mock.calls[0] as [URL, RequestInit]
    expect(new URL(url).pathname).toBe('/api/v1/users/me')
  })

  it('renders a profile picture from an object URL', async () => {
    createObjectUrl.mockReturnValue('blob:first-picture')
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['png'], { type: 'image/png' }), { status: 200 }))

    render(<ProfilePanel />)

    const image = await screen.findByRole('img', { name: 'Profile picture' })
    expect(image).toHaveAttribute('src', 'blob:first-picture')
    expect(createObjectUrl).toHaveBeenCalledTimes(1)
  })

  it('uploads multipart file, reloads picture, and revokes replaced object URL', async () => {
    createObjectUrl
      .mockReturnValueOnce('blob:old-picture')
      .mockReturnValueOnce('blob:new-picture')
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['old'], { type: 'image/png' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(new Blob(['new'], { type: 'image/png' }), { status: 200 }))

    render(<ProfilePanel />)
    await screen.findByRole('img', { name: 'Profile picture' })

    const file = new File(['file'], 'avatar.png', { type: 'image/png' })
    fireEvent.change(screen.getByLabelText('Profile picture'), {
      target: { files: [file] },
    })

    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledTimes(4))

    const [uploadUrl, uploadInit] = vi.mocked(fetch).mock.calls[2] as [URL, RequestInit]
    expect(new URL(uploadUrl).pathname).toBe('/api/v1/users/8d54a67a-f876-4215-a801-5c66426d0786/profile-picture')
    expect(uploadInit.method).toBe('PUT')
    const formData = uploadInit.body as FormData
    expect(formData.get('file')).toBe(file)
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:old-picture')
    expect(await screen.findByRole('img', { name: 'Profile picture' })).toHaveAttribute('src', 'blob:new-picture')
  })

  it('shows the server detail message when upload fails', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ detail: 'The profile picture is too large.' }),
        { status: 413, headers: { 'Content-Type': 'application/problem+json' } },
      ))

    render(<ProfilePanel />)
    await screen.findByText('No profile picture yet.')

    const file = new File(['file'], 'avatar.png', { type: 'image/png' })
    fireEvent.change(screen.getByLabelText('Profile picture'), {
      target: { files: [file] },
    })

    expect(await screen.findByRole('alert')).toHaveTextContent('The profile picture is too large.')
  })

  it('revokes the current object URL on unmount', async () => {
    createObjectUrl.mockReturnValue('blob:current-picture')
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['pic'], { type: 'image/png' }), { status: 200 }))

    const { unmount } = render(<ProfilePanel />)
    await screen.findByRole('img', { name: 'Profile picture' })

    unmount()

    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:current-picture')
  })

  it('sign-out cleanup revokes picture URL and clears loaded profile from the panel', async () => {
    createObjectUrl.mockReturnValue('blob:current-picture')
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['pic'], { type: 'image/png' }), { status: 200 }))

    let cleanup: (() => void) | null = null
    render(<ProfilePanel registerSignOutCleanup={(handler) => { cleanup = handler }} />)

    await screen.findByText('Taylor Cook')
    expect(screen.getByRole('img', { name: 'Profile picture' })).toHaveAttribute('src', 'blob:current-picture')

    act(() => cleanup?.())

    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:current-picture')
    await waitFor(() => expect(screen.queryByText('Taylor Cook')).toBeNull())
    expect(screen.queryByText('taylor@example.com')).toBeNull()
    expect(screen.queryByRole('img', { name: 'Profile picture' })).toBeNull()
    expect(screen.getByText('No profile yet')).toBeInTheDocument()
  })

  it('after sign-out and sign-in as another user, previous profile details are not shown', async () => {
    createObjectUrl
      .mockReturnValueOnce('blob:first-user')
      .mockReturnValueOnce('blob:second-user')
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['first'], { type: 'image/png' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: 'f6a6e9cf-d8c2-4b12-b2aa-4f82f80b88dc',
        displayName: 'Casey Chef',
        username: 'cchef',
        email: 'casey@example.com',
      })))
      .mockResolvedValueOnce(new Response(new Blob(['second'], { type: 'image/png' }), { status: 200 }))

    let cleanup: (() => void) | null = null
    const rendered = render(<ProfilePanel registerSignOutCleanup={(handler) => { cleanup = handler }} />)

    expect(await screen.findByText('Taylor Cook')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Profile picture' })).toHaveAttribute('src', 'blob:first-user')

    act(() => cleanup?.())
    auth0State.isAuthenticated = false
    auth0State.user = undefined
    rendered.rerender(<ProfilePanel registerSignOutCleanup={(handler) => { cleanup = handler }} />)

    auth0State.isAuthenticated = true
    auth0State.user = {
      email: 'casey@example.com',
      nickname: 'cchef',
      name: 'Casey Chef',
    }
    rendered.rerender(<ProfilePanel registerSignOutCleanup={(handler) => { cleanup = handler }} />)

    expect(await screen.findByText('Casey Chef')).toBeInTheDocument()
    expect(screen.getByText('casey@example.com')).toBeInTheDocument()
    expect(screen.queryByText('Taylor Cook')).toBeNull()
    expect(screen.queryByText('taylor@example.com')).toBeNull()
    expect(screen.getByRole('img', { name: 'Profile picture' })).toHaveAttribute('src', 'blob:second-user')
  })

  it('creates a profile from the no-profile form and then displays the loaded profile', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))

    render(<ProfilePanel />)

    expect(await screen.findByText('No profile yet')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Create profile' }))

    expect(await screen.findByText('Display name:')).toBeInTheDocument()
    expect(screen.getByText('Taylor Cook')).toBeInTheDocument()

    const [createUrl, createInit] = vi.mocked(fetch).mock.calls[1] as [URL, RequestInit]
    expect(new URL(createUrl).pathname).toBe('/api/v1/users')
    expect(createInit.method).toBe('POST')
  })

  it('shows problem detail when profile creation returns conflict', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(
        JSON.stringify({ detail: 'A profile with that username already exists.' }),
        { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
      ))

    render(<ProfilePanel />)

    expect(await screen.findByText('No profile yet')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Create profile' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('A profile with that username already exists.')
  })

  it('never places the access token in any request URL', async () => {
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: '8d54a67a-f876-4215-a801-5c66426d0786',
        displayName: 'Taylor Cook',
        username: 'tcook',
        email: 'taylor@example.com',
      })))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 404 }))

    render(<ProfilePanel />)
    await screen.findByText('No profile picture yet.')

    const file = new File(['file'], 'avatar.png', { type: 'image/png' })
    fireEvent.change(screen.getByLabelText('Profile picture'), {
      target: { files: [file] },
    })

    await waitFor(() => expect(vi.mocked(fetch)).toHaveBeenCalledTimes(4))

    for (const [requestUrl] of vi.mocked(fetch).mock.calls as [URL, RequestInit][]) {
      const value = String(requestUrl)
      expect(value).not.toContain('top-secret-token')
      expect(value.toLowerCase()).not.toContain('access_token')
    }
  })
})
