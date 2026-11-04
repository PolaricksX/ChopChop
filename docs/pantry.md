# Pantry service

Pantry owns per-user inventory. `GET /api/v1/pantry` supports `search`, `page`, and
`pageSize` (1–100); create, update, state changes, and delete require
`pantry.write`. Updates require the current version in the `If-Match` header.
Authentication uses JWT OIDC settings `Auth:Authority` and `Auth:Audience`.
Development accepts only the explicit test headers `X-Test-User` and
`X-Test-Scopes`; it never supplies a default identity and must not be enabled
in production. Ownership is derived from the `sub` claim, never from request data.
With a live issuer, JWT signature, issuer, audience, and expiry validation are
performed by the configured JWT handler. Automated tests do not have a live IdP,
so those live-token validation properties remain an integration limitation.

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
use RFC problem details. OpenAPI is served at `/openapi/v1.json`, with Swagger
UI development-only, and CORS permits `Client:Url` (default
`http://localhost:5173`). `/health` checks only the database.
