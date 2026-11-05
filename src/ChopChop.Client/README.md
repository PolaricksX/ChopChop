# ChopChop Browser Client

The React and TypeScript client uses OIDC authorization-code flow with PKCE.
It stores the OIDC user session in `sessionStorage` and has no client secret.

1. Follow the [local development guide](../../../docs/local-development.md)
   to start Keycloak and the APIs.
2. Copy `.env.example` to `.env.local` if local service URLs need overriding.
3. Run `npm ci`, then `npm run dev`.

The client supports recipe generation/status/confirmation, review submission
and reading, a moderator queue, Cookbook folders, and the Equipment inventory
tab. Equipment requests use authenticated HTTP DTOs and never include an owner
identifier.

Set `VITE_EQUIPMENT_API_URL` to the Equipment API origin if it is not running
at the local default `http://localhost:5018`. Configure the public Keycloak
client for authorization-code flow with PKCE S256 and request the
`equipment.read` and `equipment.write` scopes.

Run `npm run build`, `npm test`, and `npm run lint` to validate client changes.
