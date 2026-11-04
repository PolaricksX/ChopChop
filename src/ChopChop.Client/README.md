# ChopChop Browser Client

The React and TypeScript client uses OIDC authorization-code flow with PKCE.
It stores the OIDC user session in `sessionStorage` and has no client secret.

1. Follow the [local development guide](../../../docs/local-development.md)
   to start Keycloak and the APIs.
2. Copy `.env.example` to `.env.local` if local service URLs need overriding.
3. Run `npm ci`, then `npm run dev`.

The client supports recipe generation/status/confirmation, review submission
and reading, and a moderator queue. Photos are intentionally unavailable.

Run `npm run build`, `npm test`, and `npm run lint` to validate client changes.
