# User Service

Backend for user profiles (issue #16). .NET 10, EF Core + SQL Server, JWT bearer auth.

## Projects
- `src/UserService.Domain` – `UserProfile` entity, validation rules, domain exceptions.
- `src/UserService.Data` – `DbContext`, configurations, migrations, Unit of Work, repository factory/provider.
- `src/UserService.Api` – minimal API endpoints, auth, middleware, OpenAPI.
- `tests/UserService.UnitTests`, `tests/UserService.IntegrationTests` (real SQL Server via LocalDB, or `TEST_SQLSERVER_CONNECTION`).

## Endpoints (`/api/v1/users`)
| Method | Path | Scope | Success | Errors |
|---|---|---|---|---|
| POST | `/` | `users.write` | 201 + `Location` | 400, 401, 403, 409, 429 |
| GET | `/{id}` | `users.read` | 200 | 401, 403, 404, 429 |
| PUT | `/{id}` | `users.write` | 200 | 400, 401, 403, 404, 409, 429 |
| DELETE | `/{id}` | `users.write` | 204 (soft delete) | 401, 403, 404, 429 |

`GET /health` is anonymous. OpenAPI is served at `/openapi/v1.json` in Development only.
Errors are RFC 9457 problem details including `correlationId` (`X-Correlation-Id` header).

## Profile contract

Requests and responses use JSON camelCase. Create and update accept `username`,
`email`, and optional `displayName`; update also requires the latest `version`.
The response contains `id`, the profile fields, `createdAt`, `updatedAt`, and
`version`. The API controls profile identity, ownership, and timestamps.

- Username is required, 3–30 characters, begins with a letter or digit, and
  contains only letters, digits, `.`, `_`, or `-`.
- Email is required, must be a valid address, and is limited to 254 characters.
- Display name is optional, limited to 100 characters, and cannot contain
  control characters.
- Create requires an `Idempotency-Key` of 1–128 printable ASCII characters.

There is no `/me` or profile-list endpoint. A browser client must retain the
returned profile ID if it needs to retrieve that profile on a later visit.
Ownership is still checked using the authenticated token's `sub`; an ID is not
an authorization credential. A soft-deleted profile cannot be retrieved, and
the current service does not allow the same subject to create another profile.

## Behaviour
- A profile belongs to the token `sub`. `POST` creates the caller's own profile; a second one gives 409.
- Access is owner-only; other users' profiles return 404.
- `POST` requires an `Idempotency-Key` header. Same key and payload replays the original result; same key with a different payload gives 409.
- `PUT` requires the `version` from the last response; a stale version gives 409.
- Email and username are unique (case-insensitive) among non-deleted profiles. Deleting frees them.

## Configuration
`ConnectionStrings:ChopChop`, `Auth:Authority`, `Auth:Audience`, `Cors` allowlist and `RateLimiting:PermitLimit` (default 100/min). Services use this shared SQL Server database while owning separate tables. No secrets are committed; supply the connection string via `ConnectionStrings__ChopChop`.

## Running
```
dotnet ef database update --project src/UserService.Data --startup-project src/UserService.Api
dotnet run --project src/UserService.Api
dotnet test
```
Migrations are not applied automatically at startup.

## Limitations
- Soft delete keeps personal data; a purge/retention job is not implemented.
- No admin role or list endpoint, so no pagination.
- No `UserDeleted` event yet (planned for a later milestone).
