# Pantry Client

The Vite/React client uses only the versioned Pantry HTTP contract (`/api/v1/pantry`) and public request/response DTOs.

## Run

```bash
npm install
npm run dev
npm test
npm run build
```

Set `VITE_API_URL=http://localhost:5131` when using the local Pantry API.
The client uses OIDC authorization code with PKCE. `VITE_OIDC_AUTHORITY`
defaults to `http://localhost:8080/realms/chopchop` and
`VITE_OIDC_CLIENT_ID` defaults to `chopchop-web`. Configure the identity
provider for the browser origin, `/auth/callback`, `/auth/silent-callback`, the
`/` post-logout redirect, `pantry.read`, `pantry.write`, and the `pantry-api`
audience. The client
does not contain a secret. See `docs/pantry.md` for API setup.

The UI provides search, type, availability and expiration filters, paging, accessible loading/empty/error states, validation, and add/edit/consume/unavailable/delete actions.
