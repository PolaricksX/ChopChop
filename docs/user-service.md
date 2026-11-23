# User Service

Backend for user profiles (issue #16) and profile picture upload (issue #88). .NET 10, EF Core + SQL Server, JWT bearer auth.

## Projects
- `src/UserService.Domain` – `UserProfile` entity, validation rules (including `ProfilePictureRules`), domain exceptions.
- `src/UserService.Data` – `DbContext`, configurations, migrations, Unit of Work, repository factory/provider, profile picture storage (`IProfilePictureStorage`, local filesystem) and image integrity checking (`IImageIntegrityChecker`, SkiaSharp).
- `src/UserService.Api` – minimal API endpoints, auth, middleware, OpenAPI.
- `tests/UserService.UnitTests`, `tests/UserService.IntegrationTests` (real SQL Server via LocalDB, or `TEST_SQLSERVER_CONNECTION`).

## Endpoints (`/api/v1/users`)
| Method | Path | Scope | Success | Errors |
|---|---|---|---|---|
| POST | `/` | `users.write` | 201 + `Location` | 400, 401, 403, 409, 429 |
| GET | `/{id}` | `users.read` | 200 | 401, 403, 404, 429 |
| PUT | `/{id}` | `users.write` | 200 | 400, 401, 403, 404, 409, 429 |
| DELETE | `/{id}` | `users.write` | 204 (soft delete) | 401, 403, 404, 429 |
| PUT | `/{id}/profile-picture` | `users.write` | 204 | 400, 401, 403, 404, 409, 413, 415, 429 |

`GET /health` is anonymous. OpenAPI is served at `/openapi/v1.json` in Development only.
Errors are RFC 9457 problem details including `correlationId` (`X-Correlation-Id` header).

## Behaviour
- A profile belongs to the token `sub`. `POST` creates the caller's own profile; a second one gives 409.
- Every operation requires its scope and a usable `sub` claim (non-blank, at most 256 characters); a token with the right scope but no usable subject gets 403. The owner is never read from the request body, route, or headers.
- Access is owner-only; other users' profiles return 404.
- `POST` requires an `Idempotency-Key` header. Same key and payload replays the original result; same key with a different payload gives 409.
- `PUT` requires the `version` from the last response; a stale version gives 409.
- Email and username are unique (case-insensitive) among non-deleted profiles. Deleting frees them.

## Profile pictures
`PUT /api/v1/users/{id}/profile-picture` sets or replaces the caller's own profile picture.

**Request:** `multipart/form-data` with exactly one file part named `file`. No `Idempotency-Key` is required. Antiforgery is disabled for this endpoint because the API uses bearer tokens, not cookie authentication.

**Validation** (checked in this order, before anything is stored):
| Rule | Response |
|---|---|
| No file part, or more than one file | 400 |
| Empty file | 400 |
| File larger than 1 MB | 413 |
| Format other than JPEG or PNG (detected from the file signature, not the extension) | 415 |
| Declared `Content-Type` does not match the detected format | 415 |
| Image cannot be decoded (corrupt content) | 400 |

The endpoint accepts requests up to 2 MB so that multipart overhead does not hide the 1 MB file rule; the global request limit for all other endpoints stays at 1 MB. Requests over 2 MB are rejected by the server before the service validates them.

**Storage and consistency:**
- Files are written to `ProfilePictures:RootPath` under server-generated GUID keys; the client file name is never used.
- The profile stores only the key and content type (`ProfilePictureKey`, `ProfilePictureContentType`).
- The new file is written first, then the profile reference is saved. If the save fails, the new file is deleted and the previous picture is kept.
- The previous file is deleted only after the save succeeds. If that delete fails, a warning is logged and the request still returns 204.
- A rejected upload leaves the existing picture unchanged.

## Configuration
`ConnectionStrings:ChopChop`, `Auth:Authority`, `Auth:Audience`, `Cors` allowlist, `RateLimiting:PermitLimit` (default 100/min) and `ProfilePictures:RootPath`. Services use this shared SQL Server database while owning separate tables. No secrets are committed; supply the connection string via `ConnectionStrings__ChopChop`.

`ProfilePictures:RootPath` must be an absolute path outside the web root and the application folder. It is empty in `appsettings.json` and a placeholder in `appsettings.Development.json`; set the real path with user secrets (see [local-development.md](local-development.md)).

## Running
```
dotnet ef database update --project src/UserService.Data --startup-project src/UserService.Api
dotnet run --project src/UserService.Api
dotnet test
```
Migrations are not applied automatically at startup.

## Limitations
- Soft delete keeps personal data; a purge/retention job is not implemented.
- No admin role or list endpoint, so no pagination.
- No `UserDeleted` event yet (planned for a later milestone).
- No profile picture retrieval endpoint or client UI yet (separate issue).
- No upload-specific rate limit beyond the service-wide limiter.
- Uploaded pictures are not scanned for malware (no scanner available).
- Soft-deleting a profile does not delete its stored picture file.
