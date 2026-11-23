# Milestone 4 Evidence: Cross-Device Profile Retrieval and Private Picture Access (#94)

Plan: [`milestone-4-profile-retrieval-plan.md`](milestone-4-profile-retrieval-plan.md)
Related prior baseline: [`milestone-4-profile-picture-evidence.md`](milestone-4-profile-picture-evidence.md)
Branch: `feature/94-profile-retrieval`
Validated: 2026-11-23

## 1. Scope

Changes are limited to UserService and profile/auth-related client behavior:

- `src/UserService.*`
- `tests/UserService.*`
- `src/ChopChop.Client` (profile/auth scope only)
- `docs/*`

No Keycloak realm/config updates are included (`infra/keycloak/*` unchanged).

## 2. Files changed (against `feature/88-profile-picture`)

Based on `git diff --stat feature/88-profile-picture...HEAD`.

### Data (`src/UserService.Data`)
- `src/UserService.Data/Storage/IProfilePictureStorage.cs`
- `src/UserService.Data/Storage/LocalProfilePictureStorage.cs`

### API (`src/UserService.Api`)
- `src/UserService.Api/Endpoints/UserEndpoints.cs`
- `src/UserService.Api/Infrastructure/SecurityHeadersMiddleware.cs`
- `src/UserService.Api/Program.cs`
- `src/UserService.Api/Services/UserProfileService.cs`
- `src/UserService.Api/UserService.Api.csproj`

### Client (`src/ChopChop.Client`)
- `src/ChopChop.Client/.env.example`
- `src/ChopChop.Client/src/App.tsx`
- `src/ChopChop.Client/src/auth.ts`
- `src/ChopChop.Client/src/profile/ProfilePanel.tsx`
- `src/ChopChop.Client/src/profile/profileApi.ts`
- `src/ChopChop.Client/src/redirectNavigation.ts`

### Tests (`tests/UserService.*` + client tests)
- `tests/UserService.IntegrationTests/AuthenticationTests.cs`
- `tests/UserService.IntegrationTests/ProfilePictureEndpointsTests.cs`
- `tests/UserService.IntegrationTests/Support/ApiFactory.cs`
- `tests/UserService.IntegrationTests/Support/CapturedLogSink.cs`
- `tests/UserService.IntegrationTests/Support/SerilogStaticLoggerCollection.cs`
- `tests/UserService.IntegrationTests/TokenLeakageTests.cs`
- `tests/UserService.IntegrationTests/UserEndpointsTests.cs`
- `tests/UserService.UnitTests/Api/UserProfileServiceTests.cs`
- `tests/UserService.UnitTests/Data/LocalProfilePictureStorageTests.cs`
- `tests/UserService.UnitTests/Fakes/Fakes.cs`
- `src/ChopChop.Client/src/App.test.tsx`
- `src/ChopChop.Client/src/profile/ProfilePanel.test.tsx`
- `src/ChopChop.Client/src/profile/profileApi.test.ts`

### Docs
- `docs/milestone-4-profile-retrieval-plan.md`
- `docs/milestone-4-profile-retrieval-evidence.md`
- `docs/user-service.md`
- `docs/manual-local-development.md`
- `docs/milestones.md`

## 3. Validation results

| Command / check | Result |
|---|---|
| `dotnet restore` | `Success` |
| `dotnet build -c Release --no-restore` | `Success` |
| `dotnet test -c Release --no-build` | `total 451, failed 0, succeeded 450, skipped 1` |
| `dotnet format --verify-no-changes` | `no changes required` |
| `dotnet list package --vulnerable --include-transitive` | `no vulnerable packages` |
| `npm ci` | `Success` |
| `npm run build` | `Success` |
| `npm test` | `Success` |
| `npm run lint` | `Success` |

## 4. Acceptance criteria → tests

