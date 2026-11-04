# ADR 0003: OIDC bearer validation and browser authentication

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The platform rules and Milestone 3 PDF require real OAuth/OIDC authentication,
framework token validation, explicit API policies, and a public browser client
without embedded secrets.

## Decision

Use the framework JWT bearer handler in both APIs. Configure the OIDC
authority and API audience using `Auth:Authority` and `Auth:Audience` per
service and environment. Validate signature, issuer, audience, expiry, and
standard OIDC signing-key rotation. Use Keycloak for local identity, and
authorization-code flow with PKCE in the React client. Require explicit named
least-privilege scope/claim policies and an authenticated fallback policy;
only intentionally public routes such as `/health` are anonymous.

## Consequences

- Tests exercise the framework JWT handler with test signing keys and reject
  missing, invalid, expired, wrong-issuer, wrong-audience, and insufficient-scope
  tokens.
- No development bypass or client secret is allowed.
- Local Keycloak configuration contains no production credentials.
