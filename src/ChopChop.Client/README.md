# ChopChop Browser Client

The React and TypeScript client uses OIDC authorization-code flow with PKCE.
It stores the OIDC user session in `sessionStorage` and has no client secret.

1. Follow the [local development guide](../../../docs/local-development.md)
   to start Keycloak and the APIs.
2. Copy `.env.example` to `.env.local` if local service URLs need overriding.
3. Run `npm ci`, then `npm run dev`.

The client supports recipe generation/status/confirmation, review submission
and reading, a moderator queue, and User Service profile management. Photos
are intentionally unavailable.

Run `npm run build`, `npm test`, and `npm run lint` to validate client changes.

## User profile

The authenticated client has a **Profile** tab at `/`. The sign-in callback is
`/auth/callback`. Profile requests use the public User Service API at
`VITE_USER_SERVICE_API_URL` (default `http://localhost:5199`); set that public
URL in `.env.local` when the service uses another address.

The OIDC authorization-code flow with PKCE requests `users.read` and
`users.write`. The access token must also have the `user-service` audience.
The repository's current local Keycloak realm does not grant these scopes or
that audience, so a separate identity-provider configuration change is
required before live profile requests can succeed. This client task does not
change identity-provider behavior. Never add a client secret to browser
configuration.

The tab uses `POST /api/v1/users/` to create a profile, then `GET`,
`PUT`, and `DELETE /api/v1/users/{id}` for the returned profile. Creation
sends an `Idempotency-Key`; retries with the same unchanged form reuse the key
while the Profile tab remains open. Changing the form starts a new create
attempt with a new key.
Updates send the latest API `version`. The only editable fields are username
(3–30 letters, digits, `.`, `_`, or `-`; starts with a letter or digit), email
(valid address, maximum 254 characters), and optional display name (maximum
100 characters, no control characters). API validation remains authoritative.

The API has no `/me` endpoint and the token does not supply a profile ID. After
creation, the client stores the returned ID in browser `localStorage`, scoped
to the OIDC subject, so the same signed-in account can resume on the same
browser. The ID is only a lookup reference; all ownership checks remain in the
API. This does not synchronize across browsers/devices. A missing, inaccessible,
or deleted profile is shown as unavailable; the client does not invent profile
data or attempt an unsupported ownership lookup. Deletion is a soft delete,
but the profile cannot be retrieved or restored through the documented API.
The browser retains a subject-scoped deleted marker so a later visit does not
present an onboarding flow that the API cannot complete.

Validation errors are shown beside their fields. The client gives explicit
messages for expired sessions, missing authorization, not-found profiles,
conflicts (including stale versions), rate limits, and unavailable services.
Problem details are not rendered verbatim; when available, the correlation ID
is shown as a support reference.

See the [local development guide](../../../docs/local-development.md) for
starting the User Service and its local prerequisites.
