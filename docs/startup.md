# Local startup (development, no Keycloak)

For this milestone the site uses a fake development sign-in. No Keycloak is needed.

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Docker Desktop (running)

## 1. Configure Docker secrets

Docker Compose reads passwords from a root `.env` file, which is gitignored.

```powershell
Copy-Item .env.example .env
```

Edit `.env` and replace the placeholder passwords. Never commit it.

## 2. Start SQL Server and RabbitMQ

```powershell
docker compose up -d sqlserver rabbitmq
```

Wait about a minute for SQL Server to become ready.

## 3. Enable the fake sign-in in the client

Create `src/ChopChop.Client/.env.local` (gitignored) containing:

```
VITE_DEV_AUTH=true
```

This works only in Vite dev mode. The APIs accept the fake user because
`Auth:DevelopmentAuthentication` is `true` in each API's
`appsettings.Development.json`. Startup fails if it is enabled outside the
Development environment.

## 4. Start all APIs

From the repository root:

```powershell
dotnet run tools/RunAll.cs
```

| API | URL |
|---|---|
| RecipeManagement | http://localhost:20858 |
| Cookbook | http://localhost:20860 |
| UserService | http://localhost:5199 |
| MealPlanner | http://localhost:5033 |
| AiRecipeGeneration | http://localhost:5081 |
| InteractiveReview | http://localhost:5082 |

Logs are prefixed with the service name. Press Ctrl+C to stop all APIs.

## 5. Check health

`/health` is anonymous and checks each service's own database.

```powershell
foreach ($p in 5081,5082,20858,20860,5199,5033) {
  try { $r = (Invoke-WebRequest "http://localhost:$p/health" -UseBasicParsing -TimeoutSec 10).StatusCode }
  catch { $r = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { $_.Exception.Message } }
  "$p : $r"
}
```

Every service should return `200`. A `500` or `503` usually means SQL Server is
not running or not ready. AiRecipeGeneration also needs RabbitMQ.

## 6. Start the client

```powershell
cd src/ChopChop.Client
npm install
npm run dev
```

Open http://localhost:5173 and click **Sign in**. You are signed in as
"Development User". Restart `npm run dev` after changing `.env.local`.

## 7. Try each feature

- AI recipe generation and interactive review
- Recipes
- Cookbook
- Profile (UserService)
- Meal plans (MealPlanner)

Use the browser Network tab to find failures:

- `401`: the API is not running with `Auth:DevelopmentAuthentication` enabled.
- CORS error or failed request: the API is not running or the port is wrong.

If recipes fail to load, make sure `VITE_RECIPE_MANAGEMENT_API_URL` (default
`http://localhost:5083`) points at `http://localhost:20858`.

## Automated checks

```powershell
dotnet test
cd src/ChopChop.Client; npx vitest run
```

Integration tests need SQL Server from Docker.
