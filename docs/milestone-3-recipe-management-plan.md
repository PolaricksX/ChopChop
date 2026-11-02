# Milestone 3 Implementation Plan: Recipe Management (#23)

## Status

**Gate 1 — approved by Léa-Maude Montpetit on 2026-11-02.**

This plan is limited to Recipe Management, its approved shared React client work,
its owned infrastructure, and documentation. It does not modify Interactive
Review, Cookbook, Meal Planner, User and Profile, User Equipment, or AI Recipe
Generation ownership.

## Research (R)

### Source requirements

- GitHub issue [#23 — Recipe Management](https://github.com/PolaricksX/ChopChop/issues/23).
- `copilot-instructions.md`, especially the layering, ownership, API, data,
  event, security, and testing rules.
- `docs/milestones.md`, which now lists #23 under Milestone 3.
- `docs/adr/0002-service-architecture.md` for independently runnable layered
  services and the shared React client.
- Existing implementations in `UserService`, `InteractiveReview`,
  `AiRecipeGeneration`, `ChopChop.Shared`, and `ChopChop.Client`.

### Approved decisions

1. Issue #23 is approved for Milestone 3.
2. A recipe has one stable GUID and integer versions starting at 1. Each new
   version increments by 1.
3. Editing a published recipe creates one new draft version. Only one draft
   version may exist for a recipe at a time.
4. Reviews remain attached to the stable recipe ID. Interactive Review is not
   changed.
5. The first publication and a republication after unpublish emit
   `RecipePublished`. Publishing a new version over a currently published
   version emits `RecipeUpdated`. Draft edits emit no recipe event.
6. Published list, search, and detail endpoints are anonymous. Draft and command
   endpoints are protected.
7. Allergens use a fixed enum representing Health Canada priority allergens.
8. Allergen status is `Declared` or `Unknown`. A declared empty list means no
   allergens were declared. Unknown recipes are excluded whenever an allergen
   exclusion filter is supplied.
9. Preference filtering is limited to tags and dietary labels.
10. Confirmed AI drafts are submitted through the normal create endpoint and are
    marked as AI-generated.
11. `UserDeleted` deletes the user's drafts and anonymizes published recipes.
12. User-deletion processing uses a `ProcessedEvent` inbox row and an outcome
    record in the Recipe Management schema.
13. Integration tests use LocalDB by default and support a connection-string
    override.
14. Architecture tests are placed in the new `RecipeManagement.UnitTests`
    project.
15. Collections use `{ items, page, pageSize, totalCount }`.
16. Concurrency follows the existing Interactive Review approach: an integer
    concurrency token is configured in Data and conflicts map to `409`.
17. Recipe screens are placed under `src/ChopChop.Client/src/recipes/`, with
    minimal `App.tsx` changes.
18. Policies use exactly `recipes.read`, `recipes.write`, and `recipes.publish`.
19. Service-to-service calls forward the user's token for now; this limitation
    is documented.
20. Drafts are hard-deleted. A published recipe can only be unpublished.

### Current repository patterns to reuse

- Shared event primitives and correlation middleware:
  `src/ChopChop.Shared/EventEnvelope.cs` and
  `src/ChopChop.Shared/CorrelationIdMiddleware.cs`.
- Validated subject claim helper:
  `src/ChopChop.Shared/ClaimsPrincipalExtensions.cs`.
- Repository factory, provider, Unit of Work, EF repository, and concurrency
  translation:
  `src/UserService.Data/Repositories/RepositoryFactory.cs`,
  `src/UserService.Data/Repositories/UnitOfWork.cs`, and
  `src/InteractiveReview.Data/IRepository.cs`.
- JWT options, fallback authentication policy, scope parsing, rate limiting,
  request limits, and development-only Swagger:
  `src/UserService.Api/Program.cs`,
  `src/UserService.Api/Infrastructure/AuthOptions.cs`, and
  `src/UserService.Api/Infrastructure/Policies.cs`.
- Owned schema, DbContext, migrations, outbox records, dispatcher, and RabbitMQ
  adapter:
  `src/InteractiveReview.Data/Context/InteractiveReviewDbContext.cs`,
  `src/InteractiveReview.Data/OutboxMessage.cs`,
  `src/InteractiveReview.Api/Services/OutboxDispatcher.cs`, and
  `src/InteractiveReview.Data/Messaging/RabbitMqEventBus.cs`.
- Real JWT integration testing:
  `tests/UserService.IntegrationTests/Support/TestJwt.cs` and
  `tests/UserService.IntegrationTests/Support/ApiFactory.cs`.
- Shared React authentication and API error handling:
  `src/ChopChop.Client/src/auth.ts`,
  `src/ChopChop.Client/src/api.ts`,
  `src/ChopChop.Client/src/App.tsx`, and the existing client tests.

## Scope and constraints

### In scope

- Four service projects under `src/`:
  `RecipeManagement.Domain`, `RecipeManagement.Data`,
  `RecipeManagement.Api`, and the approved shared-client integration.
- `RecipeManagement.UnitTests` and `RecipeManagement.IntegrationTests`.
- Recipe aggregate, immutable versions, lifecycle, validation, search,
  filtering, allergen semantics, equipment matching, outbox events, and the
  idempotent `UserDeleted` consumer.
- Versioned REST API under `/api/v1/recipes`.
- React screens for draft creation/editing, publication, unpublication,
  published search/list/detail, and error/validation handling.
- API/event/setup/observability documentation.

### Out of scope

- Ratings, reviews, saved recipes, folders, meal plans, user allergies,
  nutrition goals, pantry ownership, equipment ownership, AI provider work, a
  new broker, an API gateway, or changes to Interactive Review.
- Direct access to another service's tables or code.
- A separate `RecipeManagement.Client` project; the client is
  `src/ChopChop.Client`.

### Layer rules

- Domain references no EF Core or ASP.NET Core types.
- Data references Domain and owns SQL Server, migrations, repositories,
  persistence models, outbox, inbox, and RabbitMQ adapters.
- API references Data and Domain, owns composition, DTOs, policies, endpoint
  mapping, application orchestration, and exception mapping.
- The React client uses only HTTP endpoints and public DTO shapes.
- Shared code remains limited to stable cross-cutting primitives.

## Proposed API contract

All API routes use JSON string enums, UTC `DateTimeOffset` values, GUID IDs,
RFC 9457 problem details, and `X-Correlation-Id`. Private resources belonging to
another user return `404`.

### Endpoints

| Method | Route | Policy | Success and required failure statuses |
|---|---|---|---|
| `POST` | `/api/v1/recipes` | `recipes.write` | `201 Created` with `Location`; `400`, `401`, `403`, `409` idempotency conflict, `413`, `429` |
| `GET` | `/api/v1/recipes/drafts` | `recipes.read` | `200`; `400`, `401`, `403` |
| `GET` | `/api/v1/recipes/{recipeId}/draft` | `recipes.read` | `200`; `401`, `403`, `404` |
| `PUT` | `/api/v1/recipes/{recipeId}/draft` | `recipes.write` | `204`; `400`, `401`, `403`, `404`, `409`, `429` |
| `DELETE` | `/api/v1/recipes/{recipeId}/draft` | `recipes.write` | `204`; `401`, `403`, `404`, `409`, `429` |
| `POST` | `/api/v1/recipes/{recipeId}/publish` | `recipes.publish` | `200` with the published version; `400`, `401`, `403`, `404`, `409`, `429` |
| `POST` | `/api/v1/recipes/{recipeId}/unpublish` | `recipes.publish` | `204`; `401`, `403`, `404`, `409`, `429` |
| `GET` | `/api/v1/recipes/{recipeId}/versions/{version}` | Anonymous | `200`; `400`, `404` |
| `GET` | `/api/v1/recipes/{recipeId}` | Anonymous | `200` with the current published version; `404` when unpublished or missing |
| `GET` | `/api/v1/recipes` | Anonymous | `200` paginated; `400` for invalid or excessive filter complexity |
| `GET` | `/health` | Anonymous | `200` or health-check failure status; database only |
| OpenAPI | Development only | Anonymous | Available only in development |

`GET /api/v1/recipes/{recipeId}/versions/{version}` returns any published
version, including superseded versions. `GET /api/v1/recipes/{recipeId}` returns
the current published version. An unpublished recipe returns `404` on both
public routes. Draft retrieval is through the owner-scoped draft route. The
published list/search contract is one `GET /api/v1/recipes` route accepting
bounded text, tags, dietary labels, maximum total time, servings, excluded
allergens, available equipment, page, page size, and allowlisted sort fields.
Search responses include separate allergy-exclusion and preference-exclusion
metadata where filtering diagnostics are returned. Excessive filter complexity
is a `400`, not a `413`.

Create and update requests contain recipe content only. They cannot set recipe
ID, author, lifecycle status, or version. Create requires `Idempotency-Key`.
Update carries the expected integer concurrency version. `PUT` creates the next
draft version when the recipe is published and has no draft. It returns `409`
when a draft already exists for a flow that does not permit replacement.

### Event contract

Events are stored in the service outbox before publication through the existing
`IEventBus` and RabbitMQ adapter:

- `recipe_management.recipe.published` — stable recipe ID, newly published
  version, and publication timestamp. This is emitted only for the first
  publication.
- `recipe_management.recipe.updated` — stable recipe ID, newly published version,
  previous published version, and publication timestamp. This is emitted only
  when publication replaces an older published version.
- `recipe_management.recipe.unpublished` — stable recipe ID, unpublished
  version, and timestamp.
- `user.deleted` consumption — user ID only, processed through an inbox and an
  outcome record.

The event payloads contain no secrets or unnecessary personal data. Events carry
the existing envelope fields, including schema version and correlation ID.
Consumers must assume at-least-once delivery.

## File-by-file implementation plan

Each file below is an intended implementation artifact. The modeled-on reference
is included to preserve repository conventions. Acceptance criteria are listed
using the identifiers in the mapping below.

### Domain layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/RecipeManagement.Domain/RecipeManagement.Domain.csproj` | Framework-free Domain project. | `src/InteractiveReview.Domain/InteractiveReview.Domain.csproj` | AC1 |
| `src/RecipeManagement.Domain/Enums/RecipeStatus.cs` | Draft, Published, and Unpublished lifecycle states. | `src/InteractiveReview.Domain/ReviewStatus.cs` | AC2, AC4 |
| `src/RecipeManagement.Domain/Enums/Allergen.cs` | Health Canada priority-allergen enum. | Existing domain enum patterns | AC3, AC5 |
| `src/RecipeManagement.Domain/Enums/AllergenStatus.cs` | Declared versus Unknown semantics. | Existing status patterns | AC5 |
| `src/RecipeManagement.Domain/Entities/Recipe.cs` | Stable ID, owner, current version, lifecycle, timestamps, and version transitions. | `src/InteractiveReview.Domain/Review.cs` | AC2, AC3, AC4 |
| `src/RecipeManagement.Domain/Entities/RecipeVersion.cs` | Immutable version content and publication metadata. | `src/InteractiveReview.Domain/Review.cs` | AC2, AC3, AC4 |
| `src/RecipeManagement.Domain/Entities/Ingredient.cs` | Ingredient name, decimal quantity, and explicit unit; allergens are declared at recipe level only. | `src/InteractiveReview.Domain/Review.cs` | AC3, AC5 |
| `src/RecipeManagement.Domain/Entities/InstructionStep.cs` | Ordered instruction content. | Existing aggregate child patterns | AC3 |
| `src/RecipeManagement.Domain/Entities/NutritionEstimate.cs` | Decimal nutrition values and estimate metadata. | Existing DTO/domain value patterns | AC3 |
| `src/RecipeManagement.Domain/Entities/EquipmentRequirement.cs` | Equipment requirement value and optional matching metadata. | Existing domain entity patterns | AC3, AC5 |
| `src/RecipeManagement.Domain/Exceptions/RecipeDomainExceptions.cs` | Lifecycle, validation, and conflict exceptions. | `src/UserService.Domain/Exceptions/*` | AC2, AC3, AC4 |
| `src/RecipeManagement.Domain/Services/RecipeValidator.cs` | Pure bounded validation and allergen rules. | `src/UserService.Api/Contracts/UserRequestValidator.cs` plus domain rules | AC3, AC5 |
| `src/RecipeManagement.Domain/Services/RecipeLifecycle.cs` | Validates draft, publish, and unpublish transitions. | `src/InteractiveReview.Domain/Review.cs` status methods | AC2, AC4 |

### Data layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `Solution.sln` | Adds Recipe Management source and test projects to the solution. | Existing solution project entries | AC1, AC10 |
| `src/RecipeManagement.Data/RecipeManagement.Data.csproj` | Data project referencing Domain and approved EF/RabbitMQ packages. | `src/InteractiveReview.Data/InteractiveReview.Data.csproj` | AC1 |
| `src/RecipeManagement.Data/IRepository.cs` | Generic repository, provider, and factory contracts that keep EF Core out of API services. | `src/InteractiveReview.Data/IRepository.cs` | AC1 |
| `src/RecipeManagement.Data/Repositories/IUnitOfWork.cs` | Unit of Work contract exposing generic/custom repositories and one async save operation. | `src/UserService.Data/Repositories/UnitOfWork.cs` | AC1, AC4, AC7 |
| `src/RecipeManagement.Data/Repositories/EfRepository.cs` | Generic EF repository implementation for aggregate staging. | `src/InteractiveReview.Data/Repositories/EfRepository.cs` | AC1 |
| `src/RecipeManagement.Data/Repositories/RepositoryFactory.cs` | Scoped generic/custom repository factory. | `src/UserService.Data/Repositories/RepositoryFactory.cs` | AC1 |
| `src/RecipeManagement.Data/Repositories/RepositoryProvider.cs` | Scoped repository cache. | `src/UserService.Data/Repositories/RepositoryFactory.cs` | AC1 |
| `src/RecipeManagement.Data/Repositories/UnitOfWork.cs` | Single-save transaction boundary and concurrency/constraint translation. | `src/UserService.Data/Repositories/UnitOfWork.cs` | AC1, AC4, AC7 |
| `src/RecipeManagement.Data/Context/RecipeManagementDbContext.cs` | Owned schema, aggregate DbSets, outbox, inbox, idempotency, and deletion-outcome tables. | `src/InteractiveReview.Data/Context/InteractiveReviewDbContext.cs` | AC1, AC4, AC7 |
| `src/RecipeManagement.Data/Configurations/RecipeConfiguration.cs` | Recipe keys, indexes, lengths, and ownership constraints. | Existing EF model configuration | AC2, AC3 |
| `src/RecipeManagement.Data/Configurations/RecipeVersionConfiguration.cs` | Version uniqueness, immutable content mapping, and concurrency token. | `InteractiveReviewDbContext` concurrency mapping | AC2, AC4 |
| `src/RecipeManagement.Data/Configurations/ChildEntityConfigurations.cs` | Ingredient, step, nutrition, equipment, and allergen mappings. | Existing EF configuration patterns | AC3, AC5 |
| `src/RecipeManagement.Data/Repositories/IRecipeQueries.cs` | Owner-scoped draft queries, published detail queries, and paged search contract without EF types. | `src/InteractiveReview.Data/Repositories/IReviewQueries.cs` | AC2, AC5 |
| `src/RecipeManagement.Data/Repositories/RecipeQueries.cs` | No-tracking projections, bounded filtering, pagination, sorting, and eager loading. | `src/InteractiveReview.Data/Repositories/ReviewQueries.cs` | AC2, AC5 |
| `src/RecipeManagement.Data/Repositories/IIdempotencyQueries.cs` | Lookup and persistence contract for owner/key/payload/result replay. | AI generation idempotency repository | AC4 |
| `src/RecipeManagement.Data/Repositories/IdempotencyQueries.cs` | EF implementation that returns the stored create result for an identical request and detects payload reuse conflicts. | Existing AI generation idempotency queries | AC4, AC10 |
| `src/RecipeManagement.Data/IdempotencyRecord.cs` | Stores owner, idempotency key, request fingerprint, resource ID, response payload, and status for replay. | Existing idempotency persistence entities | AC4, AC10 |
| `src/RecipeManagement.Data/Repositories/IOutboxWriter.cs` | Stage serialized recipe event envelopes with aggregate changes. | `src/InteractiveReview.Data/Repositories/IOutboxWriter.cs` | AC7 |
| `src/RecipeManagement.Data/Repositories/IOutboxQueries.cs` | Retrieve pending messages for dispatch. | `src/InteractiveReview.Data/Repositories/IOutboxQueries.cs` | AC7 |
| `src/RecipeManagement.Data/Repositories/OutboxWriter.cs` | EF implementation of outbox writes. | `src/InteractiveReview.Data/Repositories/OutboxWriter.cs` | AC7 |
| `src/RecipeManagement.Data/Repositories/OutboxQueries.cs` | EF implementation of pending outbox queries. | `src/InteractiveReview.Data/Repositories/OutboxQueries.cs` | AC7 |
| `src/RecipeManagement.Data/Repositories/IUserDeletionQueries.cs` | Idempotent inbox and deletion-outcome persistence contract. | `IOutboxQueries` and repository conventions | AC7 |
| `src/RecipeManagement.Data/Repositories/UserDeletionQueries.cs` | Deletes drafts, anonymizes published authors, and records outcome transactionally. | Existing custom repository pattern | AC2, AC7 |
| `src/RecipeManagement.Data/OutboxMessage.cs` | Recipe-owned durable event row. | `src/InteractiveReview.Data/OutboxMessage.cs` | AC7 |
| `src/RecipeManagement.Data/ProcessedEvent.cs` | Inbox deduplication primary key by event ID. | Repository event-handler example in `copilot-instructions.md` | AC7 |
| `src/RecipeManagement.Data/UserDeletionOutcome.cs` | Records deleted/anonymized counts and processing result. | Existing persistence entity conventions | AC7 |
| `src/RecipeManagement.Data/Messaging/RabbitMqEventBus.cs` | Uses the existing RabbitMQ event transport and routing exchange. | `src/InteractiveReview.Data/Messaging/RabbitMqEventBus.cs` | AC7 |
| `src/RecipeManagement.Data/ServiceCollectionExtensions.cs` | Registers SQL Server, repositories, UoW, event, outbox, and inbox services. | `src/InteractiveReview.Data/ServiceCollectionExtensions.cs` | AC1, AC7 |
| `src/RecipeManagement.Data/Migrations/<timestamp>_InitialRecipeManagement.cs` | Initial owned schema migration. | `src/InteractiveReview.Data/Migrations/*` | AC1, AC4, AC7 |
| `src/RecipeManagement.Data/Migrations/RecipeManagementDbContextModelSnapshot.cs` | EF model snapshot. | Existing migration snapshots | AC1 |

### API layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/RecipeManagement.Api/RecipeManagement.Api.csproj` | Web project referencing Data/Domain and API packages. | `src/UserService.Api/UserService.Api.csproj` | AC1 |
| `src/RecipeManagement.Api/Contracts/RecipeContracts.cs` | Public request, response, filter, pagination, nutrition, ingredient, and event DTO records. | `src/InteractiveReview.Api/Contracts/ReviewContracts.cs` | AC1, AC2, AC3, AC5 |
| `src/RecipeManagement.Api/Contracts/RecipeMappings.cs` | Maps domain/persistence results to public DTOs only. | `src/UserService.Api/Contracts/UserMapping.cs` | AC1, AC2 |
| `src/RecipeManagement.Api/Contracts/RecipeRequestValidator.cs` | Boundary validation for body size, lengths, counts, units, page/filter bounds, and idempotency key. | `src/UserService.Api/Contracts/UserRequestValidator.cs` | AC3, AC4, AC5 |
| `src/RecipeManagement.Api/Infrastructure/AuthOptions.cs` | Environment-configured JWT authority, audience, algorithms, and HTTPS metadata. | `src/UserService.Api/Infrastructure/AuthOptions.cs` | AC6 |
| `src/RecipeManagement.Api/Infrastructure/Policies.cs` | Exact `recipes.read`, `recipes.write`, and `recipes.publish` scope policies. | `src/UserService.Api/Infrastructure/Policies.cs` | AC6 |
| `src/RecipeManagement.Api/Infrastructure/ApiExceptionHandler.cs` | Maps domain, ownership, concurrency, idempotency, and validation exceptions to problem details without stack traces. | `src/UserService.Api/Infrastructure/ApiExceptionHandler.cs` | AC2, AC3, AC4, AC6 |
| `src/RecipeManagement.Api/Services/ICurrentUser.cs` | API boundary for the validated token subject. | `src/UserService.Api/Services/ICurrentUser.cs` | AC2, AC6 |
| `src/RecipeManagement.Api/Services/CurrentUser.cs` | Reads only the validated `sub` claim. | `src/UserService.Api/Services/ICurrentUser.cs` | AC2, AC6 |
| `src/RecipeManagement.Api/Services/RecipeApplicationService.cs` | Orchestrates create, draft CRUD, versioning, publication, unpublication, search, DTO mapping, and outbox staging. | `src/InteractiveReview.Api/Services/ReviewApplicationService.cs` | AC2, AC3, AC4, AC5, AC7 |
| `src/RecipeManagement.Api/Services/UserDeletedHandler.cs` | Consumes `UserDeleted`, checks `ProcessedEvent`, applies deletion/anonymization, and records outcome idempotently. | Event-handler example in `copilot-instructions.md` | AC7 |
| `src/RecipeManagement.Api/Services/UserDeletedConsumerHostedService.cs` | Hosted RabbitMQ consumer that subscribes to `user.deleted`, calls `UserDeletedHandler`, retries transient failures with backoff, and routes poison messages to the dead-letter queue. | Existing RabbitMQ hosted-service and dispatcher patterns | AC7, AC10 |
| `src/RecipeManagement.Api/Services/OutboxDispatcher.cs` | Publishes pending events and applies retry/dead-letter behavior. | `src/InteractiveReview.Api/Services/OutboxDispatcher.cs` | AC7 |
| `src/RecipeManagement.Api/Endpoints/RecipeEndpoints.cs` | Thin versioned endpoint mapping, policies, status codes, headers, and cancellation tokens. | `src/UserService.Api/Endpoints/UserEndpoints.cs` | AC2, AC3, AC4, AC5, AC6 |
| `src/RecipeManagement.Api/Program.cs` | Composition root, middleware order, fallback policy, rate limiting, body size, CORS, OpenAPI, health, event bus, and hosted services. | `src/UserService.Api/Program.cs` | AC1, AC6, AC7, AC11 |
| `src/RecipeManagement.Api/appsettings.json` | Safe local defaults and configuration keys without secrets. | Existing service appsettings files | AC6, AC11 |
| `src/RecipeManagement.Api/appsettings.Development.json` | Development-only non-secret configuration and local ports. | Existing service development settings | AC6, AC11 |
| `src/RecipeManagement.Api/Properties/launchSettings.json` | Local API port and environment profile. | Existing service launch settings | AC11 |

### Infrastructure and identity configuration

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `infra/keycloak/realm-export/chopchop-realm.json` | Adds the Recipe Management audience/client scope and the `recipes.read`, `recipes.write`, and `recipes.publish` scopes to the local Keycloak realm without committing secrets. | Existing Keycloak realm export | AC6, AC9 |
| `src/ChopChop.Client/src/auth.ts` | Requests the three Recipe Management scopes in the authorization-code + PKCE client configuration. | Existing OIDC configuration in `src/ChopChop.Client/src/auth.ts` | AC6, AC8 |

### Client layer — shared `ChopChop.Client`

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/ChopChop.Client/src/recipes/types.ts` | Public TypeScript DTOs, enums, pagination, filters, and problem-detail shapes. | `src/ChopChop.Client/src/api.ts` types | AC8 |
| `src/ChopChop.Client/src/recipes/api.ts` | Recipe HTTP functions, bearer/correlation headers, idempotency keys, concurrency version, and API error handling. | `src/ChopChop.Client/src/api.ts` | AC8 |
| `src/ChopChop.Client/src/recipes/validation.ts` | Client-side bounded validation only; no business-rule replacement. | Existing form validation patterns | AC3, AC8 |
| `src/ChopChop.Client/src/recipes/RecipeList.tsx` | Anonymous published list/search with pagination and filters. | Existing `App.tsx` recipe/review panels | AC5, AC8 |
| `src/ChopChop.Client/src/recipes/RecipeDetails.tsx` | Published version detail and exclusion-status presentation. | Existing client display panels | AC5, AC8 |
| `src/ChopChop.Client/src/recipes/RecipeEditor.tsx` | Authenticated create/edit form for ingredients, steps, tags, allergens, times, nutrition, and equipment. | Existing form code in `App.tsx` | AC3, AC8 |
| `src/ChopChop.Client/src/recipes/RecipeDrafts.tsx` | Owner draft list, edit, delete, publish, and unpublish actions. | Existing authenticated panel pattern | AC2, AC4, AC8 |
| `src/ChopChop.Client/src/recipes/RecipeScreens.test.tsx` | Component tests for auth state, validation, actions, pagination, and API errors. | `src/ChopChop.Client/src/App.test.tsx` | AC8 |
| `src/ChopChop.Client/src/recipes/api.test.ts` | HTTP contract tests for headers, routes, idempotency, concurrency, and problem details. | `src/ChopChop.Client/src/api.test.ts` | AC8 |
| `src/ChopChop.Client/src/App.tsx` | Minimal integration of recipe screens and existing OIDC callback/sign-in state. | Existing `App.tsx` | AC8 |
| `src/ChopChop.Client/src/App.css` | Minimal styles for recipe screens and validation/error states. | Existing client styling | AC8 |
| `src/ChopChop.Client/README.md` | Client environment, OIDC/PKCE settings, API URLs, requested scopes, and local run instructions. | Existing client README | AC8, AC11 |
| `src/ChopChop.Client/.env.example` | Safe API and OIDC placeholders without secrets. | Existing `.env.example` | AC8, AC11 |

### Tests layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `tests/RecipeManagement.UnitTests/RecipeManagement.UnitTests.csproj` | xUnit, NetArchTest, and Domain/API test project references. | `tests/UserService.UnitTests/UserService.UnitTests.csproj` | AC1 |
| `tests/RecipeManagement.UnitTests/Architecture/LayerRulesTests.cs` | Enforces Domain/Data/API dependency direction and no EF in API contracts/services. | `tests/UserService.UnitTests/Architecture/LayerRulesTests.cs` | AC1 |
| `tests/RecipeManagement.UnitTests/Domain/RecipeValidationTests.cs` | Tests missing title, ingredients, steps, nonpositive servings, units, bounds, and nutrition/ingredient values. | Existing domain unit-test patterns | AC3 |
| `tests/RecipeManagement.UnitTests/Domain/RecipeLifecycleTests.cs` | Tests valid/invalid draft, publish, unpublish, and published immutability transitions. | Interactive Review domain tests | AC2, AC4 |
| `tests/RecipeManagement.UnitTests/Domain/RecipeVersioningTests.cs` | Tests stable ID, version 1, incrementing versions, one-draft rule, and AI-generated flag. | Existing aggregate tests | AC2, AC4 |
| `tests/RecipeManagement.UnitTests/Domain/AllergenSemanticsTests.cs` | Tests declared empty, declared allergens, unknown data, and fail-closed filtering semantics. | Existing domain rule tests | AC5 |
| `tests/RecipeManagement.UnitTests/Services/RecipeApplicationServiceTests.cs` | Tests owner scoping, DTO mapping, idempotency, concurrency, and event staging with fake repositories/UoW. | `ReviewApplicationService` behavior and test patterns | AC2, AC4, AC7 |
| `tests/RecipeManagement.UnitTests/Services/UserDeletedHandlerTests.cs` | Tests first delivery, duplicate delivery, draft deletion, published anonymization, and outcome recording. | Event-handler example in instructions | AC7 |
| `tests/RecipeManagement.IntegrationTests/RecipeManagement.IntegrationTests.csproj` | WebApplicationFactory, EF SQL Server, JWT, xUnit, and test dependencies. | `tests/UserService.IntegrationTests/UserService.IntegrationTests.csproj` | AC1, AC10 |
| `tests/RecipeManagement.IntegrationTests/Support/TestJwt.cs` | RSA-signed tokens using the real JWT bearer handler. | `tests/UserService.IntegrationTests/Support/TestJwt.cs` | AC6, AC10 |
| `tests/RecipeManagement.IntegrationTests/Support/ApiFactory.cs` | LocalDB default, connection-string override, unique database, migrations, static OIDC signing configuration, and test configuration. | `tests/UserService.IntegrationTests/Support/ApiFactory.cs` | AC6, AC10 |
| `tests/RecipeManagement.IntegrationTests/Api/RecipeCrudTests.cs` | Create/list/get/update/delete draft contracts and ownership isolation. | Existing User Service API tests | AC2, AC3, AC10 |
| `tests/RecipeManagement.IntegrationTests/Api/RecipeLifecycleTests.cs` | Publish, unpublish, immutable published versions, new draft version, and invalid transitions. | Existing service integration tests | AC2, AC4, AC10 |
| `tests/RecipeManagement.IntegrationTests/Api/SearchTests.cs` | Text, tags, time, servings, allergens, equipment, preferences, pagination, and exclusion metadata. | Existing bounded collection tests | AC5, AC10 |
| `tests/RecipeManagement.IntegrationTests/Api/IdempotencyAndConcurrencyTests.cs` | Same-key replay, same-key different payload, optimistic concurrency, and duplicate-draft conflicts. | User Service/AI idempotency patterns | AC4, AC10 |
| `tests/RecipeManagement.IntegrationTests/Api/SecurityTests.cs` | Missing/invalid/expired/wrong issuer/wrong audience/wrong signature tokens, scopes, public routes, body limits, and rate limits. | `TestJwt` and existing security tests | AC6, AC10 |
| `tests/RecipeManagement.IntegrationTests/Events/OutboxAndUserDeletedTests.cs` | Transactional outbox, envelope fields, correlation ID, inbox deduplication, deletion outcomes, and retry behavior. | Existing outbox tests | AC7, AC10 |

### Documentation layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `docs/api-contracts.md` | Add Recipe Management routes, DTOs, status codes, policies, pagination, filters, concurrency, and public/private rules. | Existing `docs/api-contracts.md` | AC9 |
| `docs/recipe-management.md` | Ownership, data model, lifecycle/versioning, allergen semantics, search behavior, failure handling, and service-to-service token forwarding. | Existing service documentation | AC9 |
| `docs/adr/0003-recipe-versioning-and-allergen-semantics.md` | Record stable IDs, immutable published versions, draft rules, fixed allergen enum, and fail-closed filtering. | `docs/adr/0002-service-architecture.md` | AC2, AC5, AC9 |
| `docs/threat-model.md` | Add Recipe Management threats and mitigations for IDOR, mass assignment, JWT/scope misuse, allergen fail-open behavior, replay/idempotency, event duplication, filter abuse, and sensitive logging. | Existing repository threat-model guidance | AC6, AC7, AC9 |
| `docs/local-development.md` | Add API/client ports, LocalDB/Docker setup, migrations, Keycloak values/scopes, RabbitMQ, and safe configuration. | Existing local development guide | AC9, AC11 |
| `docs/milestone-3-recipe-management-evidence.md` | Record implementation commits, tests, build/format/audit outcomes, and environment limitations. | Existing AI/Review evidence document | AC11 |

## Acceptance-criteria traceability

| ID | Acceptance criterion | Implementation files | Required tests |
|---|---|---|---|
| AC1 | Domain, Data, API, and shared-client boundaries; DTO-only HTTP surface; architecture test | All project files; `Solution.sln`; `Architecture/LayerRulesTests.cs`; API contracts/mappings | `LayerRulesTests` |
| AC2 | Authenticated users can CRUD their own drafts, publish/unpublish, and cannot access another user's drafts | `Recipe.cs`, `RecipeVersion.cs`, `RecipeApplicationService.cs`, `RecipeEndpoints.cs`, Data queries | `RecipeCrudTests`, `RecipeLifecycleTests`, domain lifecycle tests |
| AC3 | Invalid recipes and oversized input return `400` problem details | Domain validator, API validator, request DTOs, Kestrel limits | `RecipeValidationTests`, `RecipeCrudTests`, `SecurityTests` |
| AC4 | Published immutability, versioning, lifecycle conflicts, idempotency, and optimistic concurrency | Domain lifecycle/versioning, `UnitOfWork`, `IdempotencyRecord`, `IdempotencyQueries`, endpoints | `RecipeLifecycleTests`, `IdempotencyAndConcurrencyTests`, domain version tests |
| AC5 | Search/filter correctness, pagination, fail-closed allergens, and separate exclusion results | Recipe queries, allergen enum/status, search DTOs, list/detail client | `AllergenSemanticsTests`, `SearchTests` |
| AC6 | Real JWT validation, exact policies, public routes, authorization status codes, and scope configuration | AuthOptions, Policies, Program, endpoint policy mapping, Keycloak realm export, client auth | `SecurityTests` |
| AC7 | Recipe events, transactional outbox, RabbitMQ UserDeleted consumer, idempotent inbox, and outcome record | Outbox, `ProcessedEvent`, `UserDeletionOutcome`, handler, consumer adapter, hosted service, dispatcher | `OutboxAndUserDeletedTests`, `UserDeletedHandlerTests` |
| AC8 | React authorization-code + PKCE client, recipe editor, publication, listing, search, details, and user-friendly errors | `src/ChopChop.Client/src/recipes/*`, `App.tsx`, `auth.ts` | `RecipeScreens.test.tsx`, recipe `api.test.ts` |
| AC9 | OpenAPI and documentation for contracts, policies, sign-in, versioning, allergens, failures, local setup, observability, and threat model | `Program.cs`, `docs/api-contracts.md`, `docs/recipe-management.md`, `docs/threat-model.md`, ADR, local guide | Documentation review plus OpenAPI endpoint metadata checks |
| AC10 | Unit/integration edge-case coverage with SQL Server migrations and real JWT bearer validation | All test projects and support files | Entire Recipe Management unit/integration suites |
| AC11 | Zero-error/warning build, tests, format, vulnerability checks, and limitation reporting | Project files, settings, local/evidence docs | Release build, test, format, package vulnerability, and client checks |

## Implementation commits

Each step is a separate commit and references issue #23. No implementation starts
until Gate 1 is approved.

| Step | Commit or review action | Agent |
|---|---|---|
| 1 | `#23 Add Recipe Management project shells and solution entries` — create the four service projects, two test projects, project references, and `Solution.sln` entries only. | `default Agent mode + r-piv skill` |
| 2 | `#23 Implement Recipe Management domain model and invariants` — add Domain entities, enums, exceptions, validation, lifecycle, recipe-level allergens, and versioning. | `domain-dev` |
| 3 | `#23 Add Recipe Management persistence abstractions and SQL Server model` — add `IRepository`, `EfRepository`, `IUnitOfWork`, repositories, `IdempotencyRecord`, `IdempotencyQueries`, DbContext, configurations, and registrations. | `data-persistence` |
| 4 | `#23 Add initial Recipe Management migration` — generate, review, and add the service-owned migration and snapshot. | `data-persistence` |
| 5 | `#23 Implement Recipe Management draft application workflows` — add DTOs, current-user boundary, create/list/get/update/delete behavior, ownership, idempotency, and concurrency mapping. | `api-services` |
| 6 | `#23 Implement Recipe Management publication and search workflows` — add lifecycle commands, immutable versions, current/superseded public browsing, one filtered list route, pagination, allergen semantics, and equipment matching. | `api-services` |
| 7a | `#23 Add Recipe Management API authentication, policies, endpoints, and middleware` — add API composition, JWT validation, exact scopes, rate limiting, limits, problem details, health, OpenAPI, and route metadata inside `src/RecipeManagement.Api`. | `api-services` |
| 7b | `#23 Add Recipe Management Keycloak audience and scopes` — add the Recipe Management audience/client configuration and `recipes.read`, `recipes.write`, and `recipes.publish` scopes inside `infra/`. | `default Agent mode + r-piv skill` |
| 7c | `#23 Request Recipe Management scopes from the React client` — update only `src/ChopChop.Client/src/auth.ts` for authorization-code + PKCE scope requests. | `frontend-dev` |
| 8a | `#23 Add Recipe Management Data outbox inbox and RabbitMQ consumer adapter` — add Data-layer event persistence, inbox records, RabbitMQ subscription, retry, and dead-letter adapter inside `src/RecipeManagement.Data`. | `data-persistence` |
| 8b | `#23 Add Recipe Management UserDeleted handler and hosted consumer` — add the API-layer handler, hosted service, dispatcher, anonymization, and outcome orchestration inside `src/RecipeManagement.Api`. | `api-services` |
| 9 | `#23 Add Recipe Management domain and architecture tests` — add unit, boundary, lifecycle, validation, allergen, and application-service tests. | `testing` |
| 10 | `#23 Add Recipe Management integration security and persistence tests` — add WebApplicationFactory, JWT signing helper, migrations, API contract, ownership, pagination, idempotency, concurrency, security, consumer, and event tests. | `testing` |
| 11 | `#23 Add Recipe Management React screens and API client` — add the `recipes/` client folder, requested scopes, screens, validation, typed API calls, and minimal App integration. | `frontend-dev` |
| 12 | `#23 Add Recipe Management client tests` — test API headers/errors, authentication gating, PKCE scope configuration, validation, search/list/detail, draft editing, and lifecycle actions inside `src/ChopChop.Client`. | `frontend-dev` |
| 13 | `#23 Document Recipe Management contracts, threat model, and local operation` — update API contracts, ADR, service documentation, threat model, local setup, and evidence template inside `docs/`. | `default Agent mode + r-piv skill` |
| 14a | `#23 Validate Recipe Management build tests format and package audit` — run the repository validation commands and record exact outcomes or environment limitations. | `default Agent mode + r-piv skill` |
| 14b | Read-only review action: inspect the complete #23 diff, verify layer ownership, acceptance traceability, security, and failure-path coverage; produce the review report without editing implementation files. | `code-review` |

## Validation commands

Run from the repository root after implementation:

```bash
docker compose up -d
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --collect:"XPlat Code Coverage"
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
cd src/ChopChop.Client
npm ci
npm run build
npm run lint
npm test
```

Integration tests should run with LocalDB by default or with
`TEST_SQLSERVER_CONNECTION` set to the Docker SQL Server connection string.
RabbitMQ and Keycloak are required for the full local service workflow; tests
must use deterministic test event/JWT configuration and must not use a
permissive fake authentication scheme.

## Risks and implementation gates

- Do not change Interactive Review to add recipe versions; its reviews remain
  keyed by stable recipe ID.
- Do not treat a declared empty allergen list as medical or allergy advice.
- Do not allow draft edits to emit a recipe event. The first publication emits
  only `RecipePublished`; publication over an older published version emits
  only `RecipeUpdated`.
- Do not expose unpublished drafts through anonymous routes.
- Do not introduce a new broker or direct cross-service table dependency.
- The user-token forwarding decision is temporary and must be documented as a
  service-to-service security limitation.
- Gate 1 approval is required before implementation. Human review, peer review,
  merge, and milestone-level signoff remain separate responsibilities.
