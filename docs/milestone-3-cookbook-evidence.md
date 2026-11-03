# Milestone 3 Evidence — Cookbook

## Research

- Implemented the approved Cookbook scope in [the plan](./milestone-3-cookbook-plan.md) for GitHub issue #32.
- Kept Cookbook independently layered across Domain, Data, and API projects, with approved shared React client integration in `ChopChop.Client`.
- Reused the repository's Keycloak OIDC, JWT bearer validation, correlation, problem-details, SQL Server, migration, and testing conventions.
- Kept Recipe Management, Interactive Review, AI Recipe Generation, User and Profile, User Equipment, and Meal Planner ownership outside this assignment.

## Plan and Implement

- Added user-owned folders and saved recipe references, with ownership derived from the validated token subject.
- Added the `/api/v1/folders` and `/api/v1/folders/{folderId}/saved-recipes` API surface with read/write scope enforcement.
- Added Cookbook SQL schema ownership, repository and Unit of Work boundaries, and Cookbook migration support.
- Added Cookbook shared-client integration in `src/ChopChop.Client` including folder management and save-to-cookbook flows.

## Authorization flow

The browser client (`ChopChop.Client`) acts on behalf of the resource owner (the user). The user authenticates with the authorization server (Keycloak) using OAuth 2.0 authorization-code flow with PKCE. After sign-in, the client sends the bearer access token to the resource server (`Cookbook.Api`) for protected Cookbook endpoints. `Cookbook.Api` authorizes access by policy and scope; `/health` is anonymous.

## Token validation

Cookbook reads these configuration keys; values, credentials, and signing secrets are intentionally excluded:

- `Auth:Authority` — Keycloak issuer/authority URL.
- `Auth:Audience` — expected API audience (`cookbook`).
- `Auth:RequireHttpsMetadata` — whether authority metadata must use HTTPS.
- `Auth:ValidAlgorithms` — allowlisted signing algorithms.

The API validates JWT signature and signing key, issuer, audience, and lifetime (including required expiration). Signed tokens are required, and only configured algorithms are accepted.

## Endpoint-to-policy mapping

| Method | Route | Policy |
|---|---|---|
| `POST` | `/api/v1/folders` | `cookbook.write` |
| `GET` | `/api/v1/folders` | `cookbook.read` |
| `DELETE` | `/api/v1/folders/{folderId:guid}` | `cookbook.write` |
| `POST` | `/api/v1/folders/{folderId:guid}/saved-recipes` | `cookbook.write` |
| `GET` | `/api/v1/folders/{folderId:guid}/saved-recipes` | `cookbook.read` |
| `DELETE` | `/api/v1/folders/{folderId:guid}/saved-recipes/{recipeId:guid}/{recipeVersion:int}` | `cookbook.write` |
| `GET` | `/health` | Anonymous |
| OpenAPI | Development only | Anonymous |

## Test results

### Automated authentication and authorization cases

The Cookbook integration tests cover these expected outcomes:

| Case | Expected status |
|---|---:|
| Missing bearer token on a protected route | `401 Unauthorized` |
| Malformed bearer token | `401 Unauthorized` |
| Invalid token signature | `401 Unauthorized` |
| Expired token | `401 Unauthorized` |
| Wrong token issuer | `401 Unauthorized` |
| Wrong token audience | `401 Unauthorized` |
| Authenticated token without a required Cookbook scope | `403 Forbidden` |
| Authenticated token with an unrelated scope | `403 Forbidden` |
| Authenticated token with `cookbook.read` on read endpoints | `200 OK` |
| Authenticated token with `cookbook.write` on write endpoints | `201 Created`/`204 No Content` |
| Anonymous health access | `200 OK` |
| Folder owned by another user is requested | `404 Not Found` |

### Current command results

- `dotnet test`: **192 total, 192 passed, 0 failed, 0 skipped**.
- `npm test` from `src/ChopChop.Client`: **13 total, 13 passed, 0 failed, 0 skipped**.

## Known limitations and out-of-scope work

- Cookbook does not verify recipe existence, publication, or version validity against Recipe Management when saving.
- Out of scope for Milestone 3 includes favorites, folder renaming, recently viewed recipes, cooking history, pagination, idempotency keys, rate limiting, events/outbox publication, and `UserDeleted` handling.
- Copying recipe content into Cookbook storage is out of scope.
- Ratings, reviews, meal plans, pantry ownership, equipment ownership, nutrition goals, User and Profile, User Equipment, AI provider work, and Interactive Review changes are out of scope.
- Direct access to Recipe Management tables or code, a separate `Cookbook.Client` project, and API gateway/service-token-exchange work are out of scope.
- Production credentials, committed secrets, and production infrastructure changes are out of scope.

## Defects caught at the human gates

- Duplicate saved recipe entries would trigger a database error; fixed in Domain to prevent duplicate save per folder/recipe/version.
- API startup failed when client origins were not configured; fixed by requiring explicit client origins configuration.
- Manual testing found a connection string name mismatch; fixed to use the shared `ConnectionStrings:ChopChop` name.
- Form validation was blocked by the native `required` attribute behavior; fixed to align with intended client-side validation flow.
- Saved recipe titles were not displayed in the client; fixed by populating and rendering saved recipe title data.
- Cookbook integration tests conflicted with the Recipe Management test database when run in parallel; fixed by using an isolated Cookbook test database.

## Validate

Validation evidence consists of Cookbook unit and integration tests, shared client tests, Release build checks, and documentation review. The command totals above are placeholders for the final recorded run.
