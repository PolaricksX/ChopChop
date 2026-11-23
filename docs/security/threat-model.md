# Security Threat Model

## Purpose

This document defines the security boundaries, threats, mitigations, residual
risk, and verification mapping for ChopChop. It complements
[the security operations guide](../milestone-4-security-operations.md) and
[the OIDC architecture decision](../adr/0003-identity-and-client-auth.md).

The manual test procedures for these threats are in
[manual-verification.md](./manual-verification.md).

## Scope and trust boundaries

| Asset or boundary | Security expectation |
|---|---|
| Browser React client | Public client; no secrets, password collection, or server-side authorization decisions. Uses authorization code flow with PKCE. |
| Identity provider | Validates credentials and MFA, issues signed access tokens, manages refresh/revocation, and grants least-privilege scopes or permissions. |
| ASP.NET Core APIs | Validate every access token independently as the resource server; enforce scopes and ownership; return RFC 9457 problem details. |
| User profile database | Store only profile attributes needed by ChopChop; owner identity comes from the validated token subject. |
| Shared SQL Server | Restrict service accounts and protect connection strings; enforce parameterized data access and service ownership boundaries. |
| Image storage | Not implemented in the current review slice. Future storage must keep content outside executable paths and enforce private, least-privilege access. |
| Logs and observability | Redact personal data and credentials; include correlation IDs without stack traces or tokens. |

Trust crosses the browser-to-provider, provider-to-API, API-to-database, and
browser-to-storage boundaries. Data received from every identity provider,
browser, file, and provider API is untrusted until validated.

## Authentication

| Threat | Mitigation | Residual risk | Manual verification |
|---|---|---|---|
| Invalid or forged bearer token | Framework JWT bearer validation requires signature, configured issuer and audience, and expiry; standard OIDC metadata and signing-key rotation are used. | Misconfigured authority/audience or accepted long-lived access tokens can weaken enforcement. | AUTH-01 through AUTH-03 |
| Missing scope or claim | Protected endpoints use named policies and require the usable `sub` subject plus the operation-specific scope. | Identity-provider role or permission mapping may differ if configuration is incomplete. | AUTH-02 |
| Brute force or credential stuffing | Authentication passwords remain at the identity provider. Configure lockout, MFA, rate limits, and suspicious-login detection in the provider. APIs apply rate limiting. | Provider configuration is outside ASP.NET Core and must be checked separately. | AUTH-04 |
| Replay, session fixation, or stale session | Authorization-code with PKCE, short-lived access tokens, provider refresh-token policy, and revocation are used; browser storage must not contain secrets. | A stolen valid access or refresh token remains usable until expiry/revocation. | AUTH-03, SESSION-01, SESSION-02 |
| Invalid credential or login failure leakage | The provider owns the login UI and generic failure behavior; APIs do not expose credentials or provider stacks in responses. | Provider-specific account-enumeration behavior depends on configuration. | AUTH-04 |
| Development authentication misuse | `Auth:DevelopmentMode=true` is accepted only in `Development` and fails startup elsewhere. The browser `VITE_DEV_AUTH=true` is development-only. | Local development authentication intentionally bypasses authorization and must never be shared or deployed. | AUTH-05 |

## Authorization

| Threat | Mitigation | Residual risk | Manual verification |
|---|---|---|---|
| Broken object-level access control (IDOR) | The owner subject comes from `ICurrentUser`; private queries filter by owner. A caller who changes a route ID for another user's profile receives 404. | Implementation drift could add an owner-less query or list endpoint. | AUTHZ-01 |
| Broken function-level access control | Endpoints have explicit policies; the fallback policy requires authentication; public routes are allowlisted intentionally. | Development authentication intentionally bypasses policies and is prohibited outside Development. | AUTHZ-02 |
| Privilege escalation | Separate read/write scopes and moderator-only permissions; least-privilege provider grants. The API, not the browser, makes authorization decisions. | Shared or administrator accounts can become overly privileged by misconfiguration. | AUTHZ-03 |
| Mass assignment | Request DTOs contain only accepted fields. Owner, ID, and audit values are not accepted from caller-controlled identity fields. | Future endpoints must retain the allowlist and ownership pattern. | AUTHZ-04 |
| Missing server-side enforcement | Authorization assertions are in API policies and ownership checks; client UI hiding is never a security control. | An unprotected or incorrectly registered endpoint could escape the fallback policy. | AUTHZ-02 |

## Profile data