| Acceptance criterion | Tests |
|---|---|
| `GET /api/v1/users/me` returns caller profile by token subject | `UserEndpointsTests.Get_me_returns_the_callers_profile`, `UserProfileServiceTests.GetCurrent_returns_own_profile` |
| `/me` returns 404 when caller has no profile | `UserEndpointsTests.Get_me_returns_404_when_the_caller_has_no_profile`, `UserProfileServiceTests.GetCurrent_without_a_profile_is_not_found`, `UserProfileServiceTests.GetCurrent_after_soft_delete_is_not_found` |
| `/me` route does not conflict with `{id:guid}` route | `UserEndpointsTests.Get_me_is_handled_by_the_me_endpoint_not_the_id_endpoint` |
| Private `GET /api/v1/users/{id}/profile-picture` is owner-only and hides missing resources as 404 | `ProfilePictureEndpointsTests.Another_user_getting_profile_picture_is_404`, `ProfilePictureEndpointsTests.Profile_without_a_picture_returns_404` |
| Picture retrieval returns stored media type and bytes | `ProfilePictureEndpointsTests.Get_after_uploading_png_returns_200_with_identical_bytes_and_png_content_type`, `ProfilePictureEndpointsTests.Get_after_uploading_jpeg_returns_200_with_identical_bytes_and_jpeg_content_type`, `ProfilePictureEndpointsTests.Get_after_replacing_picture_returns_the_new_bytes` |
| Picture retrieval security headers | `ProfilePictureEndpointsTests.Get_profile_picture_sets_private_no_store_and_nosniff_headers` |
| Default cache-control behavior on non-picture endpoints unchanged | `UserEndpointsTests.Get_me_keeps_the_default_cache_control_header` |
| Storage read validates keys and blocks traversal/outside-root paths | `LocalProfilePictureStorageTests.ReadAsync_returns_saved_bytes_for_valid_key`, `LocalProfilePictureStorageTests.ReadAsync_returns_null_for_missing_valid_key`, `LocalProfilePictureStorageTests.ReadAsync_rejects_invalid_or_unsafe_keys` |
| Query-string token rejection remains enforced | `AuthenticationTests.Token_in_query_string_is_not_accepted` |
| Raw bearer token values are not logged | `TokenLeakageTests.Successful_authenticated_profile_flow_does_not_log_raw_bearer_token`, `TokenLeakageTests.Invalid_signature_token_on_get_me_does_not_log_raw_bearer_token`, `TokenLeakageTests.Expired_token_on_get_me_does_not_log_raw_bearer_token`, `TokenLeakageTests.Query_string_access_token_on_get_me_does_not_log_raw_bearer_token`, `TokenLeakageTests.Authenticated_profile_picture_upload_and_get_do_not_log_raw_bearer_token` |
| Client keeps token out of URLs and uses Authorization header | `profileApi.test.ts` -> `loads the caller profile with users.read and bearer authorization`, `ProfilePanel.test.tsx` -> `never places the access token in any request URL` |
| Client profile panel supports no-profile state and guarded error handling | `ProfilePanel.test.tsx` -> `shows "No profile yet" on /users/me 404 and skips picture request`, `shows an error and no create form when /users/me fails with 500`, `reloads /users/me when "Try again" is clicked after a load error` |
| Sign-out cleanup revokes object URLs and clears loaded profile state | `ProfilePanel.test.tsx` -> `sign-out cleanup revokes picture URL and clears loaded profile from the panel`, `revokes the current object URL on unmount`; `App.test.tsx` -> `cleans up the loaded profile before calling Auth0 logout` |
| New routes enforce authentication, scope, and subject | `AuthenticationTests.Every_route_enforces_authentication_scope_direction_and_subject` (rows for `GET /api/v1/users/me` and `GET /api/v1/users/{id}/profile-picture`) |
| Client upload and create-profile flows | `ProfilePanel.test.tsx` -> `uploads multipart file, reloads picture, and revokes replaced object URL`, `creates a profile from the no-profile form and then displays the loaded profile`, `shows problem detail when profile creation returns conflict` |
| Independent sign-in across devices | Manual cross-device check (section 9) |

## 5. Token-handling evidence

- Query-string token input is rejected (`401`) for protected routes including `/users/me` and `/{id}/profile-picture` (`AuthenticationTests.Token_in_query_string_is_not_accepted`).
- Integration log-capture tests assert that neither raw token values nor `Bearer ` values appear in request logs for:
  - successful authenticated flow,
  - invalid-signature token,
  - expired token,
  - query-string token attempt,
  - authenticated picture upload and retrieval.
- Client tests assert tokens are sent in `Authorization` headers and are never appended to request URLs.

## 6. Client panel and sign-out evidence

- `/users/me` `404` renders a clear **No profile yet** state and skips picture retrieval.
- A minimal create-profile form is available in the no-profile state and reloads profile data after creation.
- Picture upload refreshes the displayed image and revokes the replaced object URL.
- Sign-out cleanup clears loaded profile data and revokes active object URLs before Auth0 logout.
- For non-404 `/users/me` failures, the panel shows an error and **Try again** action instead of the create-profile form.

## 7. Deviations recorded from plan

- `Program.cs`: `.ReadFrom.Services(services)` added to `UseSerilog` so tests can capture logs; no logging levels, templates, or sinks changed.
- `TokenLeakageTests` run in a non-parallel xUnit collection because Serilog request logging writes to static `Log.Logger`, which parallel test hosts replace.
- `SecurityHeadersMiddleware` now applies its default `Cache-Control` only when the response has none, so the picture endpoint keeps `private, no-store`; other endpoints are unchanged (covered by a test).
- A minimal create-profile form was added to the client’s **No profile yet** state so cross-device retrieval can be demonstrated from the client.
- Client: errors other than 404 from `/users/me` show an error with **Try again** instead of the create-profile form (found during manual check).
- `UserService.Api`: added `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`) because the documented `dotnet ef` migration command failed without it.
- The documented migration command did not target the API LocalDB database; docs now pass `--connection` explicitly.

## 8. Known limitations

- Auth0 SDK token caching in browser `localStorage` remains an accepted risk (Gate 1).
- No upload-specific rate limit beyond the service-wide limiter.
- Uploaded pictures are not scanned for malware.
- Soft-deleted profiles keep their stored picture file.

## 9. Manual cross-device check

On 2026-11-23, signed in with the same account in Chrome and Edge against a local UserService API and client; the profile and picture created in Chrome appeared in Edge without re-uploading; signing out in Chrome left Edge signed in; the picture request URL contained no token and the response had `Cache-Control: private, no-store`.
