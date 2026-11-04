# Meal Planner Service

Backend capability for issue #21. The service owns meal-plan records and dated meal entries. Its implementation follows the User Service's .NET 10, layered projects, SQL Server, JWT, minimal API, Unit of Work, and integration-test patterns.

## Scope and milestone note

The repository instructions identify Milestone 1 as current and reference `docs/milestones.md`, which is not present in this checkout. The full issue #21 implementation was explicitly authorized as an exception. RabbitMQ is identified by the repository instructions as the standard event transport beginning in Milestone 2; adding that RabbitMQ-backed `IEventBus` and outbox was separately authorized for this issue.

## Projects and ownership

- `src/MealPlanner.Domain` contains plan/entry invariants and lifecycle states.
- `src/MealPlanner.Data` contains SQL Server persistence, migrations, repositories, the outbox, and RabbitMQ transport.
- `src/MealPlanner.Api` contains minimal API endpoints, DTOs, authorization, orchestration, and middleware.
- `tests/MealPlanner.UnitTests` covers domain, request validation, failure behavior, cancellation, and architecture boundaries.
- `tests/MealPlanner.IntegrationTests` exercises the real JWT bearer handler and SQL Server migrations/API behavior.

The caller's validated token `sub` claim is stored in each plan's `OwnerId`. The User Service owns user/profile data; Meal Planner does not read or write its tables and has no cross-service foreign key. Both services use `ConnectionStrings:ChopChop` to connect to the shared SQL Server database and own separate tables. Recipe references are optional stable GUIDs; each entry also stores a bounded title snapshot. Core planning never calls Recipe Service, so unavailable or changed recipes do not make a plan unavailable.

## Data model and rules

`MealPlan` stores owner subject, name, inclusive start/end dates, lifecycle status, UTC audit timestamps, and a SQL Server rowversion. `MealEntry` stores its plan ID, calendar date, slot, title snapshot, optional recipe GUID, servings, progress status, and UTC audit timestamps. SQL Server enforces plan-to-entry ownership and a unique index for non-Snack `(plan, date, slot)` entries.

- Dates are calendar dates serialized as ISO `YYYY-MM-DD` and stored as SQL `date`. They are `DateOnly`, not instants: no timezone conversion is performed. Audit timestamps are UTC `DateTimeOffset`.
- Plan date ranges are inclusive and limited to 366 days. Entry dates must be within that range. A range update is rejected if it would exclude any existing entry.
- Plan names are required, at most 120 characters. Meal titles are required, at most 200 characters. Servings must be 1–100.
- Breakfast, Lunch, and Dinner allow one entry per plan/date/slot. Snack entries are unlimited per date, as requested. Entry collections have their own pagination so a plan read does not load every meal.
- Plan lifecycle: `Draft -> Active -> Completed` or `Draft -> Cancelled` / `Active -> Cancelled`. Completed and Cancelled are terminal; terminal plans cannot be changed.
- Entry progress: `Planned -> InProgress`, `Planned -> Completed`, `Planned -> Skipped`, and `InProgress -> Completed` or `InProgress -> Skipped`. Completed and Skipped are terminal. Only Planned entries can be edited or removed.
- Duplicate non-Snack slots are a 400 validation error when detected before save; a concurrent collision protected by the unique index is a 409 conflict.
- All mutations use optimistic concurrency on the owning plan's rowversion. Stale versions return 409. Plan GET/create/update/status responses and meal-entry responses include a version and ETag; entry mutations carry the plan version in the request, while DELETE uses `If-Match`.
- Plan DELETE physically removes that caller-owned plan and cascades its entries. The database change and `MealPlanChanged` outbox record commit together.

## API (`/api/v1/meal-plans`)

Every private route requires a validated bearer token with a `sub` claim and the listed scope. Owners are always derived from that claim, never from request bodies. A foreign or missing plan/entry returns 404.

