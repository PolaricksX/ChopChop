# User Pantry service

## Ownership and responsibilities

The User Pantry service owns pantry items, including:

- Item identity and ownership.
- Ingredient name, quantity, and unit.
- Optional expiration dates.
- The `PantryItems` table and its EF Core migrations.

The service does not call other services for the expiring-items query. It does
not publish an event or use a message broker for this read operation.

## Data model

`PantryItem` has the following fields:

| Field | Type | Persistence |
|---|---|---|
| `Id` | `Guid` | Required primary key |
| `OwnerId` | `Guid` | Required owner identifier |
| `Name` | `string` | Required, maximum 200 characters |
| `Quantity` | `decimal` | Required, precision `18,3` |
| `Unit` | `string` | Required, maximum 32 characters |
| `Expiration` | `DateTimeOffset?` | Optional SQL Server `datetimeoffset` |

The initial schema is created by the `InitialCreate` migration under
`src/UserPantry/Infrastructure/Migrations`.

## Endpoint contract

### Get expiring pantry items

```http
GET /api/v1/pantry-items/expiring?days=3&page=1&pageSize=20
```

The endpoint requires the `pantry.read` authorization policy. The owner is
always obtained from the authenticated caller through `ICurrentUser`; clients
cannot select an owner through query parameters.

### Query parameters

| Parameter | Required | Default | Valid values |
|---|---:|---:|---|
| `days` | No | `3` | Integer from `0` through `365` |
| `page` | No | `1` | Integer greater than or equal to `1` |
| `pageSize` | No | `20` | Integer from `1` through `100` |

`days=0` returns items expiring today. The lower boundary is today at
`00:00:00Z`, and the upper boundary is the start of the day after the
requested range. The lower boundary is inclusive and the upper boundary is
exclusive.

The current UTC date is obtained from the registered `TimeProvider`, which
keeps the behavior deterministic in tests.

The query excludes:

- Items with an expiration before today.
- Items whose expiration is `null`.
- Items owned by another user.

Results are ordered by:

1. `Expiration` ascending.
2. `Name` ascending.
3. `Id` ascending.

Clients cannot select a different sort order.

### Success response

The response is a paged collection:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000001",
      "ownerId": "00000000-0000-0000-0000-000000000011",
      "name": "Milk",
      "quantity": 1.0,
      "unit": "L",
      "expiration": "2026-10-07T00:00:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

An empty pantry or a window with no matches returns `200 OK` with an empty
`items` array and the requested paging metadata.

## Authorization and authentication

The endpoint uses the named `pantry.read` policy, which requires an authenticated
principal with the `pantry.read` scope.

The current scaffold uses the Development authentication handler. It supplies a
fixed development user and scope for local development only. It is not real JWT
validation and must not be treated as production identity validation.

The application layer reads the owner identifier through `ICurrentUser`. The
repository applies that owner filter to the database query.

## Errors and correlation IDs

Invalid query values return HTTP `400` with RFC problem details. The response
contains a safe validation message, the request path, and a `correlationId`
extension. It does not expose a stack trace or internal exception details.

Clients may provide an `X-Correlation-Id` request header. If it is absent, the
service generates one. The value is stored in the request context and returned
in the response header.

Unauthenticated requests return HTTP `401`.

## Local setup

Start SQL Server from the repository root:

```powershell
docker compose up -d
```

The compose configuration exposes SQL Server on `localhost:1433` and uses the
development connection values configured by the repository scaffold. Do not
reuse those credentials outside local development.

Restore and build the solution:

```powershell
dotnet restore
dotnet build -c Release --no-restore
```

The OpenAPI document is available while the service is running at:

```text
/openapi/v1.json
```

The health endpoint is:

```text
/health
```

## Tests and verification

Run the complete test project with SQL Server available:

```powershell
dotnet test .\tests\UserPantry.Tests\UserPantry.Tests.csproj -c Release --no-build
```

The test suite includes:

- UTC window and default-days unit tests.
- SQL Server-backed endpoint tests for filtering, ownership, ordering,
  pagination, validation, authorization, and problem details.
- OpenAPI contract checks.
- Namespace dependency-direction checks.

The integration-test factory applies migrations to a unique test database,
freezes time at `2026-10-06T12:34:00Z`, and removes the database during cleanup.

## Layer boundaries and limitations

The service follows these dependency boundaries:

```text
Api -> Application -> Domain
Infrastructure implements Application abstractions
```

Domain and Application do not depend on EF Core or ASP.NET Core. API response
DTOs are separate from EF entities.

The current implementation is the first User Pantry slice. It intentionally
does not include:

- Pantry write endpoints.
- Real JWT/OIDC validation.
- Cross-service calls.
- Events or broker integration.
- Rate limiting, CORS, or production header hardening.
- A dedicated owner-plus-expiration database index.

An owner-plus-expiration index should be evaluated after query performance is
measured against the real deployment workload.
