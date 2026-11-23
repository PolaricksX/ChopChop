# Auth0 Local Launch Handoff

Use this guide to continue the Auth0-backed local setup on another development
machine. Do not commit `.env.local`, `.env.test`, access tokens, or secrets.

## Current state

- The React client uses the Auth0 React SDK for sign-in and account creation.
- The Auth0 SPA and eight API resource servers were created in the project
  tenant. API audiences and scopes are listed below.
- `src/ChopChop.Client/.env.example` contains public SPA configuration and the
  seven API audiences currently requested by the client.
- The local launcher defaults to Auth0. Its `--local-auth` option enables the
  fixed development identity for no-login local testing.
- The SPA was initially configured for `http://localhost:5173`. Before using
  the full launcher, add the launcher's origin to the Auth0 SPA application:
  - Callback URL: `http://127.0.0.1:5174/`
  - Logout URL: `http://127.0.0.1:5174/`
  - Web origin: `http://127.0.0.1:5174`
- Those URL settings are not yet confirmed in the Auth0 application. Update
  them in the Auth0 Dashboard or with the Auth0 MCP, preserving existing
  localhost URLs.

## First run on this machine

From the repository root in PowerShell:

1. Copy `.env.test.example` to `.env.test` and set a strong local-only
   `TEST_DB_PASSWORD`. Do not commit this file.
2. Create the ignored browser-client config from the tracked example if it does
   not already exist:

   ```powershell
   Set-Location src\ChopChop.Client
   if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
   npm ci
   Set-Location ..\..
   ```

3. Set the Auth0 tenant domain from `VITE_AUTH0_DOMAIN` in `.env.local` as
   `CHOPCHOP_AUTH0_DOMAIN`, without `https://`. From the repository root:

   ```powershell
   $domainLine = Get-Content src\ChopChop.Client\.env.local |
     Where-Object { $_ -match '^VITE_AUTH0_DOMAIN=' } |
     Select-Object -First 1
   $env:CHOPCHOP_AUTH0_DOMAIN = $domainLine.Substring('VITE_AUTH0_DOMAIN='.Length)
   ```

   The launcher requires this environment variable and does not read
   `.env.local` itself.
4. Start the complete backend and client stack:

   ```powershell
   dotnet run --project tools\ChopChop.LocalLauncher
   ```

5. Open <http://127.0.0.1:5174>, sign in through Auth0, and test the API-backed
   flows. If token validation or authorization fails, inspect the Auth0 API
   audience and scope configuration and the API response status; do not enable
   development auth as a workaround for an Auth0-mode test.

For local testing without an identity provider, run:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher -- --local-auth
```

## Auth0 APIs

| Service | Audience | Scopes |
|---|---|---|
| AI Recipe Generation | `https://api.chopchop.local/ai-recipe-generation` | `recipe-generations.create`, `recipe-generations.read`, `recipe-generations.confirm` |
| Interactive Review | `https://api.chopchop.local/interactive-review` | `reviews.create`, `reviews.read`, `reviews.write`, `reviews.moderate` |
| Recipe Management | `https://api.chopchop.local/recipe-management` | `recipes.read`, `recipes.write`, `recipes.publish`, `recipes.admin` |
| Cookbook | `https://api.chopchop.local/cookbook` | `cookbook.read`, `cookbook.write` |
| Meal Planner | `https://api.chopchop.local/meal-planner` | `mealplans.read`, `mealplans.write` |
| Pantry | `https://api.chopchop.local/pantry` | `pantry.read`, `pantry.write` |
| Equipment | `https://api.chopchop.local/equipment` | `equipment.read`, `equipment.write` |
| User Service | `https://api.chopchop.local/user-service` | `users.read`, `users.write`, `users.admin` |

Create these Auth0 roles ([ADR 0008](./adr/0008-admin-and-pro-chef-roles.md)):

| Role | Permissions to assign |
|---|---|
| Pro Chef | `recipe-generations.create`, `recipe-generations.confirm` (AI Recipe Generation API) |
| Admin | `reviews.moderate` (Interactive Review), `users.admin` (User Service), `recipes.admin` (Recipe Management) |

Give every signed-in user the ordinary read/write permissions through a base or
default role. Grant `reviews.moderate`, `users.admin`, `recipes.admin`, and the
generation create/confirm permissions only through the roles above, and never
as a scope the browser client requests on its own.

### Sign-up role choice

The sign-up form offers **User** or **Pro Chef** (never Admin). The client sends
the choice as the `requested_role` authorize parameter. The Post-Login Action
[`infra/auth0/actions/assign-signup-role.js`](../infra/auth0/actions/assign-signup-role.js)
checks it on the first login (`logins_count === 1`) and assigns the matching role
through the Management API. Only the allowlist in that file decides which choices
are honored; change the rules there. Role IDs and the Management API client
secrets live in Action secrets (`PRO_CHEF_ROLE_ID`, `domain`, `clientId`,
`clientSecret`). The Management API client needs `update:users` and `read:roles`
and is never given to the browser client. Verify in a tenant that
`event.request.query` contains `requested_role` before relying on it.

The Pro Chef sign-up option is open to anyone until the criteria change, so
generation access is not yet gated by a real eligibility check.

The APIs use RS256 and the Auth0 tenant as their issuer/authority. Do not use
the Auth0 Management API audience or grant Management API permissions to the
browser client.

## Validation and known state

At handoff, the full .NET solution build passed, local-launcher tests passed
(5 tests), client tests passed (70 tests), and the client production build
passed. Client lint completed with four existing warnings. A previous full
solution test run had authentication integration-test failures in AI Recipe
Generation and Interactive Review, and one Equipment SQL Server persistence
test was skipped; re-run and investigate these before treating the full suite
as green.

The launcher and setup instructions are in
[`tools/ChopChop.LocalLauncher/Program.cs`](../tools/ChopChop.LocalLauncher/Program.cs)
and [`docs/local-development.md`](./local-development.md).
