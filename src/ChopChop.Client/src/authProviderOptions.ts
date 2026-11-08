import type { Auth0ProviderWithConfigOptions } from '@auth0/auth0-react'

export const authProviderTokenOptions = {
  useRefreshTokens: true,
  useRefreshTokensFallback: false,
} satisfies Pick<Auth0ProviderWithConfigOptions, 'useRefreshTokens' | 'useRefreshTokensFallback'>
