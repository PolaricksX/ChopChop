# Start ChopChop Locally

These steps start the complete ChopChop site with local development
authentication and sample data on Windows.

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

Install the browser client dependencies:

```powershell
Set-Location src\ChopChop.Client
npm ci
Set-Location ..\..
```

## Start the site

From the repository root, run:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher
```

Wait for startup to finish, then open <http://127.0.0.1:5174>. The launcher
starts the local database and message broker, applies migrations and sample
data, builds the APIs, and starts the browser client. Development
authentication is enabled; no identity-provider account is needed.

Press Ctrl+C in the launcher terminal to stop the site. The local database and
broker data are preserved. For later starts, run the launcher command again
from the repository root; repeat `npm ci` only if the client dependencies need
to be installed or updated.
