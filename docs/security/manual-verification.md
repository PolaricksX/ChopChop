# Security Manual Verification

## Purpose

This checklist contains security tests that require a configured identity
provider, multiple accounts, cloud or browser state, or human inspection. It is
not a substitute for automated tests. Security changes should still have
automated unit and integration coverage.

Run this checklist on staging before a release. For local-only checks, use the
non-production URL and credentials described below. Never run development
authentication or permissive test credentials on a shared or production host.

## Non-automated scope

The following checks are outside the normal unit/integration/E2E suite:

- Permission boundaries across different real accounts and provider roles.
- Identity-provider brute-force, MFA, refresh, logout, and revocation behavior.
- Cross-device and stale-session behavior.
- Browser redirect, callback, cookie, token, and storage inspection.
- Cloud storage policy and bucket/container IAM inspection.
- Polyglot, malformed, oversized, or content-sniffing image samples.
- Multi-account stale-version and idempotency behavior.
- Manual log and response review for PII or secret leakage.

Automated suites cover deterministic JWT parsing and validation, missing,
expired, malformed, wrong issuer/audience/signature, insufficient-scope,
public-route, profile owner, validator, and IDOR behavior. Manual testing
adds real provider configuration and operational state.

## Prerequisites

- Staging environment using real JWT/OIDC configuration, or the complete local
  launcher in [the local development guide](../local-development.md).
- Separate test accounts:
  - Account A: normal user, `users.read` and `users.write`.
  - Account B: normal user, the same normal-user scopes.
  - Reviewer moderator, if moderation endpoints are in scope.
  - Invalid or no-scope token created without destructive account access.
- At least two browsers or browser profiles representing two devices/sessions.
- Identity-provider administrator access for provider policy and session
  inspection. Do not use production administrator credentials.
- Valid and invalid image samples, including oversized, wrong-extension,
  malformed, polyglot, and active-format files. Generate them locally or use
  test-only fixtures; never use real user media.
- Staging storage policy access for Azure Blob or the selected provider.
- Browser developer tools and an HTTP client such as PowerShell or Postman.
- A record of the staging API base URL, issuer, audience, and public callback
  origin. Never record passwords or bearer tokens in this document or Git.

Stop the test and report a finding if a test reveals a real secret, a
production credential, or a public storage leak.

## Recording template

For every test, record:

| Field | Value |
|---|---|
| Date/time | |
| Tester | |
| Environment/build | |
| Account/role | Do not enter password or full token |
| Result | Pass / Fail / N/A |
| Evidence | Screenshot, sanitized response, or log correlation ID |
| Finding | Ticket/link if failed |

Use the expected status and security boundary below, not merely a happy-path
response.

## Authentication

### AUTH-01 - Reject missing and malformed tokens

**Preconditions:** Account A is provisioned; the target API is running.

**Steps:**
1. Call a protected endpoint with no `Authorization` header.
2. Repeat with `Bearer not-a-jwt`, an expired access token, and a token for
   another issuer or audience.

**Expected result:** Each request is rejected with 401. No profile or private
resource content appears. The failure response contains problem details and a
correlation ID, with no token or stack trace.

### AUTH-02 - Enforce read and write scopes

**Preconditions:** Three valid access tokens are available: Account A with
read-only scope, Account A with write-only scope, and Account A with both.

**Steps:**
1. Call a read operation with read, write-only, and valid both-scope tokens.
2. Repeat for a write operation.
3. Repeat with no usable `sub` claim if the provider permits a test token.

**Expected result:** The operation succeeds only with the required scope.
Missing or wrong scope returns 403. A missing, blank, or overlong `sub` returns
403 rather than 500 or successful owner binding.

### AUTH-03 - Reject replayed or stale credentials

**Preconditions:** Capture a valid access token and the time at which it expires.

**Steps:**
1. Use the token successfully before expiry.
2. Repeat after expiry.
3. If provider tooling allows, revoke the token/session and retry once.

**Expected result:** Use succeeds before expiry and fails with 401 after expiry
or provider revocation. A revoked refresh token cannot silently produce a new
valid session.

### AUTH-04 - Identity-provider login protections

**Preconditions:** Provider lockout/MFA/rate-limit configuration is visible to
an authorized tester.

**Steps:**
1. Inspect provider MFA and lockout/risk policy.
2. Submit several invalid passwords for a test account using the provider login
   page.
3. Verify generic login failure behavior and inspect provider/audit logs.

**Expected result:** The provider does not leak whether an account exists,
applies configured throttling or lockout, does not bypass MFA, and exposes no
password or token in application logs. Access to test-account recovery actions
is limited to authorized operators.

### AUTH-05 - Development authentication is not deployable

**Preconditions:** A non-production deployment can be started with a controlled
configuration.

**Steps:**
1. Set `Auth:DevelopmentMode=true` while `ASPNETCORE_ENVIRONMENT=Development`
   and confirm it starts.
