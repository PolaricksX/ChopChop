# Local Development

This guide runs the AI Recipe Generation, Interactive Review, Recipe
Management, Cookbook, User Equipment, Pantry, and Meal Planner services with a
local SQL Server, RabbitMQ, identity provider, and unified React client. The
ChopChop browser client uses Auth0 when configured; the APIs default to the
imported Keycloak realm unless their auth settings are overridden as described
below.
Do not use the development settings or credentials in a shared or production
environment.

## 1. Start the local dependencies

1. Copy `.env.example` to `.env` and replace every sample password with a
   different strong local password.
2. Start the dependencies from the repository root:

   ```powershell
   docker compose up -d
   ```

   The compose file starts SQL Server on `1433`, RabbitMQ on `5672` (management
   UI `15672`), and Keycloak on `8080`. Keycloak imports the `chopchop` realm,
   the public `chopchop-web` client, the `chopchop-api` audience mapper, and
   the `review-moderator` realm role.
3. Use Keycloak's realm registration page to create a local account. To test
   moderation, assign the `review-moderator` realm role to a local test user
   from the Keycloak admin console.

## Optional: skip sign-in for local testing

The APIs and browser clients support a local-only test identity. It has a fixed
subject (`00000000-0000-0000-0000-000000000001`) and bypasses API authorization
policies, so all local data created in this mode belongs to the same test user.
Do not expose these services to a shared or production environment while this
mode is on.

Set these variables in the terminals that start the APIs and browser client:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Auth__DevelopmentMode = "true"
$env:VITE_DEV_AUTH = "true"
```

Then start the API and React projects as usual. `Auth__DevelopmentMode` enables
the API test identity and skips bearer-token validation; `VITE_DEV_AUTH` skips
browser sign-in and sends a local placeholder token. The API accepts it only
when its own test mode is enabled. Both settings are off by default, and APIs
fail startup if the test mode is enabled outside `Development`. To return to
normal authentication, close those terminals or remove the variables:

```powershell
Remove-Item Env:Auth__DevelopmentMode -ErrorAction SilentlyContinue
Remove-Item Env:VITE_DEV_AUTH -ErrorAction SilentlyContinue
```

## 2. Configure local secrets

Set `ASPNETCORE_ENVIRONMENT=Development` in the shell that runs .NET commands.
Add the same SQL connection string and RabbitMQ URI as user secrets to the
API projects below. Replace the bracketed values locally; do not commit them:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$connection = "Server=localhost,1433;Database=ChopChop;User Id=sa;Password=<your-sql-password>;Encrypt=True;TrustServerCertificate=True"
$rabbit = "amqp://<rabbit-user>:<rabbit-password>@localhost:5672/"

dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project src\AiRecipeGeneration.Api
dotnet user-secrets set "Messaging:ConnectionString" $rabbit --project src\AiRecipeGeneration.Api
dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project src\InteractiveReview.Api
dotnet user-secrets set "Messaging:ConnectionString" $rabbit --project src\InteractiveReview.Api
dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project src\RecipeManagement.Api
dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project src\Cookbook.Api
```

For AI generation, also configure the Azure OpenAI v1 endpoint, deployment
name, and API key only in the AI API's user secrets:

```powershell
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com" --project src\AiRecipeGeneration.Api
dotnet user-secrets set "AzureOpenAI:Deployment" "<deployment-name>" --project src\AiRecipeGeneration.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "<secret>" --project src\AiRecipeGeneration.Api
```

The adapter appends `/openai/v1/chat/completions` to the endpoint. Never place
an API key, SQL password, broker password, or OAuth client secret in source
control, `.env.example`, browser configuration, or test data.

## 3. Apply each service's migrations

