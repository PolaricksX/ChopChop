import { Auth0Client } from '@auth0/auth0-spa-js'
import type { Auth0ProviderOptions } from '@auth0/auth0-react'
import type { ReactElement } from 'react'
import { afterEach, expect, it, vi } from 'vitest'

const render = vi.hoisted(() => vi.fn())
vi.mock('react-dom/client', () => ({ createRoot: () => ({ render }) }))
vi.mock('./App.tsx', () => ({ default: () => null }))

afterEach(() => {
  vi.unstubAllEnvs()
  document.body.innerHTML = ''
})

it('does not add Cookbook permissions to Pantry token requests', async () => {
  vi.stubEnv('VITE_AUTH0_COOKBOOK_AUDIENCE', 'https://api.chopchop.local/cookbook')
  document.body.innerHTML = '<div id="root"></div>'
  await import('./main')

  const tree = render.mock.calls[0][0] as ReactElement<{
    children: ReactElement<Auth0ProviderOptions>
  }>
  const options = tree.props.children.props
  if (!('domain' in options)) throw new Error('Expected Auth0 provider configuration')
  const client = new Auth0Client(options)
  const request = vi.fn().mockResolvedValue(undefined)
  Reflect.set(client, '_getTokenSilently', request)

  await client.getTokenSilently({
    authorizationParams: {
      audience: 'https://api.chopchop.local/pantry',
      scope: 'pantry.read',
    },
  })

  const scopes = request.mock.calls[0][0].authorizationParams.scope.split(' ')
  expect(scopes).toContain('pantry.read')
  expect(scopes).not.toContain('cookbook.read')
})