| Threat | Mitigation | Residual risk | Manual verification |
|---|---|---|---|
| Unauthorized profile read or update | `users.read`/`users.write`, usable `sub`, and owner-only repository queries; route IDs other than the caller's profile return 404. | Authenticated list/export features do not currently exist; future features require individual owner/admin policies. | PROFILE-01, PROFILE-02 |
| PII exposure in logs, responses, or errors | Structured logs avoid password/token fields and profile detail interpolation; problem details include only validation details and correlation ID. | Serilog or new dependencies may accidentally log full request bodies unless reviewed. | PROFILE-03 |
| Invalid or malicious input | Username, email, display-name, length, character, and version validation reject invalid values. Unknown nullable display names are handled safely. | Unicode and normalization edge cases require ongoing test review. | PROFILE-04 |
| Stale update or duplicate profile | Update requires the returned optimistic version and returns 409 for stale versions. Profile creation requires an idempotency key and provides duplicate replay/conflict behavior. | An uncoordinated client can repeatedly retry without preserving the key or version. | PROFILE-05 |
| Personal-data retention after deletion | Soft delete keeps profile data and is currently a limitation; no purge or retention job is implemented. | Deleted profiles are not erased from persistent storage until a future retention process is added. | PROFILE-06 |

## Image upload and storage

Photo upload and serving are deliberately deferred by
[ADR 0006](../adr/0006-review-photo-storage-boundary.md). The following controls
are required before the feature is exposed and must be re-evaluated during
implementation:

| Threat | Required mitigation | Residual risk | Manual verification |
|---|---|---|---|
| Wrong or disguised file type | Allow an explicit file extension/MIME allowlist and verify safe content signatures; reject polyglots and active formats. | Format parsers and metadata extraction may have vulnerabilities or format-specific edge cases. | IMAGE-01, IMAGE-02 |
| Oversized or decompression-bomb file | Enforce request, per-file, and decompression limits before processing. | Large workload or memory pressure may still require infrastructure limits. | IMAGE-03 |
| Path traversal or overwriting | Generate server-side storage keys and reject caller-provided paths; never use an original filename directly. | An implementation mistake or unsafe SDK API could bypass the key policy. | IMAGE-04 |
| Unauthorized image read/write | Private storage by default, short-lived signed read URLs or authorized proxy reads, server-side authorization, and least-privilege write identity. | Signed URLs and cache configuration can extend access beyond the intended window. | IMAGE-05 |
| Malicious or executable object | Store outside executable paths and use content scanning when available. Do not render HTML/SVG as executable content unless explicitly reviewed. | A scanner is only a defense-in-depth measure and may miss novel payloads. | IMAGE-02, IMAGE-06 |
| Predictable or enumerable object keys | Use random server-generated identifiers and avoid sequential IDs or externally supplied names. | Database metadata or logs could leak names unless redacted. | IMAGE-04 |
| Overly permissive bucket policy | Deny public anonymous access unless a separately approved read boundary exists; grant only required service operations. | Cloud-policy drift can introduce public access outside application release. | IMAGE-05, IMAGE-06 |

## Device and session lifecycle

ChopChop does not currently implement app-owned refresh or logout state. Session
creation, refresh, revocation, MFA, and device records are managed by the
configured identity provider and must be tested against that provider.

| Threat | Mitigation | Residual risk | Manual verification |
|---|---|---|---|
| Refresh token replay or over-broad retention | Configure short-lived access tokens, rotating refresh tokens, reuse detection, and revocation in the identity provider. | Provider policy or browser compromise can invalidate or retain sessions outside API control. | SESSION-01 |
| Logout only in browser | Revocation must be initiated at the provider and current-device logout must not be presented as global revocation unless provider behavior proves it. | A copied access token remains valid until expiration even after provider logout. | SESSION-02, SESSION-03 |
| Stale or compromised device sessions | Use provider session/device controls to inspect and revoke stale or suspicious sessions, including all-device revocation where required. | Enforcement depends on provider support and user/admin configuration. | SESSION-03, SESSION-04 |
| Session fixation or account-switch confusion | Authorization-code flow with PKCE and provider-side session checks; local development mode never represents a real account. | Browser caches or IdP session cookies can cause sign-in into an unintended account. | SESSION-05 |
| Expiry while in use | Access tokens expire according to the provider. Clients silently refresh only within provider policy and restart sign-in when refresh fails. | Network loss or provider outage can interrupt refresh and create recoverable availability issues. | SESSION-06 |

## Cross-cutting mitigations

- Use HTTPS and HSTS outside local development, explicit CORS origins, secure
  headers, and no server response header.
- Use parameterized EF queries and never concatenate SQL or shell commands.
- Limit request bodies, fields, pages, upload types/sizes, and provider output.
- Use correlation IDs on logs and problem details, while never logging secrets,
  full allergy profiles, passwords, or bearer tokens.
- Apply rate limiting per authenticated subject or remote address and return
  429 when limits are exceeded.
- Continue dependency, secret, build, test, and package audits required by
  [Milestone 4 security operations](../milestone-4-security-operations.md).

## Residual-risk review trigger

Re-run manual verification and update this model when any of the following
change: identity provider, authentication library, API authorization policy,
profile fields or endpoints, image storage, CORS origin, login/session policy,
or cloud storage policy.
