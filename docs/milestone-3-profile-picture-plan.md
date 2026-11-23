# Milestone 3 Implementation Plan: User and Profile Profile Picture Upload (#88)

## Status

**Approved at Gate 1 by Léa-Maude Montpetit on 2026-11-22.**

This plan is intentionally limited to the User and Profile service scope for
issue #88:

- `src/UserService.*`
- `tests/UserService.*`
- `docs/*` (User service and milestone evidence docs)

No client (`src/ChopChop.Client`) and no Keycloak realm changes
(`infra/keycloak/*`) are included in this plan.

## Research baseline carried into this plan

- User/Profile service already exists as:
  - `src/UserService.Domain`
  - `src/UserService.Data`
  - `src/UserService.Api`
  - `tests/UserService.UnitTests`
  - `tests/UserService.IntegrationTests`
- Existing User endpoints are `/api/v1/users` create/get/update/delete with
  `users.read` / `users.write` policy enforcement.
- `UserProfile` is persisted in SQL Server (`Users` table) and currently has no
  profile picture key/content-type columns.
- Repository currently has no implemented reusable file-storage abstraction.

## Approved decisions (fixed inputs)

These decisions are treated as fixed and drive all implementation steps:

1. Storage: local filesystem behind `IProfilePictureStorage` in
   `UserService.Data`; root path from `ProfilePictures:RootPath`, outside web
   root/executable paths.
2. Persistence reference: nullable `ProfilePictureKey` and
   `ProfilePictureContentType` on `UserProfile`, with descriptive migration.
3. Route: `PUT /api/v1/users/{id}/profile-picture`, multipart/form-data, one
   file, `users.write`, owner from `ICurrentUser`, `404` for non-owner, `204`
   on success, no `Idempotency-Key`. Antiforgery disabled for this bearer-token
   API endpoint with explicit rationale in code/docs.
4. Allowed formats: JPEG and PNG only.
5. Size: max 1 MB; empty file rejected.
6. Content type: detect from signature and reject if declared type is disallowed
   or mismatched vs detected format.
7. Integrity: decode with SkiaSharp (latest stable, MIT license, centrally
   managed package version). SixLabors.SkiaSharp was rejected during
   implementation: 3.1.x has unpatched high-severity advisories
   (NuGet audit NU1903) and 4.x requires a Six Labors license key at build time.
8. Atomicity: full validation before write; write new file then persist DB
   reference; delete new file if DB save fails; delete old file only after
   successful save.
9. Status codes: 415 (format/mismatch), 400 (corrupt/empty), 413 (oversize),
   401 (no token), 403 (missing scope), 404 (not owner), 409 (concurrency).
10. Out of scope: retrieval endpoint/client UI, upload-specific rate limit,
    virus scanning (scanner unavailable).

## Proposed API contract (User service only)

### Endpoint

- **Method/route:** `PUT /api/v1/users/{id:guid}/profile-picture`
- **Auth policy:** `users.write`
- **Consumes:** `multipart/form-data` with exactly one file part
- **Success:** `204 No Content`
- **Errors:** `400`, `401`, `403`, `404`, `409`, `413`, `415` (problem details +
  correlation ID)

### Security/ownership behavior

- Caller identity only from validated token `sub` through `ICurrentUser`.
- Owner isolation preserved: non-owner profile IDs remain `404`.
- Antiforgery explicitly disabled for this endpoint because it is a
  bearer-token API endpoint (no cookie-based auth flow).

### Upload validation behavior

- API orchestration calls a pure Domain rules service (planned
  `ProfilePictureRules`) for file-validation rules; it does not duplicate these
  checks in API code.
- Domain rules cover empty file, max 1 MB, allowed formats (JPEG/PNG),
  signature detection (magic bytes), and declared content type matching
  detected format.
- Corrupt/undecodable image content is rejected (`400`) through the integrity
  checker boundary.
- `413` is returned by service-owned validation: the profile-picture endpoint
  gets an endpoint-specific request size limit above 1 MB (to allow multipart
  overhead) and matching multipart body length limit, while the file-size rule
  still enforces exactly 1 MB maximum.

## Layered file-by-file implementation plan