| Method | Path | Scope | Success | Notes |
|---|---|---|---|---|
| POST | `/` | `mealplans.write` | 201 + `Location`, ETag | Requires `Idempotency-Key`. |
| GET | `/` | `mealplans.read` | 200 | `page` default 1; `pageSize` default 20, max 100. |
| GET | `/{planId}` | `mealplans.read` | 200 + ETag | Owner-only. |
| PUT | `/{planId}` | `mealplans.write` | 200 + ETag | Requires `version` from last response. |
| PATCH | `/{planId}/status` | `mealplans.write` | 200 + ETag | Requires current `version`; transition rules apply. |
| DELETE | `/{planId}` | `mealplans.write` | 204 | Requires latest ETag in `If-Match`. |
| GET | `/{planId}/meals` | `mealplans.read` | 200 | Paginated; optional `date=YYYY-MM-DD`. |
| POST | `/{planId}/meals` | `mealplans.write` | 201 + `Location`, ETag | Requires `Idempotency-Key` and current plan `version`. |
| PUT | `/{planId}/meals/{entryId}` | `mealplans.write` | 200 + ETag | Planned entries only; requires current plan `version`. |
| PATCH | `/{planId}/meals/{entryId}/status` | `mealplans.write` | 200 + ETag | Requires current plan `version`; transition rules apply. |
| DELETE | `/{planId}/meals/{entryId}` | `mealplans.write` | 204 | Planned entries only; requires latest plan ETag in `If-Match`. |

POST idempotency records are scoped to the caller. Reusing the same key and payload returns the created resource; using the key for a different request returns 409. Collections use a stable date/slot/creation/ID order. Invalid request/domain values return RFC 9457 validation problem details; 401/403/404/409/429 responses are documented. Problem details include `correlationId`, and responses echo `X-Correlation-Id`.

## Events and delivery

Each successful plan or entry mutation stages an immutable `MealPlanChanged` v1 envelope in `MealPlanOutbox` in the same SQL transaction. The envelope contains event ID/type, schema version, UTC occurrence time, source service, correlation ID, and a minimal payload with plan ID, owner subject, change type, and optional entry ID/status. It intentionally excludes plan names and meal titles.

The outbox publisher uses the existing repository-standard RabbitMQ client behind `IEventBus`, publishes to the configured durable topic exchange with publisher confirms, and marks a row published only after broker confirmation. A process crash after broker confirmation but before the SQL update can cause redelivery; consumers must deduplicate by event ID and tolerate out-of-order events. No ordering guarantee is made across multiple service instances.

When RabbitMQ is unavailable, the API can still commit plan operations and outbox rows; the publisher retries connection on its poll loop. A message rejection or unroutable publication is retried with backoff up to three attempts, then marked dead-lettered in the outbox with only the exception type recorded. The failure is logged and requires operator inspection/replay. Consumers must use inbox/idempotency handling, retry transient processing failures at most three times, avoid retrying permanent validation failures, and configure their own durable queue/dead-letter routing. `GET /health` checks this service's database only and does not depend on RabbitMQ or another service.

## Security and operations

`mealplans.read` protects reads and `mealplans.write` protects mutations. JWT bearer signature, issuer, audience, and expiry are validated through ASP.NET Core's framework handler. There is a per-user/IP fixed-window rate limit (default 100 requests/minute), a 1 MB request-body cap, explicit CORS allowlisting, security headers, HSTS/HTTPS outside Development, structured logs, and correlation-aware RFC 9457 problem details. Tokens and meal contents are not logged.

Set `ConnectionStrings__ChopChop`, `Auth__Authority`, `Auth__Audience`, and (if needed) `Cors__AllowedOrigins__0`. To run local dependencies, set `MSSQL_SA_PASSWORD`, `RABBITMQ_DEFAULT_USER`, and `RABBITMQ_DEFAULT_PASS` in the shell, then run `docker compose up -d`. Set `RabbitMq__Uri` to the local AMQP URI (URL-escape special characters in credentials) and `RabbitMq__Enabled=true`. Apply migrations explicitly; they are not applied automatically:

```powershell
dotnet ef database update --project .\src\MealPlanner.Data --startup-project .\src\MealPlanner.Api
dotnet run --project .\src\MealPlanner.Api
```

Development OpenAPI is available at `/openapi/v1.json`. The executable request examples are in `src/MealPlanner.Api/MealPlanner.http`. Integration tests use LocalDB by default or `TEST_SQLSERVER_CONNECTION`; their databases are isolated and removed after each test host.

## Limitations

- Recipe IDs are not resolved or validated against Recipe Service; the title snapshot remains available if referenced content changes or is unavailable.
- RabbitMQ consumers, replay tooling for dead-lettered outbox records, and consumer inbox tables belong to their consuming services and are not implemented here.
- User deletion event handling and retention/purge policies are not implemented in this issue.
