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

## Behaviour
- A profile belongs to the token `sub`. `POST` creates the caller's own profile; a second one gives 409.
- Access is owner-only; other users' profiles return 404.
- `POST` requires an `Idempotency-Key` header. Same key and payload replays the original result; same key with a different payload gives 409.
- `PUT` requires the `version` from the last response; a stale version gives 409.
- Email and username are unique (case-insensitive) among non-deleted profiles. Deleting frees them.

## Configuration
`ConnectionStrings:UserService`, `Auth:Authority`, `Auth:Audience`, `Cors` allowlist and `RateLimiting:PermitLimit` (default 100/min). No secrets are committed; supply them via environment variables, for example `ConnectionStrings__UserService`.

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
