import { WebStorageStateStore, type UserManagerSettings } from 'oidc-client-ts'

export interface AuthConfiguration {
  authority: string
  clientId: string
  scope: string
  redirectUri: string
  postLogoutRedirectUri: string
}

export function createOidcSettings(configuration: AuthConfiguration): UserManagerSettings {
  return {
    authority: configuration.authority,
    client_id: configuration.clientId,
    response_type: 'code',
    scope: configuration.scope,
    redirect_uri: configuration.redirectUri,
    post_logout_redirect_uri: configuration.postLogoutRedirectUri,
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: false,
    monitorSession: false,
    loadUserInfo: false,
  }
}