Each service owns its schema and migration history in the shared `ChopChop`
database. Run each migration from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project src\AiRecipeGeneration.Data --startup-project src\AiRecipeGeneration.Api --context AiRecipeDbContext
dotnet ef database update --project src\InteractiveReview.Data --startup-project src\InteractiveReview.Api --context InteractiveReviewDbContext
```

The AI service uses schema `ai_recipe`; the review service uses
`interactive_review`. Do not modify the other service's tables.

### Recipe Management

Recipe Management owns the `recipe_management` schema in the shared
`ChopChop` database. Configure `ConnectionStrings:ChopChop` through local user
secrets for `src\RecipeManagement.Api`; do not commit the connection string or
any credentials. Apply its migration from the repository root:

```powershell
dotnet ef database update --project src\RecipeManagement.Data --startup-project src\RecipeManagement.Api --context RecipeManagementDbContext
```

### Cookbook

Cookbook owns the `cookbook` schema in the shared `ChopChop` database. Configure
`ConnectionStrings:ChopChop` through local user secrets for `src\Cookbook.Api`;
do not commit the connection string or any credentials. Apply its migration
from the repository root using the design-time override:

```powershell
$env:ConnectionStrings__Cookbook = $connection
dotnet ef database update --project src\Cookbook.Data --startup-project src\Cookbook.Api --context CookbookDbContext
```

Cookbook local development quick reference:

- API URL: `http://localhost:20860`.
- User secret key: `ConnectionStrings:ChopChop` on `src\Cookbook.Api`.
- Migration override: set `ConnectionStrings__Cookbook` before running `dotnet ef database update` for `CookbookDbContext`.
- Client API URL setting: `VITE_COOKBOOK_API_URL=http://localhost:20860` in `src\ChopChop.Client\.env.local`.

## 4. Start APIs and client

Open separate PowerShell terminals from the repository root:

```powershell
dotnet run --project src\AiRecipeGeneration.Api --launch-profile AiRecipeGeneration.Api
```

```powershell
dotnet run --project src\InteractiveReview.Api --launch-profile InteractiveReview.Api
```

```powershell
dotnet run --project src\RecipeManagement.Api --launch-profile RecipeManagement.Api
```

```powershell
dotnet run --project src\Cookbook.Api --launch-profile Cookbook.Api
```

In another terminal:

```powershell
Copy-Item src\ChopChop.Client\.env.example src\ChopChop.Client\.env.local
Set-Location src\ChopChop.Client
npm ci
npm run dev
```

The unified browser client defaults to API ports `5081`, `5082`, `20858`,
`20860`, `5033`, `5018`, and `5131`, plus the configured Auth0 public client
settings. Its **My Pantry** and **Equipment** tabs use the same sign-in and
navigation as the rest of ChopChop. `.env.local` may
override those public values; it must never contain a client secret.

Available endpoints:

- AI Recipe API: `http://localhost:5081`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Interactive Review API: `http://localhost:5082`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Recipe Management API: `http://localhost:20858` (HTTPS:
  `https://localhost:20857`); OpenAPI document at `/openapi/v1.json`; health at
  `/health`.
- Cookbook API: `http://localhost:20860`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- User Equipment API: `http://localhost:5018`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Pantry API: `http://localhost:5131`; OpenAPI document at `/openapi/v1.json`;
  health at `/health`.
- Keycloak: `http://localhost:8080/realms/chopchop`.

The client uses `VITE_RECIPE_MANAGEMENT_API_URL` for the Recipe Management API
base URL. Set `VITE_RECIPE_MANAGEMENT_API_URL=http://localhost:20858` (or the
HTTPS URL above) in `src\ChopChop.Client\.env.local`; this is a public URL and
must not contain a client secret.

The client uses `VITE_COOKBOOK_API_URL` for the Cookbook API base URL. Set
`VITE_COOKBOOK_API_URL=http://localhost:20860` in
`src\ChopChop.Client\.env.local`; this is a public URL and must not contain a
client secret.

The My Pantry and Equipment tabs use `VITE_PANTRY_API_URL` and
`VITE_EQUIPMENT_API_URL`; they default to `http://localhost:5131` and
`http://localhost:5018` respectively. These are public API URLs, not secrets.

The Meal Plans tab uses `VITE_MEAL_PLANNER_API_URL` for its API base URL. It
defaults to `http://localhost:5033`; override it in
`src\ChopChop.Client\.env.local` when the Meal Planner API runs elsewhere.

When using local Keycloak, the APIs validate signed bearer tokens against the
issuer and audience from their development settings. Local HTTP and local-only
secrets are development conveniences; production must use HTTPS and an
appropriately secured secret store.

