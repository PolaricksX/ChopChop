# ChopChop Browser Client

The React and TypeScript client uses the Auth0 React SDK with authorization
code flow and PKCE. It is a public SPA and must not have a client secret.

1. In Auth0, configure the SPA's callback URL and logout URL as
   `http://localhost:5173/`, and its web origin as `http://localhost:5173`.
   The Vite dev command uses port `5173` with strict port selection to match.
2. Copy `.env.example` to `.env.local` and set `VITE_AUTH0_DOMAIN`,
   `VITE_AUTH0_CLIENT_ID`, and the API audiences you use. These are public client
   settings, not secrets. Create separate Auth0 APIs with the identifiers and
   delegated permissions documented in the
   [local development guide](../../../docs/local-development.md). Do not use
   the Auth0 Management API audience or grant management permissions to this
   SPA. Assign permissions by least-privilege user roles.
3. Configure each API with its matching Auth0 issuer and audience as
   described in the [local development guide](../../../docs/local-development.md).
   The APIs default to local Keycloak unless overridden with user secrets.
4. Run `npm ci`, then `npm run dev`.

The client supports recipe generation/status/confirmation, review submission
and reading, a moderator queue, cookbook management, and meal planning. Photos
are intentionally unavailable.

The client uses cached access tokens or refresh tokens for each API. When an
API token requires interactive Auth0 authorization, the client offers an
**Authorize access** button. It opens an Auth0 popup so the current page and
unsubmitted form contents remain in place; retry the action afterward. Allow
popups for the local client. The silent iframe fallback is disabled because
blocked third-party cookies can cause it to clear the local sign-in session.
API permissions must also be assigned to the signed-in user in Auth0.

The [Meal Planning client guide](../../docs/meal-planning-client.md) covers the
Meal Planner API configuration and behavior.

Run `npm run build`, `npm test`, and `npm run lint` to validate client changes.
