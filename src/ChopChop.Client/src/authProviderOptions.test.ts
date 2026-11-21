import { expect, it } from 'vitest'
import { authProviderTokenOptions } from './authProviderOptions'

it('does not use the silent iframe fallback when a service refresh token is missing', () => {
  expect(authProviderTokenOptions.useRefreshTokens).toBe(true)
  expect(authProviderTokenOptions.useRefreshTokensFallback).toBe(false)
})