### Using Auth0 with the browser client and APIs

The client Auth0 application must allow `http://localhost:5173/` as its
callback and logout URL and `http://localhost:5173` as its web origin. Its Vite
dev server is pinned to port `5173`.

Do **not** configure the Auth0 Management API (`https://<tenant>/api/v2/`) as
the ChopChop API or grant its management permissions to the SPA. Create a
separate Auth0 API (resource server) for each backend. Use these stable API
identifiers, or replace them consistently in the client env and API user
secrets:

| API | Identifier/audience | Delegated API permissions |
|---|---|---|
| AI Recipe Generation | `https://api.chopchop.local/ai-recipe-generation` | `recipe-generations.create`, `recipe-generations.read`, `recipe-generations.confirm` |
| Interactive Review | `https://api.chopchop.local/interactive-review` | `reviews.create`, `reviews.read`, `reviews.write`, `reviews.moderate` |
| Recipe Management | `https://api.chopchop.local/recipe-management` | `recipes.read`, `recipes.write`, `recipes.publish` |
| Cookbook | `https://api.chopchop.local/cookbook` | `cookbook.read`, `cookbook.write` |
| User Service | `https://api.chopchop.local/user-service` | `users.read`, `users.write` |
| Meal Planner | `https://api.chopchop.local/meal-planner` | `mealplans.read`, `mealplans.write` |
| User Equipment | `https://api.chopchop.local/equipment` | `equipment.read`, `equipment.write` |
| Pantry | `https://api.chopchop.local/pantry` | `pantry.read`, `pantry.write` |

For each API, set its signing algorithm to RS256, enable RBAC, and enable
permissions in the access token. Assign only the needed permissions to user
roles: `reviews.moderate` should be granted only to moderators. The browser
client requests user-delegated access; do not enable a client secret or use
client-credentials access from the browser. Public endpoints need no scope.
The client uses scopes from its API-specific list and each API also recognizes
Auth0's `permissions` array claim.

Set the client audiences in `src\ChopChop.Client\.env.local` using the
identifiers above. The SPA gets a distinct audience and scope set for each
backend. Configure each API with the tenant issuer and its own expected
audience through local user secrets:

```powershell
$issuer = "https://<your-auth0-domain>/"
$apiAudiences = @{
  "src\AiRecipeGeneration.Api" = "https://api.chopchop.local/ai-recipe-generation"
  "src\InteractiveReview.Api" = "https://api.chopchop.local/interactive-review"
  "src\RecipeManagement.Api" = "https://api.chopchop.local/recipe-management"
  "src\Cookbook.Api" = "https://api.chopchop.local/cookbook"
  "src\UserService.Api" = "https://api.chopchop.local/user-service"
  "src\MealPlanner.Api" = "https://api.chopchop.local/meal-planner"
  "src\Equipment.Api" = "https://api.chopchop.local/equipment"
  "src\Pantry.Api" = "https://api.chopchop.local/pantry"
}

foreach ($project in $apiAudiences.Keys) {
  dotnet user-secrets set "Auth:Authority" $issuer --project $project
  dotnet user-secrets set "Auth:Audience" $apiAudiences[$project] --project $project
}
```

The APIs validate tokens with their framework JWT bearer handlers, including
signature, issuer, audience, expiry, signing-key rotation, and the configured
algorithm allowlist. Auth0's API permissions are checked from the validated
`scope`, `scp`, or `permissions` claims; the Management API permissions shown
on the Auth0 Management API page are not relevant to these resource servers.
API user secrets are local configuration and must not be committed.

Existing data owned by a GUID-valued subject remains associated with that
GUID. An opaque Auth0 subject gets a deterministic internal GUID derived from
issuer and subject; this does not automatically link accounts across identity
providers.

## 5. Validate changes

```powershell
dotnet restore Solution.sln
dotnet build Solution.sln -c Release --no-restore
dotnet test Solution.sln -c Release --no-build
dotnet format Solution.sln --verify-no-changes
dotnet list Solution.sln package --vulnerable --include-transitive

Set-Location src\ChopChop.Client
npm ci
npm run build
npm test
npm run lint
```