## Domain layer (`src/UserService.Domain`) — Agent: **Domain Developer**

### Files to change

- `src/UserService.Domain/Entities/UserProfile.cs`
  - Add nullable `ProfilePictureKey` and `ProfilePictureContentType`.
  - Add domain methods to set/replace profile picture reference while
    preserving aggregate invariants and updating timestamps.
- `src/UserService.Domain/Services/UserProfileRules.cs`
  - Add constants/rules for allowed profile picture content types if rule reuse
    is needed by API/data orchestration.

### Files to add

- `src/UserService.Domain/Services/ProfilePictureRules.cs`
  - Pure domain validation service for issue #88 upload rules: empty file,
    max size (1 MB), allowed formats (JPEG/PNG), signature detection from magic
    bytes, and declared content type matching detected format.

### Domain tests

- `tests/UserService.UnitTests/Domain/UserProfileTests.cs`
  - Add tests for picture reference transitions (add/replace semantics).
  - Assert unchanged profile state on rejected mutation paths invoked via
    service-level orchestration tests.
- `tests/UserService.UnitTests/Domain/ProfilePictureRulesTests.cs`
  - Add deterministic rule tests for empty file, max size, allowed formats,
    signature detection, and declared-vs-detected content type matching.

## Data layer (`src/UserService.Data`) — Agent: **Data Access (UOW/Repositories/EF) Layer**

### Files to add

- `src/UserService.Data/Storage/IProfilePictureStorage.cs`
  - Storage boundary abstraction (save/delete for profile picture objects).
- `src/UserService.Data/Storage/LocalProfilePictureStorage.cs`
  - Local filesystem implementation using server-generated GUID keys and safe
    extension mapping.
- `src/UserService.Data/Storage/ProfilePictureStorageOptions.cs`
  - Bind/validate `ProfilePictures:RootPath` and constraints (non-empty,
    absolute path policy as implemented).
- `src/UserService.Data/Images/IImageIntegrityChecker.cs`
  - Integrity-check boundary used by API orchestration.
- `src/UserService.Data/Images/SkiaSharpImageIntegrityChecker.cs`
  - SkiaSharp-backed implementation that decodes/loads image bytes to verify
    integrity without leaking SkiaSharp dependency into Domain.

### Files to change

- `src/UserService.Data/Configurations/UserProfileConfiguration.cs`
  - Map new nullable columns with explicit max lengths where appropriate.
- `src/UserService.Data/Migrations/*`
  - Add descriptive migration introducing picture columns.
- `src/UserService.Data/Context/UserServiceDbContext.cs`
  - Ensure model includes new properties through configuration.
- `src/UserService.Data/DataServiceCollectionExtensions.cs`
  - Register storage abstraction, integrity checker abstraction, and options
    binding/validation.

### Data tests

- `tests/UserService.UnitTests/Data/UnitOfWorkTests.cs`
  - Add persistence assertions for new properties.
- `tests/UserService.IntegrationTests/PersistenceTests.cs`
  - Add SQL persistence and optimistic concurrency behavior coverage for picture
    reference updates.

## API/Application layer (`src/UserService.Api`) — Agent: **API and Services Developer**

### Files to add

- `src/UserService.Api/Contracts/ProfilePictureContracts.cs`
  - Upload constants/DTO helpers if needed for endpoint validation and problem
    responses.

### Files to change

- `src/UserService.Api/Endpoints/UserEndpoints.cs`
  - Add `PUT /api/v1/users/{id}/profile-picture` mapping, policy, metadata,
    response types, and explicit antiforgery disable on this endpoint.
- `src/UserService.Api/Services/UserProfileService.cs`
  - Add upload orchestration method implementing approved atomic sequence:
    domain-rules validate -> integrity check (via data interface) -> write new
    file -> save profile reference -> compensating delete on DB failure ->
    delete old file after success.
  - If deleting the old file fails after a successful save, log a warning and
    still return 204 (the new picture is already committed).
- `src/UserService.Api/Program.cs`
  - Keep global 1 MB request limit unchanged.
  - Add endpoint-specific request size limit above 1 MB (enough for multipart
    overhead) and matching multipart body length limit so the service can
    evaluate the 1 MB file rule and return service-owned `413` problem details.
  - Register any needed upload validation services/dependencies.
