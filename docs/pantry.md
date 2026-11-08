# Pantry service

Pantry owns per-user inventory. `GET /api/v1/pantry` supports `search`, `page`, and
`pageSize` (1–100); create, update, state changes, and delete require
`pantry.write`. Updates require the current version in the `If-Match` header.
JSON requests and responses use enum names such as `"Grains"`, `"Cup"`, and
`"Available"`, matching the other service APIs. Numeric enum input remains
accepted for compatibility. Omit an optional expiration or send it as `null`.
The browser sends a selected date at midnight UTC in ISO 8601 format.
The standalone React client is a public OAuth client, the user is the resource
owner, Keycloak is the authorization server, and Pantry.Api is the resource
server. The browser uses authorization code with PKCE and requests
`pantry.read` and `pantry.write`; it does not hold a client secret. Configure
the public client with both `/auth/callback` and `/auth/silent-callback` redirect
URIs, the `/` post-logout redirect, and its browser origin. Sign-in requires
the corresponding scopes and the `pantry-api` audience in the access token.

The API requires `Auth:Authority` and `Auth:Audience` in every environment;
the framework JWT handler validates the signature, issuer, audience, and expiry.
Use HTTPS for the authority outside Development. There is no test-header
authentication bypass. Ownership is derived from the validated `sub` claim,
never from request data. `GET /api/v1/pantry` requires `pantry.read` or
`pantry.write`; POST, PUT, consume, unavailable, and DELETE require
`pantry.write`. `/health` is public. Tests exercise the JWT handler using test
signing credentials; live Keycloak and SQL Server still require separate
end-to-end validation.

For local development set `Auth__Authority=http://localhost:8080/realms/chopchop`,
`Auth__Audience=pantry-api`, and a SQL Server `ConnectionStrings__Pantry`
using environment variables or user secrets. Start the API with
`dotnet run --project src\Pantry.Api`. Start `src\Pantry.Client` with
`VITE_API_URL=http://localhost:5131` and `npm run dev`. The client's
`VITE_OIDC_AUTHORITY` and `VITE_OIDC_CLIENT_ID` default to the local realm and
`chopchop-web`. Reimport or update an existing local Keycloak realm to add the
Pantry scopes, audience mapper, and silent callback; an existing realm is not
automatically updated by changing the export.

SQL Server is configured by `ConnectionStrings:Pantry`. EF owns the schema and
optimistic row-version concurrency returns 409. `consume` sets quantity to zero
and records `Consumed`; `unavailable` records `Unavailable` while preserving
quantity. Custom items require a non-empty `customCategory`.

Every pantry mutation writes an `OutboxMessage` in the same database save,
including the correlation value `system` when no request correlation accessor is
available. The outbox dispatcher uses the shared `IEventBus` contract and
RabbitMQ topic conventions when `Messaging:ConnectionString` is configured;
without a broker, messages remain safely transactional and unpublished for
later retry. There is no idempotency-key convention in the repository, so
mutation requests do not claim replay suppression; duplicate active items are
rejected deterministically. Every request returns `X-Correlation-Id`; failures
use RFC problem details. OpenAPI at `/openapi/v1.json` and Swagger UI are
available in Development only. CORS permits `Client:Url` (default
`http://localhost:5173`). `/health` checks only the database.

Owner isolation uses a SHA-256 key derived from the exact UTF-16 subject,
including case and whitespace. The `ExactPantryOwnerKey` migration backfills
keys from the stored owner IDs. Older versions normalized owner IDs before
storing them, so an operator must assess any identities affected by that loss
before applying the migration. Review a generated migration script before
deployment. Downgrading this migration is intentionally unsupported and
requires a separately reviewed data migration.