API integration tests validate JWT behavior using a framework bearer handler
and test signing key. Database-backed integration tests require the local
SQL Server and RabbitMQ services.

## Isolated test database

The test stack uses its own SQL Server and RabbitMQ containers, host ports
`1434` and `5673`, and named volumes. It does not use or modify the databases,
broker, or volumes from `compose.yaml`. The one-shot seeder applies all eight
service migrations and inserts deterministic starter data for the local test identity
`00000000-0000-0000-0000-000000000001`. Operational tables such as outboxes,
idempotency records, and migration history are not starter-seeded. Pantry's
starter item is inserted without invoking the outbox-producing save path.
Examples include four published recipes, three cookbook folders, three reviews,
three AI requests, six equipment items, eight pantry items, and two meal plans.
The seeder also adds a second test user profile for ownership testing; sign-in
as the development identity continues to show the first user's data. Pantry
expiration dates and meal-plan dates are relative to the first seed run, and
rerunning the seeder preserves existing records rather than refreshing dates.
If a user has already created a matching active pantry or equipment item, the
corresponding example is not duplicated. Auth0 users have a different subject
and will not automatically see the development identity's data.

From the repository root, create a local-only password file if one does not
already exist, then replace its placeholder with a strong password:

```powershell
if (-not (Test-Path .env.test)) { Copy-Item .env.test.example .env.test }
notepad .env.test
```

Start SQL Server and run the migrations/seeder:

```powershell
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml up --build -d
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml logs seed
```

The database is available at
`Server=127.0.0.1,1434;Database=ChopChopTest;User Id=sa;...`; the broker is
available at `amqp://chopchop_test:<password-from-.env.test>@127.0.0.1:5673/`.
Use `Encrypt=True;TrustServerCertificate=True` for the database connection.
To repeat the idempotent migrations and seed:

```powershell
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml run --build --rm seed
```

For an unmodified isolated test database, verify the expected starter records:

```powershell
Get-Content -Raw tools\ChopChop.Database.Seeder\verify-starter-data.sql |
  docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml exec -T sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d ChopChopTest -b'
```

The check expects the initial starter records and their relationships; it is not
intended to pass after editing or removing them through the application.

Stop the test stack and keep its data with:

```powershell
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml down
```

To reset only this isolated test database and its volume, use:

```powershell
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml down -v
```

Do not use `down -v` on the regular development Compose stack unless you intend
to remove its data.

### Start the complete local test site

After creating `.env.test` once and installing the unified client dependencies
with `npm ci` in `src\ChopChop.Client`, start the isolated database, broker,
seeder, all eight APIs, and the unified browser client from the repository root:

```powershell
dotnet run --project tools\ChopChop.LocalLauncher
```

The Windows launcher keeps the services in separate processes and streams their
logs into one terminal. Press Ctrl+C or close the terminal to stop the apps and
the isolated test database and broker, even if those containers were already
running. Their named data volumes are preserved. Cleanup after closing the
window is best effort if Docker is unavailable; any failure is reported the
next time the launcher starts. The launcher checks the API and browser client
ports first and reports conflicts before starting. It reuses the isolated Compose
services when they are already running; Docker Compose reports conflicts on
infrastructure ports. `--dry-run` checks launcher configuration and prints the
planned services without starting them. Repeated EF Core SQL command logs are
reduced to warnings and errors; application logs and other EF Core warnings
remain visible.

Use the unified browser site:

- ChopChop: `http://127.0.0.1:5174`

The My Pantry and Equipment tabs are available in ChopChop. The standalone
Pantry client remains available for compatibility and can be started separately
from `src\Pantry.Client` on port 5175 with its own `npm ci` and
`npm run dev -- --port 5175` commands.

Development authentication is enabled for local testing. When finished, press
Ctrl+C in the launcher terminal or close it. If automatic Docker cleanup fails,
stop the isolated database and broker while preserving their data with:

```powershell
docker compose --project-name chopchop-test --env-file .env.test -f compose.test.yaml down
```

Use the `down -v` reset command above only when you want to delete the isolated
test database and broker data.
