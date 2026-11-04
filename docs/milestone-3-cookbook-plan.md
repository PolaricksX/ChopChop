# Milestone 3 Implementation Plan: Cookbook (#32)

## Status

**Gate 1 — approved by Léa-Maude Montpetit on 2026-11-03.**

This plan is limited to Cookbook, its approved shared React client work, its
owned SQL Server schema and migration, Keycloak configuration required for the
service, tests, and documentation. It does not modify Recipe Management,
Interactive Review, AI Recipe Generation, User and Profile, User Equipment,
Meal Planner, or another service's tables or application code.

## Research (R)

### Source requirements

- GitHub issue [#32 — Cookbook](https://github.com/PolaricksX/ChopChop/issues/32).
- `copilot-instructions.md`, especially the ownership, one-way layering,
  security, async I/O, correlation, DTO, SQL Server, testing, and documentation
  requirements.
- `.github/skills/r-piv-skill.md`, requiring research, a minimal ordered plan,
  implementation evidence, and validation evidence.
- `docs/milestone-3-recipe-management-plan.md`, the closest Milestone 3
  implementation pattern for Domain, Data, API, Keycloak, JWT integration
  tests, and the shared React client.
- `docs/adr/0002-service-architecture.md` and
  `docs/adr/0003-identity-and-client-auth.md` for independently runnable
  services, shared client integration, and OIDC identity decisions.

### Scope summary

The Cookbook service will provide user-owned folders and saved recipe
references. A saved recipe stores a stable recipe GUID and integer recipe
version; it does not copy recipe content and does not verify the recipe against
Recipe Management. Authenticated users can create, list, and delete their own
folders and add, list, and remove saved recipe references in those folders.

The implementation will add three projects under `src/`:
`Cookbook.Domain`, `Cookbook.Data`, and `Cookbook.Api`, plus
`tests/Cookbook.UnitTests` and `tests/Cookbook.IntegrationTests`. The shared
React client remains `src/ChopChop.Client`; no separate Cookbook client project
will be created.

### Constraints and decisions to carry into implementation

1. Dependencies are one-way: `Cookbook.Api -> Cookbook.Data -> Cookbook.Domain`.
   Domain references neither EF Core nor ASP.NET Core. API returns DTOs only.
2. The service uses its own `cookbook` SQL Server schema in the shared database,
   EF Core SQL Server, repositories, Unit of Work, typed EF configurations, and
   an initial migration.
3. Folder and saved-recipe ownership is always derived from the validated JWT
   `sub` claim. No request DTO accepts an owner/user ID. A resource belonging to
   another user is indistinguishable from a missing resource and returns `404`.
4. Folder names are required, trimmed, and bounded by the Domain/API
   validation rules. Recipe ID must be a non-empty GUID and recipe version must
   be a positive integer.
5. A folder delete cascades its saved-recipe rows in the Cookbook schema.
6. No pagination, idempotency, rate limiting, events, UserDeleted handling, or
   cross-service recipe verification is introduced in this milestone.
7. All service I/O is asynchronous and accepts a `CancellationToken`.
8. The API uses the framework JWT bearer handler, configurable authority and
   audience, signed-token/issuer/audience/lifetime validation, and exact
   `cookbook.read` and `cookbook.write` scope policies. `/health` is explicitly
   anonymous and checks only the Cookbook database.
9. CORS runs before authentication and allows `Authorization`, `Content-Type`,
   and `X-Correlation-Id`. The API project has a `UserSecretsId`; no secret is
   documented or committed.
10. Keycloak changes preserve `chopchop-web`'s existing default scopes:
    `profile`, `email`, `roles`, `web-origins`, `acr`, and `basic`. The Cookbook
    audience is a default client scope, while Cookbook read/write scopes are
    requested as optional scopes by the browser client.
11. Client API functions obtain the current access token from `userManager` for
    each request. They do not rely on a stale token passed through screen props.
12. The client sends no `Idempotency-Key` header for Cookbook operations.
13. EF configurations use typed shadow properties where concurrency or audit
    metadata is needed. Updates use tracked entities so ownership and state
    changes are persisted correctly.

### Existing files and patterns to copy

- `src/RecipeManagement.Domain/RecipeManagement.Domain.csproj` and its
  framework-free entities, exceptions, and pure validation organization.
- `src/RecipeManagement.Data/RecipeManagement.Data.csproj` for Domain
  references and centrally managed EF Core SQL Server packages.
- `src/RecipeManagement.Data/Context/RecipeManagementDbContext.cs` for an
  owned default schema and explicit configuration registration.
- `src/RecipeManagement.Data/Configurations/*` for typed EF entity mappings,
  indexes, lengths, foreign keys, and shadow properties.
- `src/RecipeManagement.Data/Repositories/IRepository.cs`, `IUnitOfWork.cs`,
  `EfRepository.cs`, `RepositoryFactory.cs`, `RepositoryProvider.cs`, and
  `UnitOfWork.cs` for the EF boundary and transaction/save pattern.
- `src/RecipeManagement.Data/DesignTimeDbContextFactory.cs` for `dotnet ef`
  migrations and the `ConnectionStrings__Cookbook` override.
- `src/RecipeManagement.Api/Program.cs` for registration order, CORS before
  authentication, JWT bearer setup, fallback authorization, problem details,
  health checks, and thin endpoint mapping.
- `src/RecipeManagement.Api/Infrastructure/AuthOptions.cs` and `Policies.cs`
  for configurable JWT validation and space-delimited `scope`/`scp` handling.
- `src/RecipeManagement.Api/Services/CurrentUser.cs` for obtaining only the
  validated subject claim.
- `src/RecipeManagement.Api/Contracts/*` and
  `Services/RecipeApplicationService.cs` for DTO-only boundaries, mappings,
  validation, and application orchestration.
- `tests/RecipeManagement.UnitTests/Architecture/LayerRulesTests.cs` for the
  dependency-direction architecture test and domain test layout.
- `tests/RecipeManagement.IntegrationTests/Support/ApiFactory.cs` and
  `Support/TestJwt.cs` for LocalDB by default, a connection-string override,
  test signing credentials, and framework JWT bearer integration tests.
- `infra/keycloak/realm-export/chopchop-realm.json` for public PKCE client,
  audience client scopes, and optional API scopes.
- `src/ChopChop.Client/src/auth.ts`, `api.ts`, `App.tsx`, and the existing
  recipe screens/tests for OIDC callback handling, HTTP error parsing, and
  minimal shared-client integration. Recipe detail is the location for the
  `Save to cookbook` action.

## Proposed API contract (contract first)

All endpoints use JSON DTOs, GUID recipe/folder identifiers, UTC
`DateTimeOffset` values where timestamps are exposed, RFC 9457-style problem
details with `X-Correlation-Id`, and cancellation-aware application services.
The API never serializes Domain entities or EF entities.

### DTOs

| DTO | Shape and purpose |
|---|---|
| `CreateFolderRequest` | `{ "name": "string" }`; the name is required after trimming and is bounded by the agreed validation maximum. |
| `FolderResponse` | `{ "id": "guid", "name": "string", "createdAtUtc": "date-time" }`. |
| `SaveRecipeRequest` | `{ "recipeId": "guid", "recipeVersion": "integer" }`; both values are required, and `recipeVersion` is positive. |
| `SavedRecipeResponse` | `{ "folderId": "guid", "recipeId": "guid", "recipeVersion": "integer", "savedAtUtc": "date-time" }`. |
| Problem details | `application/problem+json` containing `type`, `title`, `status`, `detail`, and the correlation ID according to shared middleware conventions. |

### Endpoints

| Method | Route | Policy | Success and required failure statuses |
|---|---|---|---|
| `POST` | `/api/v1/folders` | `cookbook.write` | `201 Created` with `FolderResponse` and `Location`; `400`, `401`, `403` |
| `GET` | `/api/v1/folders` | `cookbook.read` | `200 OK` with `FolderResponse[]`; `401`, `403` |
| `DELETE` | `/api/v1/folders/{folderId:guid}` | `cookbook.write` | `204 No Content`; `401`, `403`, `404` |
| `POST` | `/api/v1/folders/{folderId:guid}/saved-recipes` | `cookbook.write` | `201 Created` with `SavedRecipeResponse` and `Location`; `400`, `401`, `403`, `404` |
| `GET` | `/api/v1/folders/{folderId:guid}/saved-recipes` | `cookbook.read` | `200 OK` with `SavedRecipeResponse[]`; `401`, `403`, `404` |
| `DELETE` | `/api/v1/folders/{folderId:guid}/saved-recipes/{recipeId:guid}/{recipeVersion:int}` | `cookbook.write` | `204 No Content`; `400`, `401`, `403`, `404` |
| `GET` | `/health` | Anonymous | `200` when the Cookbook database health check is healthy; the framework health failure status otherwise |
| OpenAPI | Development only | Anonymous | Available only in Development, consistent with existing service behavior |

`GET /api/v1/folders` and all folder child routes are owner-scoped by the
validated token subject. A missing folder, another user's folder, or a saved
recipe that is not in the caller's folder returns `404`. Invalid JSON or
validation failures return `400`; missing/invalid credentials return `401`;
authenticated callers without the required scope return `403`.

## Acceptance criteria mapping

| ID | Criterion |
|---|---|
| AC1 | Domain, Data, and Api follow the one-way dependency direction, an architecture test passes, and the API returns DTOs only. |
| AC2 | An authenticated user can create, list, and delete their folders, and add, list, and remove saved recipes in their folders. A user cannot access another user's folders. |
| AC3 | Invalid input (empty folder name, missing recipe ID or version) is rejected with `400` problem details. |
| AC4 | A protected request without a token returns `401`; an invalid, expired, wrong-issuer, or wrong-audience token is rejected; a valid token without the required scope returns `403`; a valid token with the required scope succeeds while a different scope does not; the public `/health` endpoint is accessible without a token. |
| AC5 | The React client has a Cookbook tab to manage folders and saved recipes, and can save a recipe from the recipe detail screen. |
| AC6 | Unit tests cover the domain rules; integration tests cover the API and all authentication/authorization cases above using the framework JWT bearer handler with test signing credentials. |
| AC7 | Documentation describes the authorization flow, token-validation settings, endpoint-to-policy mapping, and test results. |
| AC8 | `dotnet build` and `dotnet test` pass with zero errors and zero warnings. |

## File-by-file implementation plan

Each row is an intended implementation artifact. The modeled-on file preserves
existing repository conventions; the acceptance criteria identify why the file
is needed.

### Domain layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `Solution.sln` | Add Cookbook source and test projects. | Existing solution project entries | AC1, AC6 |
| `src/Cookbook.Domain/Cookbook.Domain.csproj` | Framework-free .NET 10 Domain project with nullable reference types. | `src/RecipeManagement.Domain/RecipeManagement.Domain.csproj` | AC1 |
| `src/Cookbook.Domain/Entities/Folder.cs` | User-owned folder aggregate with stable ID, owner ID, name, timestamps, and saved-reference collection. | `src/RecipeManagement.Domain/Entities/Recipe.cs` | AC2, AC3 |
| `src/Cookbook.Domain/Entities/SavedRecipe.cs` | Saved recipe reference containing folder ID, recipe ID, positive version, and saved timestamp. | `src/RecipeManagement.Domain/Entities/RecipeVersion.cs` | AC2, AC3 |
| `src/Cookbook.Domain/Exceptions/CookbookDomainExceptions.cs` | Validation exceptions. | `src/RecipeManagement.Domain/Exceptions/RecipeDomainExceptions.cs` | AC3 |
| `src/Cookbook.Domain/Services/FolderRules.cs` | Pure folder-name normalization and validation. | `src/RecipeManagement.Domain/Services/RecipeValidator.cs` | AC2, AC3 |
| `src/Cookbook.Domain/Services/SavedRecipeRules.cs` | Pure recipe ID/version rules. | `src/RecipeManagement.Domain/Services/RecipeLifecycle.cs` and Recipe Management domain rules | AC2, AC3 |

### Data layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/Cookbook.Data/Cookbook.Data.csproj` | Reference Domain and centrally managed EF Core SQL Server packages. | `src/RecipeManagement.Data/RecipeManagement.Data.csproj` | AC1 |
| `src/Cookbook.Data/IRepository.cs` | Generic repository boundary without exposing EF to API services. | `src/RecipeManagement.Data/IRepository.cs` | AC1 |
| `src/Cookbook.Data/Repositories/IUnitOfWork.cs` | Unit of Work contract for repositories and one async save boundary. | `src/RecipeManagement.Data/Repositories/IUnitOfWork.cs` | AC1 |
| `src/Cookbook.Data/Repositories/EfRepository.cs` | Generic EF repository implementation. | `src/RecipeManagement.Data/Repositories/EfRepository.cs` | AC1 |
| `src/Cookbook.Data/Repositories/RepositoryFactory.cs` | Scoped repository factory for registered repositories. | `src/RecipeManagement.Data/Repositories/RepositoryFactory.cs` | AC1 |
| `src/Cookbook.Data/Repositories/RepositoryProvider.cs` | Scoped repository cache/provider. | `src/UserService.Data/Repositories/RepositoryFactory.cs` | AC1 |
| `src/Cookbook.Data/Repositories/UnitOfWork.cs` | Transaction/save boundary and database exception translation. | `src/RecipeManagement.Data/Repositories/UnitOfWork.cs` | AC1, AC2 |
| `src/Cookbook.Data/Context/CookbookDbContext.cs` | DbSets for folders and saved recipes; default schema `cookbook`; explicit configuration registration. | `src/RecipeManagement.Data/Context/RecipeManagementDbContext.cs` | AC1 |
| `src/Cookbook.Data/Configurations/FolderConfiguration.cs` | Keys, owner/name lengths, indexes, and cascade behavior. | `src/RecipeManagement.Data/Configurations/RecipeConfiguration.cs` | AC1, AC2 |
| `src/Cookbook.Data/Configurations/SavedRecipeConfiguration.cs` | Foreign key, folder/recipe/version indexes, timestamps, and typed shadow properties if required. | `src/RecipeManagement.Data/Configurations/RecipeVersionConfiguration.cs` | AC1, AC2 |
| `src/Cookbook.Data/Repositories/ICookbookQueries.cs` | Owner-scoped query contract returning application-friendly projections without EF types. | `src/RecipeManagement.Data/Repositories/IRecipeQueries.cs` | AC1, AC2 |
| `src/Cookbook.Data/Repositories/CookbookQueries.cs` | No-tracking folder/reference queries and owner predicates. | `src/RecipeManagement.Data/Repositories/RecipeQueries.cs` | AC2 |
| `src/Cookbook.Data/DesignTimeDbContextFactory.cs` | Permit migrations with `ConnectionStrings__Cookbook` and a safe LocalDB fallback. | `src/RecipeManagement.Data/DesignTimeDbContextFactory.cs` | AC1 |
| `src/Cookbook.Data/ServiceCollectionExtensions.cs` | Register DbContext, repositories, provider, factory, and Unit of Work as scoped services. | `src/RecipeManagement.Data/ServiceCollectionExtensions.cs` | AC1 |
| `src/Cookbook.Data/Migrations/<timestamp>_InitialCookbook.cs` | Create the owned Cookbook schema and tables. | `src/RecipeManagement.Data/Migrations/*` | AC1 |
| `src/Cookbook.Data/Migrations/CookbookDbContextModelSnapshot.cs` | Store the EF model snapshot. | Recipe Management migration snapshot | AC1 |

### API layer

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/Cookbook.Api/Cookbook.Api.csproj` | Web project with `UserSecretsId`, Data/Domain/shared references, JWT, OpenAPI, EF design, Serilog, and health packages. | `src/RecipeManagement.Api/RecipeManagement.Api.csproj` | AC1, AC4 |
| `src/Cookbook.Api/Contracts/CookbookContracts.cs` | Public request/response DTOs defined by this contract. | `src/RecipeManagement.Api/Contracts/RecipeContracts.cs` | AC1 |
| `src/Cookbook.Api/Contracts/CookbookMappings.cs` | Map folders and saved references to DTOs only. | `src/RecipeManagement.Api/Contracts/RecipeMappings.cs` | AC1 |
| `src/Cookbook.Api/Contracts/CookbookRequestValidator.cs` | Boundary checks for JSON shape, name length, GUIDs, and positive versions. | `src/RecipeManagement.Api/Contracts/RecipeRequestValidator.cs` | AC3 |
| `src/Cookbook.Api/Infrastructure/AuthOptions.cs` | Bind and validate `Auth:Authority`, `Auth:Audience`, HTTPS metadata, and algorithm settings. | `src/RecipeManagement.Api/Infrastructure/AuthOptions.cs` | AC4 |
| `src/Cookbook.Api/Infrastructure/Policies.cs` | Define exactly `cookbook.read` and `cookbook.write` and evaluate `scope`/`scp`. | `src/RecipeManagement.Api/Infrastructure/Policies.cs` | AC4 |
| `src/Cookbook.Api/Infrastructure/ApiExceptionHandler.cs` | Map validation, ownership/not-found, and database exceptions to safe problem details. | `src/RecipeManagement.Api/Infrastructure/ApiExceptionHandler.cs` | AC3, AC4 |
| `src/Cookbook.Api/Services/ICurrentUser.cs` | Boundary exposing the validated current subject. | `src/RecipeManagement.Api/Services/ICurrentUser.cs` | AC2, AC4 |
| `src/Cookbook.Api/Services/CurrentUser.cs` | Read only the validated `sub` claim and reject an absent/invalid subject. | `src/RecipeManagement.Api/Services/CurrentUser.cs` | AC2, AC4 |
| `src/Cookbook.Api/Services/CookbookApplicationService.cs` | Orchestrate owner-scoped folder/reference commands, queries, domain validation, tracked updates, and DTO mapping. | `src/RecipeManagement.Api/Services/RecipeApplicationService.cs` | AC2, AC3 |
| `src/Cookbook.Api/Endpoints/CookbookEndpoints.cs` | Thin `/api/v1/folders` endpoint mapping, policies, status codes, Location headers, and cancellation tokens. | `src/RecipeManagement.Api/Endpoints/RecipeEndpoints.cs` | AC1, AC2, AC4 |
| `src/Cookbook.Api/Program.cs` | Composition root, CORS-before-authentication order, JWT bearer, fallback authorization, health, problem details, OpenAPI, Serilog, and service registration. | `src/RecipeManagement.Api/Program.cs` | AC1, AC4 |
| `src/Cookbook.Api/appsettings.json` | Safe configuration keys and no secrets. | `src/RecipeManagement.Api/appsettings.json` | AC4 |
| `src/Cookbook.Api/appsettings.Development.json` | Local non-secret database, client origin, Auth, and logging defaults. | `src/RecipeManagement.Api/appsettings.Development.json` | AC4 |
| `src/Cookbook.Api/Properties/launchSettings.json` | Local Cookbook API port/profile. | `src/RecipeManagement.Api/Properties/launchSettings.json` | AC4 |

### Infrastructure and Keycloak

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `infra/keycloak/realm-export/chopchop-realm.json` | Preserve `profile`, `email`, `roles`, `web-origins`, `acr`, and `basic` as `chopchop-web` default scopes; add Cookbook audience as a default client scope and `cookbook.read`/`cookbook.write` as optional scopes without removing existing Recipe Management scopes. | Existing Keycloak realm export and Recipe Management realm changes | AC4 |
| `docker-compose.yml` | No new identity service; verify the existing Keycloak import continues to load the expanded realm export. | Existing Keycloak service definition | AC4 |

### Client layer — shared `ChopChop.Client`

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `src/ChopChop.Client/src/auth.ts` | Request `cookbook.read` and `cookbook.write` in the authorization-code + PKCE scope list while retaining existing scopes. | Existing OIDC setup and Recipe Management scope additions | AC4, AC5 |
| `src/ChopChop.Client/src/cookbook/types.ts` | TypeScript DTOs for folders, saved recipe references, and problem details. | `src/ChopChop.Client/src/recipes/types.ts` and `api.ts` types | AC1, AC5 |
| `src/ChopChop.Client/src/cookbook/api.ts` | HTTP functions for folder/reference operations; obtain the current access token from `userManager` and send correlation/content headers. | `src/ChopChop.Client/src/api.ts` and recipe API conventions | AC4, AC5 |
| `src/ChopChop.Client/src/cookbook/validation.ts` | Client-side bounded validation for folder names and recipe reference fields without replacing server validation. | Recipe client validation patterns | AC3, AC5 |
| `src/ChopChop.Client/src/cookbook/CookbookTab.tsx` | Authenticated tab to list/create/delete folders and list/add/remove saved recipes. | Existing `App.tsx` authenticated panels | AC2, AC5 |
| `src/ChopChop.Client/src/cookbook/CookbookTab.test.tsx` | Component tests for sign-in state, folder actions, saved-reference actions, validation, and API errors. | `src/ChopChop.Client/src/recipes/RecipeScreens.test.tsx` and `App.test.tsx` | AC5, AC6 |
| `src/ChopChop.Client/src/cookbook/api.test.ts` | HTTP contract tests for current-token lookup, routes, headers, response mapping, and problem details. | Existing client API tests | AC4, AC5, AC6 |
| `src/ChopChop.Client/src/recipes/RecipeDetails.tsx` | Add the `Save to cookbook` action and folder selection using the Cookbook API. | Existing Recipe Management recipe detail screen | AC2, AC5 |
| `src/ChopChop.Client/src/App.tsx` | Minimal Cookbook tab/view integration and selected-folder/detail state. | Existing App panel/view integration | AC5 |
| `src/ChopChop.Client/src/App.css` | Minimal Cookbook layout, form, list, and error styles. | Existing shared client styles | AC5 |
| `src/ChopChop.Client/README.md` | Document Cookbook API URL, OIDC scopes, PKCE, and local run configuration. | Existing client README and Recipe Management client documentation | AC5, AC7 |
| `src/ChopChop.Client/.env.example` | Add safe Cookbook API URL placeholder if the client uses a separate base URL. | Existing client environment template | AC5, AC7 |

### Tests

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `tests/Cookbook.UnitTests/Cookbook.UnitTests.csproj` | Unit-test project referencing Domain and architecture-test dependencies. | `tests/RecipeManagement.UnitTests/RecipeManagement.UnitTests.csproj` | AC1, AC6 |
| `tests/Cookbook.UnitTests/Architecture/LayerRulesTests.cs` | Assert Domain has no EF/ASP.NET dependency and API/Data/Domain references remain one-way. | `tests/RecipeManagement.UnitTests/Architecture/LayerRulesTests.cs` | AC1, AC6 |
| `tests/Cookbook.UnitTests/Domain/FolderTests.cs` | Test folder ownership/name normalization, empty-name rejection, and delete behavior. | Recipe Management domain tests | AC3, AC6 |
| `tests/Cookbook.UnitTests/Domain/SavedRecipeTests.cs` | Test missing/invalid recipe identity, positive version, and aggregate behavior. | Recipe Management domain tests | AC3, AC6 |
| `tests/Cookbook.IntegrationTests/Cookbook.IntegrationTests.csproj` | Web application test project with SQL Server/JWT test dependencies. | `tests/RecipeManagement.IntegrationTests/RecipeManagement.IntegrationTests.csproj` | AC6 |
| `tests/Cookbook.IntegrationTests/Support/TestJwt.cs` | Generate test signing credentials and tokens with configurable issuer, audience, expiry, and scopes. | `tests/RecipeManagement.IntegrationTests/Support/TestJwt.cs` | AC4, AC6 |
| `tests/Cookbook.IntegrationTests/Support/ApiFactory.cs` | Configure the framework JWT bearer handler, static signing-key metadata, LocalDB default, connection override, and migration lifecycle. | `tests/RecipeManagement.IntegrationTests/Support/ApiFactory.cs` | AC4, AC6 |
| `tests/Cookbook.IntegrationTests/FolderEndpointsTests.cs` | Cover create/list/delete, validation, and ownership isolation. | Recipe Management endpoint tests | AC2, AC3, AC6 |
| `tests/Cookbook.IntegrationTests/SavedRecipeEndpointsTests.cs` | Cover add/list/remove, invalid recipe ID/version, and ownership isolation. | Recipe Management endpoint tests | AC2, AC3, AC6 |
| `tests/Cookbook.IntegrationTests/AuthenticationTests.cs` | Cover missing, malformed, invalid-signature, expired, wrong-issuer, and wrong-audience tokens. | `tests/RecipeManagement.IntegrationTests/AuthenticationTests.cs` | AC4, AC6 |
| `tests/Cookbook.IntegrationTests/AuthorizationTests.cs` | Cover unrelated/missing scopes as `403`, required read/write scopes as success, and anonymous `/health` as success. | Recipe Management authorization tests | AC4, AC6 |

### Documentation

| File | Purpose | Modeled on | Acceptance criteria |
|---|---|---|---|
| `docs/milestone-3-cookbook-plan.md` | This contract-first research and implementation plan. | `docs/milestone-3-recipe-management-plan.md` | AC7 |
| `docs/milestone-3-cookbook-evidence.md` | Record implementation scope, authorization flow, validation settings, endpoint-policy mapping, test results, and known limitations after implementation. | `docs/milestone-3-recipe-management-evidence.md` | AC7, AC8 |
| `docs/local-development.md` | Add Cookbook API port, `ConnectionStrings__Cookbook`, migration command, Keycloak scopes/audience, client URL, and safe local setup. | Existing local development guide and Recipe Management plan | AC7 |

## Authorization flow and token validation

The browser `ChopChop.Client` is the public OAuth2/OIDC client and acts for the
resource owner. Keycloak is the authorization server. The browser uses the
authorization-code flow with PKCE and no client secret. `Cookbook.Api` is the
resource server; it receives the bearer access token over HTTP and protects all
Cookbook endpoints except `/health`.

`Cookbook.Api` reads `Auth:Authority`, `Auth:Audience`,
`Auth:RequireHttpsMetadata`, and `Auth:ValidAlgorithms` from environment-specific
configuration. The framework JWT bearer handler validates the signature and
signing key, issuer, audience, lifetime, required expiration, signed-token
requirement, and configured algorithms. The API reads only the validated `sub`
claim for ownership. `cookbook.read` is required for list operations;
`cookbook.write` is required for create/delete/add/remove operations. Missing or
invalid authentication is `401`; an authenticated token without the exact
required scope is `403`.

Keycloak's `chopchop-web` default scopes must continue to include `profile`,
`email`, `roles`, `web-origins`, `acr`, and `basic`. The Cookbook audience is
also a default client scope. `cookbook.read` and `cookbook.write` are optional
client scopes requested by `auth.ts`; adding them must not remove existing
Recipe Management scopes or built-in defaults.

## Commit list and agent ownership

Commits should remain focused and follow the repository's implementation order.

| Commit | Agent | Contents |
|---|---|---|
| `#32 Add Cookbook project shells and solution entries` | `default Agent mode + r-piv skill` | Empty Cookbook.Domain, Cookbook.Data, Cookbook.Api, Cookbook.UnitTests, and Cookbook.IntegrationTests projects, project references, UserSecretsId in Cookbook.Api, and Solution.sln entries only. |
| `#32 Add Cookbook Domain model` | `domain-dev` | `src/Cookbook.Domain` only: entities, pure validation/rules, and exceptions. |
| `#32 Add Cookbook persistence` | `data-persistence` | Data project, EF context/configurations, repositories/Unit of Work, design-time factory, migration, and persistence registrations. |
| `#32 Add Cookbook API services` | `api-services` | DTO contract, mappings, application service, current-user boundary, exception handling, policies, endpoints, Program composition, health, CORS, and JWT configuration. |
| `#32 Configure Cookbook Keycloak scopes` | `default Agent mode + r-piv skill` | Keycloak realm audience and optional scope configuration in `infra/`. |
| `#32 Add Cookbook client screens` | `frontend-dev` | Cookbook API/types/screens, Recipe Details save action, App integration, and client tests. |
| `#32 Add Cookbook tests` | `testing` | Domain unit tests, architecture test, integration factory/JWT helpers, endpoint tests, and authentication/authorization tests. |
| `#32 Add Cookbook documentation and evidence` | `default Agent mode + r-piv skill` | Local development documentation, implementation evidence, validation results, and final R-PIV report. |
| `#32 Review Cookbook implementation` | `code-review` | Read-only review of the final diff for security, ownership boundaries, DTO/API leakage, and failure-path coverage. |

## Out of scope for Milestone 3

- Favorites.
- Renaming folders.
- Recently viewed recipes.
- Cooking history.
- Pagination.
- Idempotency keys and `Idempotency-Key` request handling.
- Rate limiting.
- Events, outbox publishing, and `UserDeleted` handling.
- Verifying recipe existence, publication, or version validity against Recipe
  Management when saving.
- Copying recipe content into Cookbook storage.
- Ratings, reviews, folders or data owned by another service, meal plans,
  pantry ownership, equipment ownership, nutrition goals, User and Profile,
  User Equipment, AI provider work, or changes to Interactive Review.
- Direct access to Recipe Management tables or code.
- A separate `Cookbook.Client` project.
- An API gateway, service-to-service token exchange, or a new identity/broker
  service.
- Production credentials, secrets, or production infrastructure changes.

## Validation plan (V)

1. Restore and build the solution in Release with warnings treated as errors.
2. Run Cookbook unit and integration tests, including all JWT and policy cases,
   with LocalDB by default or the documented connection-string override.
3. Run shared client tests and the client production build.
4. Run `dotnet format --verify-no-changes` and the repository's vulnerability
   check when dependencies are touched.
5. Verify the migration creates only the `cookbook` schema and that health is
   database-only and anonymous.
6. Review endpoint-policy mapping, CORS middleware order, DTO-only responses,
   current-user claim handling, absence of secrets, absence of
   `Idempotency-Key`, and absence of unrelated service changes.
7. Record exact commands, totals, warnings, and any environment limitation in
   `docs/milestone-3-cookbook-evidence.md` after implementation.
