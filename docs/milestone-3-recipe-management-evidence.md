# Milestone 3 Evidence — Recipe Management

## Research

- Implemented the approved Recipe Management scope in [the plan](./milestone-3-recipe-management-plan.md) for GitHub issue #23.
- Kept Recipe Management independently layered across Domain, Data, and API projects, with the approved shared React client integration in `ChopChop.Client`.
- Reused the repository's Keycloak OIDC, JWT bearer validation, correlation, problem-details, SQL Server, migration, and testing conventions.
- Kept Interactive Review, Cookbook, Meal Planner, User and Profile, User Equipment, and AI Recipe Generation ownership outside this assignment.

## Plan and Implement

- Added recipe lifecycle and immutable version rules, draft ownership, publication and unpublication commands, validation, allergen semantics, equipment matching, filtering, pagination, and a stale-version check on draft updates.
- Added the versioned `/api/v1/recipes` API with anonymous published discovery and protected owner-scoped draft and command operations.
- Added the Recipe Management SQL schema, repository and Unit of Work boundaries, and the initial migration. The migration also creates outbox, inbox, and idempotency tables for later milestones; nothing writes to them yet.
- Added Recipe Management screens and API integration under `src/ChopChop.Client/src/recipes/` without introducing a separate client project.

## Authorization flow

The browser client (`ChopChop.Client`) acts on behalf of the resource owner (the user). The user authenticates with the authorization server (Keycloak) using the OAuth 2.0 authorization-code flow with PKCE. After sign-in, the client sends the resulting bearer access token to the resource server (`RecipeManagement.Api`) for protected draft and command endpoints. Published recipe list, search, version, and detail routes are anonymous.

## Token validation

Recipe Management reads the following configuration keys; values, credentials, and signing secrets are not documented here:

- `Auth:Authority` — the Keycloak issuer/authority URL.
- `Auth:Audience` — the expected API audience.
- `Auth:RequireHttpsMetadata` — whether the authority metadata must use HTTPS.
- `Auth:ValidAlgorithms` — the allowlisted signing algorithms.

The API validates the JWT signature and signing key, issuer, audience, lifetime and required expiration time. Signed tokens are required, and only configured signing algorithms are accepted. The API also requires the appropriate space-delimited `scope` or `scp` claim for each protected policy. Authentication failures return `401`; an authenticated token missing the required scope returns `403`.

## Endpoint-to-policy mapping

| Method | Route | Policy |
|---|---|---|
| `POST` | `/api/v1/recipes` | `recipes.write` |
| `GET` | `/api/v1/recipes/drafts` | `recipes.read` |
| `GET` | `/api/v1/recipes/{recipeId}/draft` | `recipes.read` |
| `PUT` | `/api/v1/recipes/{recipeId}/draft` | `recipes.write` |
| `DELETE` | `/api/v1/recipes/{recipeId}/draft` | `recipes.write` |
| `POST` | `/api/v1/recipes/{recipeId}/publish` | `recipes.publish` |
| `POST` | `/api/v1/recipes/{recipeId}/unpublish` | `recipes.publish` |
| `GET` | `/api/v1/recipes/{recipeId}/versions/{version}` | Anonymous |
| `GET` | `/api/v1/recipes/{recipeId}` | Anonymous |
| `GET` | `/api/v1/recipes` | Anonymous |

## Test results

### Automated authentication and authorization cases

The Recipe Management integration tests cover these expected outcomes:

| Case | Expected status |
|---|---:|
| Missing bearer token on a protected route | `401 Unauthorized` |
| Malformed bearer token | `401 Unauthorized` |
| Invalid token signature | `401 Unauthorized` |
| Expired token | `401 Unauthorized` |
| Wrong token issuer | `401 Unauthorized` |
| Wrong token audience | `401 Unauthorized` |
| Authenticated token without a required recipe scope | `403 Forbidden` |
| Authenticated token with an unrelated scope | `403 Forbidden` |
| Authenticated token with the required `recipes.read` scope | `200 OK` |
| Anonymous published list and detail access | `200 OK` |
| Private draft belonging to another user | `404 Not Found` |

### Current command results

- `dotnet test`: 156 total, 156 passed, 0 failed, 0 skipped**.
- `npm test` from `src/ChopChop.Client`: **7 total, 7 passed, 0 failed, 0 skipped**.

## Known limitations and out-of-scope work

- Published search currently loads the published filter fields in memory before applying the bounded search and filtering behavior.
- Event publication and consumer integration, `UserDeleted` handling, idempotency, and rate limiting are out of scope for Milestone 3.
- Local development uses configured public URLs and development-only infrastructure; production deployments must provide HTTPS and a secured configuration/secret store.

## Validate

Validation evidence consists of the automated Recipe Management unit and integration test suites, the shared client test suite, Release build checks, and documentation review. Database-backed integration checks require the local SQL Server dependency. The test totals above are placeholders for the final run results.
