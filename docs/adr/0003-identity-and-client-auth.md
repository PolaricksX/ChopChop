# ADR 0003: OIDC bearer validation and browser authentication

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The platform rules and Milestone 3 PDF require real OAuth/OIDC authentication,
framework token validation, explicit API policies, and a public browser client
without embedded secrets.

## Decision

Use the framework JWT bearer handler in each API. Configure the OIDC
authority and API audience using `Auth:Authority` and `Auth:Audience` per
service and environment. Validate signature, issuer, audience, expiry, and
standard OIDC signing-key rotation. Use authorization-code flow with PKCE in
the public React client. Local Keycloak and configured Auth0 issuers are
supported. Require explicit named least-privilege scope/claim policies and an
authenticated fallback policy; only intentionally public routes such as
`/health` and published recipe reads are anonymous.

Owner IDs remain GUIDs in existing service data. Preserve GUID-valued `sub`
claims; for opaque provider subjects, derive a stable internal GUID from the
validated issuer and subject so separate issuers cannot share an owner key.
Never accept the owner identity from request data. This does not automatically
link a new opaque Auth0 subject to an existing account created under another
issuer; account migration requires a trusted subject-to-account mapping.

## Consequences

- Tests exercise the framework JWT handler with test signing keys and reject
  missing, invalid, expired, wrong-issuer, wrong-audience, and insufficient-scope
  tokens.
- No development bypass or client secret is allowed.
- Local Keycloak configuration contains no production credentials.
