# Milestone 3 Evidence — AI Recipe and Interactive Review

## Research

- Implemented the approved scope in [the plan](./milestone-3-ai-review-plan.md)
  and assigned GitHub work items #18 and #19.
- Aligned application dependencies with repository rules: .NET 10, SQL Server,
  framework JWT bearer validation for configured OIDC issuers (local Keycloak
  or Auth0), RabbitMQ behind `IEventBus`, Azure OpenAI behind a provider
  adapter, and React with Auth0 authorization-code + PKCE.
- Kept recipe catalog ownership, photos, the other team's services, and team
  milestone approval outside this assignment.

## Plan and Implement

- Added independent Domain/Data/Api project groups, service-owned SQL schemas,
  initial migrations, repository and Unit of Work abstractions, API contracts,
  health checks, structured request logging, correlation propagation, and
  OpenAPI.
- AI generation persists owner-scoped, idempotent requests; publishes
  generation work via a transactional outbox; validates bounded provider JSON;
  supports status polling and explicit user confirmation; and records
  completed/failed events.
- Review supports owner-scoped create/read/update/delete, database-enforced
  one-review-per-user-per-recipe, published-only feed pagination, moderator
  policy, state transitions, and transactional review events.
- Created the browser workflow for OIDC sign-in, opt-in pantry use, asynchronous
  recipe status, labelled recipe drafts, review submission, and moderation.
- Enforced operation-specific API scopes and authenticated fallback policies;
  mapped opaque provider subjects to stable internal owner IDs using validated
  issuer and subject claims.
- Added local infrastructure and setup guidance without storing live
  credentials.
- Added workspace-specific layer agents and skills before implementation, as
  requested.

## Authorization flow and policy mapping

The public SPA (`ChopChop.Client`) acts for the signed-in resource owner using
Auth0 authorization-code flow with PKCE. It obtains a bearer access token for
the target API audience and requested delegated scopes. Each API is an
independent resource server and validates its own audience. Interactive Review
uses `reviews.moderate` as its moderator-only permission; user identity is
derived only from validated token claims. The SPA has no client secret. The
client is the SPA, the resource owner is the signed-in user, Auth0 is the
authorization server, and each ChopChop API is its own resource server.

| API operation | Policy and required scope |
|---|---|
| `POST /api/recipe-generations` | `recipe-generations.create` |
| `GET /api/recipe-generations/{id}` | `recipe-generations.read` |
| `POST /api/recipe-generations/{id}/confirm` | `recipe-generations.confirm` |
| `POST /api/reviews` | `reviews.create` |
| `GET /api/reviews/{id}`, `GET /api/recipes/{recipeId}/reviews` | `reviews.read` |
| `PUT /api/reviews/{id}`, `DELETE /api/reviews/{id}` | `reviews.write` |
| `GET /api/moderation/reviews`, `POST /api/moderation/reviews/{id}` | `reviews.moderate` |
| `/`, `/health` | Explicitly anonymous |
| OpenAPI | Authenticated fallback policy |

Both APIs use `Auth:Authority` and `Auth:Audience` configuration. Their
framework bearer handlers validate signing key/signature, issuer, audience,
expiry, and signing-key metadata. HTTPS metadata is required outside
Development; development settings do not contain production credentials.
Policies authorize an exact space-delimited `scope`/`scp` or Auth0 `permissions`
claim. Unannotated endpoints are protected by a fallback policy.

## Validate

Authentication integration tests exercise the real framework bearer handler
with test signing keys and cover missing, malformed, invalid-signature,
expired, wrong-issuer, and wrong-audience tokens (`401`); missing or unrelated
scopes (`403`); correct per-operation scopes; anonymous root access; protected
OpenAPI; and stable mapping of opaque issuer/subject identities. Full
database-backed service tests and an end-to-end browser run require Docker
dependencies and a configured Azure OpenAI deployment.

Verification results in this environment:

- Client production build: passed.
- Client tests: passed, 19 tests across 6 files.
- Client lint: completed with four warnings in existing recipe/cookbook
  components.
- .NET build and API integration tests: not verified. The repository targets
  .NET 10, but this machine has only .NET 3.1 and 9.0 SDKs; the build stops
  with `NETSDK1045` before compiling the APIs or their new authentication tests.

## Reflect and Improve

- Keep all generated content visibly labelled and subject to human
  confirmation; never represent AI allergen/nutrition output as authoritative.
- The outbox and processing lease recovery preserve work across transient
  broker, provider, or process failures. The RabbitMQ event archive queue is
  bounded by message count and a seven-day TTL.
- Human code review, another team member's PR review, team-board tracking, and
  merge remain explicit follow-up gates and are not claimed complete.