2. Restart in `Staging` or `Production` with the same setting.
3. Confirm normal browser startup ignores `VITE_DEV_AUTH=true` in a production
   build.

**Expected result:** Development mode starts only in Development and supplies
only the fixed local test identity. Startup fails outside Development. A
production build ignores the Vite development-auth flag.

## Authorization

### AUTHZ-01 - Object-level ownership

**Preconditions:** Both Account A and Account B have profiles. Account A knows
Account B's profile ID.

**Steps:**
1. Account A reads, updates, and deletes Account B's profile ID.
2. Repeat with Account B as the caller.
3. Replace route ID, body ID, or owner field with a different account value.

**Expected result:** Account A receives 404 for another user's private profile
and cannot change or delete it. Account B can operate only on its own profile.
No private content or existence confirmation leaks in 404/response differences.

### AUTHZ-02 - Function-level policy enforcement

**Preconditions:** Valid tokens with no scope, a read-only scope, and a
write scope.

**Steps:**
1. Call health and explicitly public endpoints without a token.
2. Call protected profile read/write endpoints with each token.
3. Call a newly protected endpoint not exposed in UI navigation.

**Expected result:** Public routes remain public. Every protected route returns
401 for no authentication and 403 for insufficient scope. Hiding a button or
navigation item does not grant access.

### AUTHZ-03 - Privilege escalation and role boundary

**Preconditions:** Normal user and moderator accounts have distinct provider
permissions.

**Steps:**
1. Call normal-user operations with a moderator token.
2. Call moderator-only operations with a normal-user token.
3. Inspect provider permission claims and remove the moderator permission, then
   retry.

**Expected result:** Least privilege is maintained. Moderator-only operations
return 403 for normal users. The API does not infer admin status from a browser
role or unauthenticated request field.

### AUTHZ-04 - Mass assignment

**Preconditions:** Account A owns a profile and has valid update permission.

**Steps:** Submit update requests containing `id`, `ownerId`, `sub`, `version`,
and role or deleted fields that are not part of the documented update DTO.

**Expected result:** Unknown fields are rejected or ignored according to API
contract. Owned identity and role fields cannot be changed by request payload.
The successful response still reflects only server-derived identity and audited
data.

## Profile data

### PROFILE-01 - Profile read boundary

**Preconditions:** Account A and Account B are created.

**Steps:** Account A calls `GET /api/v1/users/{accountB-profile-id}` and then
calls it with an unknown GUID.

**Expected result:** Both cases return 404. Neither response distinguishes
another user's profile from an unknown profile or returns private fields.

### PROFILE-02 - Profile update boundary

**Preconditions:** Account A and Account B profiles have valid versions.

**Steps:** Account A submits an update for Account B's ID, then Account A submits
its own valid update.

**Expected result:** Account A receives 404 for Account B. The own-profile
update succeeds and includes the new server-returned version.

### PROFILE-03 - PII and secret redaction

**Preconditions:** Capture the application log output for failed and successful
authentication/profile operations.

**Steps:**
1. Attempt login/profile operations with an invalid password or token.
2. Create and update a profile using unique test PII.
3. Search logs, problem responses, browser network responses, and trace data.

**Expected result:** Passwords, bearer tokens, API keys, and full sensitive
profile fields are absent. Correlation IDs are present. Error responses contain
no stack trace or SQL.

### PROFILE-04 - Negative profile input

**Preconditions:** A valid write-scope token is available.

**Steps:** Submit create/update requests with:
- username shorter than 3 or longer than 30 characters;
- invalid characters or leading punctuation;
- invalid email or email over 254 characters;
- whitespace in email;
- display name over 100 characters or containing control characters;
- missing/null request body;
- an invalid idempotency key.

**Expected result:** Every invalid case returns 400 problem details or an
RFC 9457 validation response, identifying the relevant field without revealing
server internals. Valid normalization, uniqueness, and length behavior stays
consistent.

### PROFILE-05 - Idempotency and optimistic concurrency

**Preconditions:** Account A has no profile.

**Steps:**
1. Create the profile twice with the same idempotency key and same payload.
2. Retry with the same key and different payload.
3. Update once, then retry update with the old version.

**Expected result:** Same-key/same-payload create replays the original result;
same-key/different-payload create returns 409. A stale profile version update
returns 409. A valid new version succeeds once.

### PROFILE-06 - Delete and retained data

**Preconditions:** Account A has a profile.

**Steps:** Delete the profile, try to read/update it, and inspect database
retention behavior with an authorized operator.

**Expected result:** Delete returns 204 once and later private operations return
404 or an appropriate deleted/not-found boundary. The operator documents that
soft delete retains personal data until the future purge/retention feature is
implemented; no production purge claim is made.

## Image upload and storage

### IMAGE-01 - Upload boundary and deferred state

**Preconditions:** Interactive Review is running.

**Steps:** Attempt to create or update a review with an image field and call
any documented photo/upload route.

