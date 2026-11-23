# Milestone 4 Implementation Plan: Cross-Device Profile Retrieval and Private Picture Access (#94)

## Status

**Approved at Gate 1 by Léa-Maude Montpetit on 2026-11-23.**

This plan is intentionally limited to the requested scope for issue #94:

- `src/UserService.*`
- `tests/UserService.*`
- `src/ChopChop.Client` (profile/auth-related parts only)
- `docs/*`

No Keycloak realm changes (`infra/keycloak/*`) are included.

This plan builds on issue #88 artifacts:

- `docs/milestone-4-profile-picture-plan.md`
- `docs/milestone-4-profile-picture-evidence.md`

## Research baseline carried into this plan

- Existing UserService API endpoints currently include:
  - `POST /api/v1/users`
  - `GET /api/v1/users/{id}`
  - `PUT /api/v1/users/{id}`
  - `DELETE /api/v1/users/{id}`
  - `PUT /api/v1/users/{id}/profile-picture` (upload from #88)
- A profile-picture retrieval endpoint does not yet exist.
- `IUserProfileRepository` already has `GetBySubjectAsync(string subject, ...)`, which supports a `/me` lookup path without requiring a stored profile ID.
- `IProfilePictureStorage` currently supports only `SaveAsync` and `DeleteAsync`; there is no read method yet.
- `LocalProfilePictureStorage` already validates keys against a strict GUID+extension pattern and verifies resolved paths stay under `ProfilePictures:RootPath` for save/delete operations.
- UserService request logging is enabled through Serilog request logging middleware in `src/UserService.Api/Program.cs`.
- Authentication currently rejects query-string tokens (`tests/UserService.IntegrationTests/AuthenticationTests.cs` already asserts `?access_token=` is unauthorized).

Client baseline (`src/ChopChop.Client`):

- Auth0 provider is configured in `src/ChopChop.Client/src/main.tsx` with:
  - `cacheLocation="localstorage"`
  - token options from `src/ChopChop.Client/src/authProviderOptions.ts`:
    - `useRefreshTokens: true`
    - `useRefreshTokensFallback: false`
    - `useMrrt: true`
- API calls attach bearer tokens through the `Authorization` header via `fetch` in `src/ChopChop.Client/src/api.ts`.
- No profile page or profile-picture upload/retrieval UI is currently present in the client.
- Sign-out currently calls only Auth0 logout (`logout({ logoutParams: { returnTo: ... } })`) in `src/ChopChop.Client/src/App.tsx`; it does not explicitly clear app-side cached profile data, query caches, or object URLs.

Gate 1 resolutions applied to baseline constraints:

- Auth0 browser token caching remains unchanged (`cacheLocation="localstorage"`, refresh-token settings unchanged) because it is app-wide behavior and switching to memory with `useRefreshTokensFallback: false` would force sign-in on reload.
- No new log-redaction middleware is planned; current request logging behavior is verified by tests that assert raw token values never appear in captured logs.

## Approved decisions (fixed inputs)

These decisions are fixed and drive all implementation steps:

1. Private images: no public or signed links. The client fetches the picture with its bearer token and displays it via `URL.createObjectURL`; object URLs are revoked when replaced, on unmount, and on sign-out. No token ever appears in a URL.
2. Add `GET /api/v1/users/me` (`users.read`) that returns the caller's profile resolved from token `sub`; return `404` if no profile exists.
3. Add `GET /api/v1/users/{id}/profile-picture` (`users.read`), owner-only (`404` for non-owners and when no picture exists), streaming stored file with stored content type, `Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`.
4. Add a read method to `IProfilePictureStorage` and `LocalProfilePictureStorage` that only accepts server-generated keys and rejects path traversal or keys resolving outside `ProfilePictures:RootPath`.
5. Tokens must never be accepted in query strings, written to logs, or persisted in server-side data (database rows, stored files, API responses). Browser localStorage token caching by the Auth0 SDK remains in place by Gate 1 approval. Add tests that capture logs and assert the raw bearer token value does not appear.
6. Sign-out must call Auth0 logout, clear the Auth0 SDK cache, clear any client-side cached profile data and query caches, and revoke picture object URLs. Each device signs in independently; no token/session sharing.
7. No request-log redaction middleware will be added unless tests prove a leak. Enforcement is test-driven against current Serilog request logging behavior (method, path without query string, status, elapsed; no headers) with IdentityModel default PII protections.
8. Add a minimal client profile panel in profile/auth scope only: display name, username, email, profile picture, and JPEG/PNG upload via `PUT /api/v1/users/{id}/profile-picture`, then refresh the picture.

## Proposed API contract

### Endpoint: get caller profile

- **Method/route:** `GET /api/v1/users/me`
- **Auth policy:** `users.read`
- **Behavior:** Resolve caller by validated `sub` claim and return that profile.
- **Success:** `200 OK` + existing `UserResponse` shape.
- **Errors:** `401`, `403`, `404`.
- **Routing note:** No conflict with `GET /api/v1/users/{id:guid}` because `{id:guid}` will not match `me`.

### Endpoint: get private profile picture

- **Method/route:** `GET /api/v1/users/{id:guid}/profile-picture`
- **Auth policy:** `users.read`
- **Ownership:** Owner-only (`404` if caller does not own target profile).
- **Success:** `200 OK` streamed file body.
- **Response headers:**
  - `Content-Type` = stored `ProfilePictureContentType`
  - `Cache-Control: private, no-store`
  - `X-Content-Type-Options: nosniff`
- **Errors:** `401`, `403`, `404`.

### Security and leakage constraints

- No token accepted from query string.
- No token written to logs.
- No token in image URL/query/path on client.

## Layer-by-layer file plan with agent ownership

## Domain layer (`src/UserService.Domain`) — Agent: **Domain Developer**

### Files to review/change

- `src/UserService.Domain/Entities/UserProfile.cs`
  - Confirm no domain-model changes are required beyond #88 fields for retrieval use-cases.
- `src/UserService.Domain/Exceptions/*` (if needed)
  - Reuse existing not-found behavior; avoid adding new exception types unless required by API contract mapping.

### Domain tests

- Reuse existing unit coverage where possible; add only if retrieval/sign-out semantics require domain-level behavior changes.

## Data layer (`src/UserService.Data`) — Agent: **Data Access (UOW/Repositories/EF) Layer**

### Files to change

- `src/UserService.Data/Storage/IProfilePictureStorage.cs`
  - Add read API (stream/bytes + content metadata shape as finalized in implementation) for server-side retrieval.
- `src/UserService.Data/Storage/LocalProfilePictureStorage.cs`
  - Implement secure read path with the same server-generated key validation and root-bound path resolution guarantees.
- `src/UserService.Data/Repositories/IUserProfileRepository.cs`
  - Reuse existing `GetBySubjectAsync`; extend only if an owner-safe retrieval convenience method is needed.
- `src/UserService.Data/Repositories/UserProfileRepository.cs`
  - Minimal repository updates only if required by `/me` or picture read flow.

### Data tests

- `tests/UserService.UnitTests/Data/*`
  - Add tests for storage read key validation, traversal rejection, and root-bound enforcement.

## API layer (`src/UserService.Api`) — Agent: **API and Services Developer**

### Files to change

- `src/UserService.Api/Endpoints/UserEndpoints.cs`
  - Add `GET /api/v1/users/me` and `GET /api/v1/users/{id}/profile-picture` endpoint mappings and response metadata.
- `src/UserService.Api/Services/UserProfileService.cs`
  - Add service methods for caller-profile lookup (`sub`-based) and owner-safe profile picture retrieval.
- `src/UserService.Api/Infrastructure/ApiExceptionHandler.cs`
  - Ensure consistent `404` mapping for non-owner/no-picture and no token leakage in error details.

### API/integration tests

- `tests/UserService.IntegrationTests/AuthenticationTests.cs`
  - Extend token-in-query and scope/owner coverage for new routes, including `GET /api/v1/users/me`.
- `tests/UserService.IntegrationTests/ProfilePictureEndpointsTests.cs`
  - Add retrieval endpoint contract tests (`200/404`, owner isolation, headers, streamed bytes/content type).
- `tests/UserService.IntegrationTests/UserEndpointsTests.cs` (existing)
  - Add `/me` routing/contract tests, including assertion that `/api/v1/users/me` is not treated as `{id:guid}`.
- `tests/UserService.UnitTests/Api/UserProfileServiceTests.cs`
  - Add service-level tests for `/me`, owner checks, and no-picture behavior.
- `tests/UserService.IntegrationTests/*` (new/updated test fixture helpers)
  - Add log-capture assertions proving raw bearer token values do not appear in logs for: (a) successful authenticated request, (b) invalid/expired token request, (c) `?access_token=` query-string request.

## Client layer (`src/ChopChop.Client`) — Agent: **Client Developer**

### Files to change (profile/auth-related only)

- `src/ChopChop.Client/src/App.tsx`
  - Add profile panel mounting and profile bootstrap flow using `GET /api/v1/users/me`.
  - Add sign-out flow additions: clear local auth/profile state, clear query/data caches (if any), revoke current object URL, then trigger Auth0 logout.
- `src/ChopChop.Client/src/profile/ProfilePanel.tsx` (new)
  - Minimal profile panel UI: display name, username, email, picture, and JPEG/PNG file input for upload.
  - Show a "No profile yet" state when `/users/me` returns `404` and skip picture request in that state.
- `src/ChopChop.Client/src/profile/profileApi.ts` (new)
  - Add authenticated calls for `GET /api/v1/users/me`, `GET /api/v1/users/{id}/profile-picture`, and `PUT /api/v1/users/{id}/profile-picture`.
  - Return binary picture content for object-URL rendering and never place tokens in URL/query/path.
- `src/ChopChop.Client/.env.example`
  - Add `VITE_USER_SERVICE_API_URL` (and user-service audience variable if required by existing audience-per-API token strategy), following the current `VITE_*_API_URL` pattern.

### Client tests

- `src/ChopChop.Client/src/*.test.ts(x)` (new/updated)
  - Verify profile retrieval on authenticated load.
  - Verify `/users/me` `404` renders "No profile yet" and does not request profile picture.
  - Verify profile picture upload uses `PUT /api/v1/users/{id}/profile-picture` and refreshes picture after success.
  - Verify profile picture object URL lifecycle (replace/unmount/sign-out revokes URLs).
  - Verify sign-out clears local auth/profile/query state.
  - Verify client requests never place tokens in URLs.

## Documentation (`docs/*`) — Agent: **default Agent mode with the r-piv skill**

### Files to change/add

- `docs/milestone-4-profile-retrieval-plan.md` (this file)
- `docs/milestone-4-profile-retrieval-evidence.md` (to be created during Validate)
- `docs/user-service.md` (endpoint/auth updates)
- `docs/local-development.md` or `docs/manual-local-development.md` (document UserService client env configuration)
- `docs/milestones.md` (milestone tracking updates if required by repo convention)

## Ordered implementation checklist

1. **Add API contract support for caller-profile retrieval** — Agent: API and Services Developer
   - Implement `GET /api/v1/users/me` through existing subject-based repository lookup.
   - Add integration test proving `/api/v1/users/me` is not bound to `{id:guid}`.
2. **Add secure profile-picture read capability in storage layer** — Agent: Data Access (UOW/Repositories/EF) Layer
   - Extend `IProfilePictureStorage` and `LocalProfilePictureStorage` with validated read support.
3. **Add private profile-picture retrieval endpoint** — Agent: API and Services Developer
   - Implement owner-only `GET /api/v1/users/{id}/profile-picture` with required headers and `404` behavior.
4. **Add token-leakage verification tests without middleware changes** — Agent: Testing Developer
   - Capture logs for (a) successful authenticated request, (b) invalid/expired token request, (c) `?access_token=` request, and assert raw token absence.
5. **Implement minimal client profile panel and API module** — Agent: Client Developer
   - Add `ProfilePanel` and `profileApi` for `/users/me`, picture retrieval, and upload+refresh flow.
   - Handle `/users/me` `404` as a "No profile yet" UI state without requesting a picture.
6. **Harden sign-out behavior in client** — Agent: Client Developer
   - Clear SDK cache/local profile state/query caches, revoke object URLs, and then complete Auth0 logout.
7. **Add UserService client env configuration and docs** — Agent: default Agent mode with the r-piv skill
   - Update `.env.example` and local-development docs with `VITE_USER_SERVICE_API_URL` (+ audience variable if needed).
8. **Update docs and collect evidence** — Agent: default Agent mode with the r-piv skill
   - Update service/milestone docs and prepare #94 evidence file structure.
9. **Run validation suite and record outputs** — Agent: default Agent mode with the r-piv skill
   - Execute required .NET and client commands; document exact results and any limitations.

## Acceptance-criteria-to-test mapping (#94)

| Acceptance criterion | Planned tests (minimum) |
|---|---|
| New device can resolve profile without stored ID | Integration: `GET /api/v1/users/me` with valid `users.read` token returns owned profile; missing profile returns `404` |
| `/me` route does not conflict with `{id:guid}` route | Integration: explicit request to `/api/v1/users/me` resolves the `/me` handler and is not treated as `id` |
| Private picture retrieval endpoint exists and is owner-only | Integration: owner gets `200`; non-owner gets `404`; no-picture gets `404` |
| Picture response security headers are correct | Integration: assert `Cache-Control: private, no-store` and `X-Content-Type-Options: nosniff` |
| Stored content type is returned and file is streamed | Integration: uploaded JPEG/PNG retrieved with matching content type and non-empty body |
| Storage read rejects unsafe keys/paths | Data unit tests for invalid key format, traversal patterns, and outside-root resolution |
| Tokens rejected from query string | Integration tests for `access_token` query on new and existing protected routes -> `401` |
| Tokens never written to logs | Log-capture tests assert raw bearer token value is absent for: successful authenticated request, invalid/expired token request, and `?access_token=` query-string request |
| Tokens never appear in URLs/client data flow | Client tests verify authenticated header usage and absence of token query/path interpolation |
| No-profile client behavior is non-error | Client tests verify `/users/me` `404` renders "No profile yet" and no picture request is issued |
| Profile panel supports upload and refresh | Client tests verify JPEG/PNG upload through `PUT /api/v1/users/{id}/profile-picture` and picture refresh afterward |
| Object URLs lifecycle is safe | Client tests verify `URL.revokeObjectURL` on replace, component unmount, and sign-out |
| Sign-out clears local auth/profile/query state | Client tests verify state/cache cleanup and logout call ordering |
| Cross-device independent sign-in behavior preserved | Client/API integration behavior validation: each device uses its own auth flow; no token/session sharing assumptions |

## Validation commands to run during Validate phase

From repository root:

```bash
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

From `src/ChopChop.Client`:

```bash
npm ci
npm run build
npm test
npm run lint
```

Stop all running APIs before executing build/test commands.

If SQL/environment dependencies block integration tests, capture exact failing command and output in evidence, and do not mark blocked checks as passed.

## Evidence requirements

Create and maintain `docs/milestone-4-profile-retrieval-evidence.md` with:

1. Scope statement (UserService + profile/auth-related client only; no Keycloak changes).
2. File list grouped by Domain/Data/API/Client/Tests/Docs.
3. Validation command outputs (exact success/failure snippets for .NET and client commands).
4. Acceptance-criteria checklist with links to concrete test names.
5. Token-handling verification evidence:
   - Query-string token rejection evidence
   - Log-capture evidence showing raw token absence for success, invalid/expired, and query-string token attempts
   - Client evidence that tokens are not embedded in URLs
6. Client profile panel evidence:
   - `/users/me` `404` -> "No profile yet" state
   - upload + picture refresh behavior
7. Object URL lifecycle and sign-out cleanup evidence.
8. UserService client env configuration documentation evidence (`.env.example` + local-development docs).
9. Known limitations and any blocked validations.

## Known limitations

- This issue remains scoped to UserService and profile/auth-related client changes only.
- No Keycloak realm/config changes are included.
- Existing broader client architecture (outside profile/auth scope) is unchanged.
- Auth0 SDK browser token caching in `localstorage` remains an accepted risk because app-wide Auth0 configuration is intentionally unchanged; mitigation is explicit sign-out calling Auth0 logout to clear SDK cache and local client auth/profile state.

## Open decisions

None.
