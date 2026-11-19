# Start ChopChop Locally

These steps start the complete ChopChop site with Auth0 authentication and
sample data on Windows. An explicit local-auth mode is available for testing
without an identity-provider account.

## Requirements

Install and start Docker Desktop, install Git, the .NET 10 SDK, and Node.js
22.12 or newer (which includes npm).

## First-time setup

In PowerShell, clone the repository and create its local test password file:

```powershell
git clone https://github.com/PolaricksX/ChopChop.git
Set-Location ChopChop
Copy-Item .env.test.example .env.test
notepad .env.test
```

In Notepad, replace the `TEST_DB_PASSWORD` placeholder with a strong local-only
password, save the file, and close Notepad. Do not commit `.env.test`. Password must meet SQL Server complexity requirements.
Docker Compose resolves quoting and dollar-sign escapes in this file. Keep the
password unchanged after the database and broker are initialized: their
credentials are stored in the persistent volumes.

Install the browser client dependencies:

```powershell
Set-Location src\ChopChop.Client
if (-not (Test-Path .env.local)) { Copy-Item .env.example .env.local }
npm ci
Set-Location ..\..
```

For the default Auth0 mode, set `CHOPCHOP_AUTH0_DOMAIN` to the tenant hostname
shown in the client `.env.local` file (without `https://`). The client
`.env.local` must contain the Auth0 client settings and API audiences configured
for this project. The launcher passes the tenant authority and each API's
audience to its service process; it does not read or modify `.env.local`.
The `.env.example` values are public SPA configuration, not client secrets.

In PowerShell, set the launcher domain before starting:

```powershell
$env:CHOPCHOP_AUTH0_DOMAIN = "dev-z0oytbo8xgsxujpl.ca.auth0.com"
```

## Start the site

From the repository root, run:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher
```

Wait for startup to finish, then open <http://127.0.0.1:5174>. The launcher
starts the local database and message broker, applies migrations and sample
data, builds the APIs, and starts the browser client. Auth0 is enabled by
default; the identity provider must allow `http://127.0.0.1:5174/` as a
callback and logout URL and `http://127.0.0.1:5174` as a web origin.
Development authentication is disabled in this mode. The isolated SQL Server
uses `127.0.0.1,1435` on the host.

To run the same stack without signing in to Auth0, opt in to the local
development identity:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher -- --local-auth
```

The local-auth mode enables the fixed development identity only on the local
development stack and does not use Auth0 credentials.

Press Ctrl+C in the launcher terminal to stop the site. The local database and
broker data are preserved. For later starts, run the launcher command again
from the repository root; repeat `npm ci` only if the client dependencies need
to be installed or updated.