- `src/UserService.Api/Infrastructure/ApiExceptionHandler.cs`
  - Map upload-specific domain/application exceptions to required status codes
    (`400`, `413`, `415`, `409`) with problem details + correlation.
- `src/UserService.Api/appsettings.json`
  - Add safe placeholder section for `ProfilePictures:RootPath` (no secrets).
- `src/UserService.Api/appsettings.Development.json`
  - Add local development root path placeholder outside web root/executable
    path.

## Testing layer (`tests/UserService.*`) — Agent: **Testing Developer**

### Unit tests to add/update

- `tests/UserService.UnitTests/Domain/ProfilePictureRulesTests.cs`
  - Empty file rejected.
  - >1 MB rejected.
  - JPEG/PNG magic-byte detection succeeds.
  - Unsupported signature rejected.
  - Declared content type mismatch rejected.
- `tests/UserService.UnitTests/Api/UserProfileServiceTests.cs`
  - Validation failure leaves profile + storage unchanged.
  - Service uses Domain rules outcomes instead of duplicating validation logic.
  - Corrupt/undecodable image rejected.
  - Integrity-check interface is faked in tests (Domain does not reference
    SkiaSharp).
  - Save failure triggers new-file rollback.
  - Old file deleted only after successful DB save.
  - Old-file delete failure after a successful save logs a warning and still succeeds.
- `tests/UserService.UnitTests/Api/AuthorizationTests.cs`
  - Scope helper coverage for new endpoint policy usage path (if needed).

### Integration tests to add/update

- `tests/UserService.IntegrationTests/AuthenticationTests.cs`
  - Extend route authorization matrix to include
    `PUT /api/v1/users/{id}/profile-picture`.
  - Auth cases for this endpoint: no token (`401`), malformed token (`401`),
    invalid signature (`401`), expired (`401`), wrong issuer (`401`), wrong
    audience (`401`), missing scope (`403`), wrong scope (`403`), valid scope
    + valid `sub` shape passes auth.
- `tests/UserService.IntegrationTests/UserEndpointsTests.cs`
  - Happy path upload returns `204` and updates persisted picture reference.
  - Non-owner route returns `404`.
  - Empty file -> `400`.
  - Corrupt image bytes -> `400`.
  - Disallowed media type -> `415`.
  - Declared JPEG with PNG bytes (and inverse) -> `415`.
  - File > 1 MB -> `413` with problem details payload.
  - Concurrency conflict path -> `409`.
  - Rejection cases keep prior profile picture unchanged.
- `tests/UserService.IntegrationTests/Support/ApiFactory.cs`
  - Configure isolated temp storage root per test run.
  - Ensure cleanup of storage root during test teardown.

## Documentation layer (`docs`) — Agent: **default Agent mode with the r-piv skill**

### Files to add

- `docs/milestone-3-profile-picture-evidence.md`
  - Implementation + validation evidence log for issue #88.

### Files to change

- `docs/user-service.md`
  - Document endpoint contract, validation rules, status codes, config key,
    storage behavior, and known limitations.
- `docs/local-development.md` (if needed)
  - Add UserService local profile-picture storage setup note.
- `docs/milestones.md` (if needed)
  - Keep links/status consistent for #88 plan/evidence artifacts.

## Ordered implementation checklist (with layer agent ownership)

1. **Dependency decision record and package baseline** — Agent: default Agent mode with the r-piv skill
   - Add SkiaSharp dependency plan note (latest stable, centrally managed).
   - Record required license review + vulnerability scan evidence targets.
2. **Domain model extension** — Agent: Domain Developer
   - Add profile picture reference fields and set/replace mutation methods to
     `UserProfile`.
   - Add `ProfilePictureRules` and domain rule tests.
3. **Data schema + storage abstraction** — Agent: Data Access (UOW/Repositories/EF) Layer
   - Add migration, EF mapping updates, and filesystem storage implementation
     behind `IProfilePictureStorage`.
   - Add `IImageIntegrityChecker` with SkiaSharp implementation and DI
     registration.
