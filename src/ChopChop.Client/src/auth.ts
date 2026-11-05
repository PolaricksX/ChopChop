import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts'

const developmentAuth = import.meta.env.DEV && import.meta.env.VITE_DEV_AUTH === 'true'

const settings = {
  authority: import.meta.env.VITE_OIDC_AUTHORITY ?? 'http://localhost:8080/realms/chopchop',
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID ?? 'chopchop-web',
  redirect_uri: `${window.location.origin}/auth/callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code',
  scope: 'openid profile email recipes.read recipes.write recipes.publish cookbook.read cookbook.write users.read users.write mealplans.read mealplans.write',
  automaticSilentRenew: true,
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
}

// Development-only stand-in for Keycloak; APIs must run with Auth:DevelopmentAuthentication enabled.
class DevelopmentUserManager extends UserManager {
  override async signinRedirect(): Promise<void> {
    const user = new User({
      access_token: 'development',
      token_type: 'Bearer',
      scope: settings.scope,
      expires_at: Math.floor(Date.now() / 1000) + 24 * 60 * 60,
      profile: {
        sub: '00000000-0000-0000-0000-0000000000d0',
        iss: 'development',
        aud: 'development',
        exp: 0,
        iat: 0,
        preferred_username: 'dev-user',
        name: 'Development User',
        email: 'dev-user@example.test',
      },
    })
    await this.storeUser(user)
    window.location.replace('/')
  }

  override async signinSilent(): Promise<User | null> {
    return this.getUser()
  }

  override async signoutRedirect(): Promise<void> {
    await this.removeUser()
    window.location.replace('/')
  }
}

export const userManager = developmentAuth ? new DevelopmentUserManager(settings) : new UserManager(settings)

userManager.events.addSilentRenewError((error) => {
  console.error('OIDC silent token renewal failed.', error)
})