**Expected result:** The current implementation does not expose photo upload,
as specified by ADR 0006. An active route must not appear accidentally. If
uploads are enabled for a release, verify a documented upload endpoint and
continue with IMAGE-02 through IMAGE-06.

### IMAGE-02 - File type and malicious payload

**Preconditions:** Upload implementation is enabled and valid test samples exist.

**Steps:** Upload valid JPEG/PNG data, an executable renamed `.jpg`, mismatched
MIME/extension data, malformed image data, and a polyglot sample.

**Expected result:** Only allowlisted types with verified content are accepted.
No executable, malformed, polyglot, or mismatched file is stored or served.
Failures return a safe 400/415 response with no parser stack trace.

### IMAGE-03 - File size

**Preconditions:** Upload implementation is enabled.

**Steps:** Upload a file one byte above the documented limit, a multipart
payload with many files, and a compressed/decompression-bomb sample.

**Expected result:** Oversized or decompression-risk payloads are rejected
before storage; the service remains responsive and returns 413 or 400 according
to the API contract.

### IMAGE-04 - Storage key and traversal

**Preconditions:** Upload implementation is enabled and storage is accessible
to an authorized tester.

**Steps:** Submit a filename containing `..`, absolute path text, separators,
reserved names, or traversal sequences. Inspect the resulting storage key.

**Expected result:** The caller filename is not used as a path. A server
generates a safe random key, traversal values are rejected or encoded, and no
filesystem location is overwritten.

### IMAGE-05 - Storage authorization

**Preconditions:** Storage policy can be inspected and two accounts exist.

**Steps:** Attempt direct bucket/container access, list objects, overwrite
another object's key, and read another account's image using a URL or signed
link.

**Expected result:** Storage is private by default. Anonymous list/read/write
and cross-owner access fail. Required service credentials have only the
minimum write/read operations. Expiring URLs enforce expiry.

### IMAGE-06 - Cloud policy and malware controls

**Preconditions:** Storage policy/IAM and malware scanner configuration are
available.

**Steps:** Inspect public access, CORS, object encryption, service principal
permissions, lifecycle rules, malware scanner hook, and logging/alerts.

**Expected result:** Public access is denied. Only approved origins and service
identities are allowed. Data is encrypted, lifecycle rules are documented,
scanning or an explicit residual-risk exception exists, and denied activity is
auditable.

## Device and session lifecycle

### SESSION-01 - Refresh token rotation

**Preconditions:** One test account and provider/admin access; a browser session
is active.

**Steps:**
1. Complete authorization code with PKCE and obtain a refresh token.
2. Refresh once, capture the old and new refresh tokens, then refresh again
   using the old token.
3. Inspect provider reuse-detection behavior.

**Expected result:** Refresh succeeds once within provider policy and returns a
rotated token. Reuse of an old refresh token is denied or triggers configured
revocation.

### SESSION-02 - Current-device logout

**Preconditions:** Test account is signed in on Browser A and Browser B.

**Steps:** Sign out Browser A through the application. Try provider-authorized
API access from Browser A and Browser B.

**Expected result:** Browser A is signed out according to provider callback
behavior. Browser B remains signed in unless provider policy defines otherwise.
No expired access token is silently accepted after revocation where provider
revocation is immediate.

### SESSION-03 - Revoke all devices

**Preconditions:** Account is signed in on two devices/browsers.

**Steps:** Revoke all sessions or change password through the provider or
approved account control. Retry API access from both sessions.

**Expected result:** Existing refresh and provider sessions are revoked according
to provider policy. Access tokens expire normally and cannot be renewed with a
revoked refresh token.

### SESSION-04 - Stale or compromised session

**Preconditions:** Provider session list includes at least two test sessions.

**Steps:** Identify or mark one stale test session, revoke it, and retry access
from that device. Optionally inspect suspicious-login/audit data.

**Expected result:** The revoked session cannot refresh or establish a new
session. The provider audit record identifies the revoke event without
exposing credentials.

### SESSION-05 - Session fixation and account switching

**Preconditions:** Browser A is signed into Account A; Browser B is clean.

**Steps:**
1. Open sign-in in Browser B and sign in as Account B.
2. Reload protected views and verify the displayed/returned profile owner.
3. Clear browser session and complete sign-in with Account A again.

**Expected result:** The token subject determines the API owner. Sign-out clears
local auth state and account switching does not reuse Account A's profile,
token, or cache.

### SESSION-06 - Expired access token during use

**Preconditions:** Client and provider can produce a short-lived test access
token.

**Steps:** Start a protected request near expiry, then wait and retry using the
expired token. If refresh is available, retry after refresh.

**Expected result:** The expired token returns 401. The client refreshes
according to policy or restarts sign-in. The client never exposes the refresh
token to application JavaScript or third-party URLs.

## Completion

Map each failed test to the threat row in
[threat-model.md](./threat-model.md), identify the owner and remediation date,
and add the evidence to the release record. Re-run affected tests after fix
deployment.