4. **API endpoint + orchestration** — Agent: API and Services Developer
   - Add upload endpoint, validation, exception mapping, antiforgery disable,
     and atomic write/save/delete sequence.
5. **Unit tests for validation and failure safety** — Agent: Testing Developer
   - Add deterministic unit tests for all rejection and rollback paths.
6. **Integration tests for API contract and auth matrix** — Agent: Testing Developer
   - Add multipart endpoint behavior tests and full auth cases.
7. **Documentation and evidence setup** — Agent: default Agent mode with the r-piv skill
   - Update user-service docs and create evidence doc template for #88.
8. **Validation execution and evidence capture** — Agent: default Agent mode with the r-piv skill
   - Run required checks and record exact outputs in evidence doc.

## Acceptance-criteria-to-test mapping (#88)

| Acceptance criterion | Planned tests (minimum) |
|---|---|
| Upload endpoint exists in User/Profile service | Integration: endpoint reachable with valid auth and multipart; returns `204` |
| Permission/authorization enforced | Integration auth matrix for new route: `401`/`403` cases plus valid-token success path |
| Allowed format validation (JPEG/PNG only) | Domain unit tests (`ProfilePictureRulesTests`) + integration: reject unsupported type (`415`), accept JPEG/PNG |
| Declared content-type must match signature | Domain unit tests + integration mismatch cases (`415`) |
| Image integrity validation | API unit tests with faked integrity checker + integration corrupt/undecodable bytes -> `400` |
| Size limit and empty-file rejection | Domain unit tests + integration empty -> `400`; >1 MB file-rule rejection -> `413` |
| Persistent storage of picture + profile reference | Integration: filesystem object written and DB fields persisted |
| Atomic save and rollback behavior | Unit tests for DB failure rollback and old-file deletion sequencing; integration spot-check for unchanged state on failure |
| Unchanged profile on rejection/failure | Integration: prior reference remains when validation/save fails |
| Concurrency behavior | Integration: stale/parallel update path -> `409` |
| Docs updated | `docs/user-service.md` + evidence doc include endpoint/rules/limitations/validation outputs |

## Required auth test cases for the new endpoint

Planned integration assertions for `PUT /api/v1/users/{id}/profile-picture`:

1. Missing token -> `401`
2. Malformed token -> `401`
3. Invalid signature -> `401`
4. Expired token -> `401`
5. Wrong issuer -> `401`
6. Wrong audience -> `401`
7. Missing scope claim -> `403`
8. Wrong scope (`users.read` instead of `users.write`) -> `403`
9. Missing/invalid `sub` claim shape -> `403`
10. Valid token + `users.write` + owner id -> route-level authorization passes
11. Valid token + `users.write` + non-owner id -> `404`

## Dependency, license, and security evidence plan

Because SkiaSharp is newly introduced for cross-platform image decoding:

- Record chosen version in central package management.
- Record license review result (package + version + acceptable license).
- Run vulnerability scan and record exact output in evidence doc.
- Keep no secrets in config values (`ProfilePictures:RootPath` only path config).

## Validation commands to run during Validate phase

From repository root:

```bash
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

Stop all running APIs before running the tests (they lock the build output).

If local SQL dependency prevents integration execution, record exact failing
command/output and do not report it as passed.

## Evidence document requirements

Create and maintain `docs/milestone-3-profile-picture-evidence.md` with:

1. Scope statement (UserService only, no client/Keycloak changes).
2. File list grouped by Domain/Data/API/Tests/Docs.
3. Validation command outputs (exact success/failure snippets).
4. Acceptance-criteria checklist with links to test names.
5. SkiaSharp dependency review notes (license + vulnerability scan result).
6. Known limitations (retrieval/UI separate issue, no upload-specific rate
   limiting, no virus scanner available).

## Known limitations (explicitly out of scope)

- Picture retrieval endpoint and client UI.
- Upload-specific rate limiting policy beyond current service-global limiter.
- Malware/virus scanning integration (scanner not available in current scope).

## Open decisions

None. This plan uses only the approved decisions listed above.

If any additional architectural choice becomes necessary during implementation,
it must be raised for approval before coding that part.
