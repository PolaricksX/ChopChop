import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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

  beforeEach(() => {
    vi.mocked(useAuth0).mockReturnValue({ isAuthenticated: true } as never)
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
