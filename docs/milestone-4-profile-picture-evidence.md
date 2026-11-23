# Milestone 4 Evidence: User Profile Picture Upload (#88)

Plan: [`milestone-4-profile-picture-plan.md`](milestone-4-profile-picture-plan.md)
Branch: `feature/88-profile-picture`
Validated: 2026-11-23

## 1. Scope

Changes are limited to the User and Profile service (`src/UserService.*`,
`tests/UserService.*`) and documentation (`docs/*`).
No client (`src/ChopChop.Client`) or Keycloak realm (`infra/keycloak/*`) changes.

## 2. Files changed

### Domain (`src/UserService.Domain`)
- `Entities/UserProfile.cs`: nullable `ProfilePictureKey` and `ProfilePictureContentType`; `SetOrReplaceProfilePicture`
- `Services/ProfilePictureRules.cs`: empty file, 1 MB limit, JPEG/PNG signature detection, declared-vs-detected content type
- `Exceptions/*`: `ProfilePictureBadRequestException`, `ProfilePicturePayloadTooLargeException`, `ProfilePictureUnsupportedMediaTypeException`

### Data (`src/UserService.Data`)
- `Storage/IProfilePictureStorage.cs`, `Storage/LocalProfilePictureStorage.cs`, `Storage/ProfilePictureStorageOptions.cs`
- `Images/IImageIntegrityChecker.cs`, `Images/SkiaSharpImageIntegrityChecker.cs`
- `Configurations/UserProfileConfiguration.cs`: column mapping for the new properties
- `Migrations/20261123132251_AddUserProfilePicture.cs` (+ designer and model snapshot)
- `DataServiceCollectionExtensions.cs`: registers storage, integrity checker and options
- `UserService.Data.csproj`, `Directory.Packages.props`: SkiaSharp dependency (central package management)

### API (`src/UserService.Api`)
- `Endpoints/UserEndpoints.cs`: `PUT /api/v1/users/{id:guid}/profile-picture`
  (multipart, `users.write`, 2 MB endpoint request limit, antiforgery disabled for bearer-token API)
- `Services/UserProfileService.cs`: `ReplaceProfilePictureAsync`: validate, integrity check, write new file,
  save reference, delete new file if save fails, delete old file after success (warning logged if that delete fails)
- `Infrastructure/ApiExceptionHandler.cs`: maps picture exceptions to `400` / `413` / `415`
- `appsettings.json`, `appsettings.Development.json`: `ProfilePictures:RootPath` placeholders

### Tests (`tests/UserService.*`)
- `UnitTests/Domain/ProfilePictureRulesTests.cs`, `UnitTests/Domain/UserProfileTests.cs`
- `UnitTests/Api/UserProfileServiceTests.cs`, `UnitTests/Data/UnitOfWorkTests.cs`
- `UnitTests/Fakes/Fakes.cs`: `FakeProfilePictureStorage`, `FakeImageIntegrityChecker`, `FailingUnitOfWork`, `RecordingLogger<T>`
- `IntegrationTests/ProfilePictureEndpointsTests.cs` (new)
- `IntegrationTests/AuthenticationTests.cs`, `IntegrationTests/PersistenceTests.cs`
- `IntegrationTests/Support/ApiFactory.cs`: isolated temporary storage root per run, deleted on teardown

### Docs
- `docs/milestone-4-profile-picture-plan.md`, `docs/milestone-4-profile-picture-evidence.md` (this file)
- `docs/milestones.md`, `docs/user-service.md`, `docs/local-development.md`

## 3. Validation results

All commands run from the repository root with no API processes running.

| Command | Result |
|---|---|
| `dotnet restore` | Succeeded (1.8 s) |
| `dotnet build -c Release --no-restore` | Succeeded (6.6 s) |
| `dotnet test -c Release --no-build` | Total 391, **failed 0**, succeeded 390, skipped 1 (14.7 s) |
| `dotnet format --verify-no-changes` | No changes required (no output, exit code 0) |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages in any project |

**Skipped test:** `Equipment.IntegrationTests.SqlServerPersistenceTests.Migration_preserves_owner_isolation_rowversion_and_outbox_atomicity`.
It belongs to the Equipment service and is unrelated to #88. No UserService tests are skipped.

**Vulnerability scan (UserService projects):**

```
The given project `UserService.Api` has no vulnerable packages given the current sources.
The given project `UserService.Data` has no vulnerable packages given the current sources.
The given project `UserService.Domain` has no vulnerable packages given the current sources.
The given project `UserService.IntegrationTests` has no vulnerable packages given the current sources.
The given project `UserService.UnitTests` has no vulnerable packages given the current sources.
```

All other projects in the solution also report no vulnerable packages.

## 4. Acceptance criteria → tests

