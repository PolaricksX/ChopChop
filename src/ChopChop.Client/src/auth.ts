type TokenOptions = {
  authorizationParams: { audience: string; scope: string }
  cacheMode?: 'off'
}

type TokenGetter = (options: TokenOptions) => Promise<string | undefined>
export type ApiName = 'ai' | 'review' | 'recipes' | 'cookbook'
export type ApiPermission =
  | 'recipe-generations.create'
  | 'recipe-generations.read'
  | 'recipe-generations.confirm'
  | 'reviews.create'
  | 'reviews.read'
  | 'reviews.write'
  | 'reviews.moderate'
  | 'recipes.read'
  | 'recipes.write'
  | 'recipes.publish'
  | 'cookbook.read'
  | 'cookbook.write'

let tokenGetter: TokenGetter | undefined

export function setTokenGetter(getter: TokenGetter) {
  tokenGetter = getter
}

export async function getApiAccessToken(
  api: ApiName,
  permission: ApiPermission,
  forceRefresh = false,
): Promise<string> {
  const apiAuth: Record<ApiName, { audience: string | undefined; permissions: ApiPermission[] }> = {
    ai: {
      audience: import.meta.env.VITE_AUTH0_AI_RECIPE_AUDIENCE,
      permissions: ['recipe-generations.create', 'recipe-generations.read', 'recipe-generations.confirm'],
    },
    review: {
      audience: import.meta.env.VITE_AUTH0_INTERACTIVE_REVIEW_AUDIENCE,
      permissions: ['reviews.create', 'reviews.read', 'reviews.write', 'reviews.moderate'],
    },
    recipes: {
      audience: import.meta.env.VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE,
      permissions: ['recipes.read', 'recipes.write', 'recipes.publish'],
    },
    cookbook: {
      audience: import.meta.env.VITE_AUTH0_COOKBOOK_AUDIENCE,
      permissions: ['cookbook.read', 'cookbook.write'],
    },
  }
  const { audience, permissions } = apiAuth[api]
  if (!permissions.includes(permission)) {
    throw new Error(`Permission ${permission} is not valid for the ${api} API.`)
  }
  if (!audience) {
    const envVariable = {
      ai: 'VITE_AUTH0_AI_RECIPE_AUDIENCE',
      review: 'VITE_AUTH0_INTERACTIVE_REVIEW_AUDIENCE',
      recipes: 'VITE_AUTH0_RECIPE_MANAGEMENT_AUDIENCE',
      cookbook: 'VITE_AUTH0_COOKBOOK_AUDIENCE',
    }[api]
    throw new Error(`API access is not configured. Set ${envVariable} in .env.local.`)
  }
  if (!tokenGetter) {
    throw new Error('Auth0 is not ready to provide an API access token.')
  }

  const token = await tokenGetter({
    authorizationParams: { audience, scope: permission },
    ...(forceRefresh ? { cacheMode: 'off' } : {}),
  })
  if (!token) {
    throw new Error('Auth0 did not return an access token for the configured API audience.')
  }
  return token
}
