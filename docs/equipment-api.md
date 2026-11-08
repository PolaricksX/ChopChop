# User Equipment API

`Equipment.Api` owns a caller's cookware, appliances, and related equipment.
The service owns the `equipment` SQL schema and does not read or write another
service's tables. `Equipment.Domain` owns equipment invariants;
`Equipment.Data` owns SQL Server persistence, idempotency records, and the
transactional outbox.

## Authentication and ownership

The browser client (`ChopChop.Client`) is a public OAuth client. Its Equipment
tab shares the unified site's Auth0 sign-in and requests `equipment.read` or
`equipment.write` from the Equipment API audience. The imported Keycloak realm
remains available as the API's default local authority. The client and API
never handle user passwords or client secrets.

Configure the Keycloak `chopchop-web` client as public, enable standard flow,
and require PKCE S256 if using the local Keycloak default. The imported local
realm provides the `equipment-api` audience and the optional `equipment.read`
and `equipment.write` scopes. When using the unified client's Auth0 setup,
configure the Equipment API and delegated permissions from
`docs/local-development.md`. Read operations require `equipment.read`; create,
update, availability changes, and delete require `equipment.write`. Both
policies require an authenticated subject.

Configure `Auth:Authority`, optional `Auth:Issuer`, and `Auth:Audience` for the
deployment. The API validates issuer, audience, signature, expiration, and
configured RS256/ES256 algorithms using OIDC metadata and signing-key
rotation. HTTPS is required outside Development. There is no development
authentication bypass. `ICurrentUser` obtains the owner only from the
validated token's `sub` claim; clients cannot choose an owner in request data.
Every private query includes that owner and reports non-owned records as 404.

## Endpoints

Base path: `/api/v1/equipment`.

| Method | Path | Scope | Success | Notable errors |
|---|---|---|---|---|
| POST | `/` | `equipment.write` | 201, response body, `Location`, and `ETag` | 400, 401, 403, 409, 429 |
| GET | `/` | `equipment.read` | 200 paged response | 400, 401, 403, 429 |
| PUT | `/{equipmentId}` | `equipment.write` | 200 response body and `ETag` | 400, 401, 403, 404, 409, 429 |
| PUT | `/{equipmentId}/availability` | `equipment.write` | 200 response body and `ETag` | 400, 401, 403, 404, 409, 429 |
| DELETE | `/{equipmentId}` | `equipment.write` | 204 | 400, 401, 403, 404, 409, 429 |
| GET | `/health` | Public | 200 when the database is healthy | 503 |

`EquipmentUpsertRequest` accepts `name`, `type`, `quantity`, `condition`, and
optional `brand`, `size`, `notes`, and `location`. The API returns DTOs only.
List requests accept `page` (default 1), `pageSize` (default 20, maximum 100),
`search`, `type`, `condition`, and `availability`. Results have stable ordering
and return `items`, `page`, `pageSize`, and `totalCount`.

Create requires a unique `Idempotency-Key` (up to 128 characters). Repeating
the same key and payload replays the created resource as it currently exists
(it is not an immutable snapshot, and returns 409 if the item was since
deleted); using the key with a
different payload returns 409. The client reuses one key for retries of an
identical failed create. Update, availability changes, and delete
require the current strong `If-Match` value from the response's `ETag`; stale
versions return 409. Validation errors and other failures use RFC 9457
problem details with a `correlationId`. `X-Correlation-Id` is returned on
every response.

## Events

Equipment mutations stage `EquipmentAdded`, `EquipmentUpdated`,
`EquipmentAvailabilityChanged`, or `EquipmentRemoved` envelopes in the same
SQL transaction as the domain changes. Their routing keys are respectively
`equipment.added`, `equipment.updated`,
`equipment.availability.changed`, and `equipment.removed`. The dispatcher
publishes pending messages through the shared event bus to the
`chopchop.events` topic exchange. Failed delivery is retried up to three
times, then marked dead-lettered for operational review. Dispatch is disabled
unless `Messaging:Enabled` is true; disabled messages remain in the outbox.

## Configuration and local run

Use environment variables or a secret manager for deployment-specific
settings. `appsettings.json` contains only safe placeholders.

| Setting | Purpose |
|---|---|
| `ConnectionStrings__Equipment` | Equipment SQL Server database |
| `Auth__Authority` | OIDC authority / discovery endpoint |
| `Auth__Issuer` | Optional expected token issuer |
| `Auth__Audience` | Expected access-token audience (`equipment-api` locally) |
| `Cors__AllowedOrigins__0` | Allowed browser origin (local default: `http://localhost:5173`) |
| `RateLimiting__PermitLimit` | Per-subject or per-IP requests per minute (default 100) |
| `Messaging__Enabled` | Enable the outbox dispatcher |
| `Messaging__ConnectionString` | AMQP URI, required when dispatch is enabled |

Set `ConnectionStrings__Equipment`, then apply migrations and run the API:

```powershell
dotnet ef database update --project src\Equipment.Data --startup-project src\Equipment.Api
dotnet run --project src\Equipment.Api
```

Migrations are not applied automatically. In Development, OpenAPI is available
at `/openapi/v1.json`. The API listens on `http://localhost:5018`; the client
origin is allowed by `appsettings.Development.json`.

## Tests

Run the API, domain, and data tests with:

```powershell
dotnet test tests\Equipment.UnitTests
dotnet test tests\Equipment.IntegrationTests
```

API authentication tests use generated signed tokens and an in-memory
database. The SQL Server migration, collation, row-version, and transaction
test requires `TEST_SQLSERVER_CONNECTION` to point to a SQL Server instance
with permission to create and drop a database. It creates a uniquely named
`EquipmentTests_*` database and drops it afterward. There is no LocalDB
fallback; without the environment variable, the SQL Server test is reported
as skipped with the prerequisite stated.