| Acceptance criterion | Tests |
|---|---|
| Upload endpoint exists | `ProfilePictureEndpointsTests.Upload_png_returns_204_and_persists_the_picture_reference`, `Upload_jpeg_returns_204` |
| Authorization enforced | `AuthenticationTests.Every_route_enforces_authentication_scope_direction_and_subject` (profile-picture row), `Profile_picture_route_rejects_bad_tokens_with_401_and_missing_scope_with_403`, `Profile_picture_route_with_users_write_passes_auth_and_hides_other_users_profiles` |
| Owner isolation (`404`) | `ProfilePictureEndpointsTests.Upload_to_another_users_profile_is_404_and_changes_nothing`, `UserProfileServiceTests.ReplacePicture_of_another_users_profile_is_not_found` |
| JPEG/PNG only (`415`) | `ProfilePictureRulesTests`, `ProfilePictureEndpointsTests.Unsupported_format_is_415`, `UserProfileServiceTests.ReplacePicture_with_unsupported_format_is_rejected` |
| Declared type must match signature (`415`) | `ProfilePictureRulesTests`, `ProfilePictureEndpointsTests.Declared_type_not_matching_the_file_is_415_in_both_directions`, `UserProfileServiceTests.ReplacePicture_with_content_type_not_matching_the_file_is_rejected` |
| Image integrity (`400`) | `ProfilePictureEndpointsTests.Corrupt_image_with_a_valid_signature_is_400`, `UserProfileServiceTests.ReplacePicture_with_corrupted_image_is_rejected_and_nothing_is_saved` |
| Empty file (`400`) / size limit (`413`) | `ProfilePictureRulesTests`, `ProfilePictureEndpointsTests.Empty_file_is_400`, `File_over_1_MB_is_413_with_problem_details`, `Request_without_a_file_is_400`, matching `UserProfileServiceTests` cases |
| Persistent storage + profile reference | `ProfilePictureEndpointsTests.Upload_png_returns_204_and_persists_the_picture_reference`, `Replacing_the_picture_stores_a_new_reference`, `PersistenceTests` |
| Atomic save and rollback | `UserProfileServiceTests.ReplacePicture_when_saving_fails_deletes_the_new_file_and_keeps_the_old_one`, `ReplacePicture_twice_deletes_the_old_picture`, `ReplacePicture_when_deleting_the_old_file_fails_still_succeeds_and_logs_a_warning` |
| Unchanged profile on rejection | `ProfilePictureEndpointsTests.Rejected_upload_keeps_the_previous_picture`, `UserProfileServiceTests.ReplacePicture_rejected_upload_keeps_the_existing_picture` |
| Concurrency (`409`) | `ProfilePictureEndpointsTests.Concurrent_uploads_return_204_or_409_and_never_500`, `PersistenceTests` |
| Docs updated | `docs/user-service.md`, this evidence document |

## 5. Dependency review: SkiaSharp

| Item | Result |
|---|---|
| Package | `SkiaSharp` |
| Version | 4.151.0 |
| License | MIT |
| Management | Central package management (`Directory.Packages.props`) |
| Vulnerability scan | No known vulnerabilities (see section 3) |
| Isolation | Referenced only by `UserService.Data`; the Domain project does not depend on SkiaSharp |
| Alternative rejected | `SixLabors.ImageSharp`: 3.1.x has unpatched high-severity advisories (NU1903); 4.x requires a Six Labors license key at build time |

## 6. Issue found and fixed during validation

The integration test `Corrupt_image_with_a_valid_signature_is_400` returned `500` instead of `400`
for a file with a valid PNG signature followed by undecodable bytes.
`SkiaSharpImageIntegrityChecker` was changed to decode through `SKCodec` with a
`null` check and to treat any decoding exception as an invalid image (returns `false`).
Both affected tests now pass.

## 7. Deviations from the plan

- The endpoint-specific request and multipart limits (2 MB) are set as metadata on the endpoint in
  `UserEndpoints.cs` instead of in `Program.cs`. The behaviour matches the plan: the global 1 MB limit is
  unchanged and the service-owned 1 MB file rule returns `413`.
- The profile-picture integration tests are in a new file, `ProfilePictureEndpointsTests.cs`, instead of
  `UserEndpointsTests.cs`, to keep the existing endpoint test file focused.

## 8. Known limitations (out of scope)

- No picture retrieval endpoint or client UI (separate issue).
- No upload-specific rate limit beyond the service-wide limiter.
- No malware or virus scanning (no scanner available).
- Requests larger than the 2 MB endpoint limit are rejected by the server before the service's
  1 MB file rule runs; files between 1 MB and 2 MB return the service-owned `413` problem details.
- Each developer must set `ProfilePictures:RootPath` locally (see `docs/local-development.md`).
