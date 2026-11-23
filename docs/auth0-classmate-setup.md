# Auth0 setup for classmates

This guide is for running the complete ChopChop site on your own Windows PC
using the team's existing Auth0 development tenant. You do not need to create
your own Auth0 tenant or have access to the Auth0 dashboard. The commands below
use PowerShell.

## What you need

- Git and access to the ChopChop repository.
- Docker Desktop running, the .NET 10 SDK, and Node.js 22.12 or newer with npm.
- An account that can sign in to the team's Auth0 application. Use your own
  account; ask a team administrator if sign-in is unavailable.
- A current checkout containing the Auth0 MRRT client changes. If you already
  cloned the repository, get the team's latest changes before starting.

## First-time setup

1. Clone the repository if needed, then open a PowerShell terminal in the
   repository root (the folder containing `Solution.sln`):

   ```powershell
   git clone https://github.com/PolaricksX/ChopChop.git
   Set-Location ChopChop
   ```

2. Create the local database password file if you do not already have one:

   ```powershell
   if (-not (Test-Path .env.test)) { Copy-Item .env.test.example .env.test }
   notepad .env.test
   ```

   Replace the placeholder with your own strong password that meets SQL Server
   complexity requirements. Save and close the file. Keep the same password
   after the local database and broker are initialized; their data persists
   between runs.

3. Set up the browser client:

   ```powershell
   Set-Location src\ChopChop.Client
   if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
   npm ci
   Set-Location ..\..
   ```

   The example contains the team's public Auth0 domain, SPA client ID, and API
   audiences. If you already had `.env.local`, check that its `VITE_AUTH0_*`
   values match the current `.env.example` or the settings supplied by the
   team. Do not copy another person's access tokens or credentials.

4. In the same PowerShell terminal, set the domain for the backend launcher:

   ```powershell
   $env:CHOPCHOP_AUTH0_DOMAIN = "dev-z0oytbo8xgsxujpl.ca.auth0.com"
   ```

   This must match `VITE_AUTH0_DOMAIN` in the client `.env.local`, without
   `https://`. The launcher does not read the client file. The variable applies
   only to this PowerShell session, so set it again in a new terminal.

## Start and test

From the repository root, with Docker Desktop running:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher
```

Wait for startup to finish, then open <http://127.0.0.1:5174>. Select **Sign in
to continue** or **Create an account** and follow Auth0's prompts. Try Cookbook,
My Pantry, and Equipment while signed in. Sample data is local to your PC.
Press **Ctrl+C** in the launcher terminal to stop the site. For later runs,
set `CHOPCHOP_AUTH0_DOMAIN` again if using a new terminal and rerun the
launcher; `npm ci` is normally only needed after dependencies change.

Use the exact `127.0.0.1:5174` address with the launcher. `localhost` and
`127.0.0.1` are different browser origins: if Auth0 reports a callback, logout,
or web-origin mismatch, ask the tenant administrator to verify the SPA's
allowed URLs. Do not change shared tenant settings yourself.

## Troubleshooting

- **Missing `.env.test`, Docker, or a port is already in use:** read the
  launcher message, start Docker Desktop, and stop the conflicting local
  service if needed. See [local development](./local-development.md).
- **API access is not configured:** check the relevant `VITE_AUTH0_*_AUDIENCE`
  in `src/ChopChop.Client/.env.local`, then restart the launcher.
- **Missing refresh token or requested scopes:** confirm you have the latest
  client changes and the correct tenant settings, then sign out and sign back
  in. If it persists, tell the team which API and scope were reported. The
  tenant administrator should check that API's MRRT policy and permissions;
  don't add unrelated scopes to the browser's default request.
- **401 or 403 from an API:** confirm the frontend domain matches
  `CHOPCHOP_AUTH0_DOMAIN`. A 401 can indicate a token/issuer/audience problem;
  a 403 can indicate missing permission. Ask the administrator to check the
  API configuration and your role rather than granting yourself permissions.
- **Reviews:** Reviews MRRT and moderation authorization were not yet fully
  configured and tested at the time of this guide. Ask the team before testing
  Reviews or assuming moderation is restricted. Do not grant moderator access
  to ordinary users.

If you only need to work locally without Auth0, the launcher also supports
`dotnet run --project tools\ChopChop.LocalLauncher -- --local-auth`. This uses
a fixed development identity and **does not test Auth0**.

## Security

The SPA client ID, tenant domain, and API audiences are public configuration,
not passwords. Never put a client secret in a `VITE_` variable or browser code;
never commit `.env.local`, `.env.test`, tokens, or passwords. The browser stores
Auth0 tokens in local storage, so use a trusted browser profile and sign out on
shared computers. Do not give the SPA Auth0 Management API permissions.

For detailed launcher information, see [local development](./local-development.md).
