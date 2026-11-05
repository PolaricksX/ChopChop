# Local Development

This guide runs the AI Recipe Generation, Interactive Review, Recipe
Management, Cookbook, User Service, and Meal Planner services with a local SQL
Server, RabbitMQ, Keycloak realm, and unified React client.
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
dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project src\UserService.Api
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

### User Service

User Service owns its tables in the shared `ChopChop` database. Configure its
connection string through local user secrets and apply its migration:

```powershell
dotnet ef database update --project src\UserService.Data --startup-project src\UserService.Api
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

For the User Service, allow the local Vite origin for this terminal and start
the API:

```powershell
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
dotnet run --project src\UserService.Api --launch-profile http
```

For Meal Planner, allow the same Vite origin and start its API:

```powershell
$env:Cors__AllowedOrigins__0 = "http://localhost:5173"
dotnet run --project src\MealPlanner.Api --launch-profile http
```

The ChopChop browser client requests `mealplans.read` and `mealplans.write`
along with its other API scopes. Configure the local identity provider to
grant these scopes and issue a token with the audience expected by
`src\MealPlanner.Api`; the API rejects tokens missing that audience or scopes.
If Vite selects a different port, use that origin in each API's CORS
configuration and in the identity provider's allowed web origins.

In another terminal:

```powershell
Copy-Item src\ChopChop.Client\.env.example src\ChopChop.Client\.env.local
Set-Location src\ChopChop.Client
npm ci
npm run dev
```

The browser client defaults to API ports `5081` and `5082`, the local Keycloak
realm URL, the public client ID, and the Meal Planner API at port `5033`.
`.env.local` may override those public URLs; it must never contain a client
secret.

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
- User Service API: `http://localhost:5199`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Meal Planner API: `http://localhost:5033`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Keycloak: `http://localhost:8080/realms/chopchop`.

The client uses `VITE_RECIPE_MANAGEMENT_API_URL` for the Recipe Management API
base URL. Set `VITE_RECIPE_MANAGEMENT_API_URL=http://localhost:20858` (or the
HTTPS URL above) in `src\ChopChop.Client\.env.local`; this is a public URL and
must not contain a client secret.

The client uses `VITE_COOKBOOK_API_URL` for the Cookbook API base URL. Set
`VITE_COOKBOOK_API_URL=http://localhost:20860` in
`src\ChopChop.Client\.env.local`; this is a public URL and must not contain a
client secret.

The client uses `VITE_USER_SERVICE_API_URL` for User Service requests. Set
`VITE_USER_SERVICE_API_URL=http://localhost:5199` in
`src\ChopChop.Client\.env.local`. User Service requests require the
`user-service` token audience and `users.read` / `users.write` scopes. The
imported Keycloak realm does not currently issue them; configure the public
`chopchop-web` client and its token audience separately before testing live
profile calls. Do not put credentials or a client secret in `.env.local`.

The APIs validate signed bearer tokens against Keycloak issuer and audience.
The moderation policy expects the realm role as a `role` access-token claim.
The imported realm configures that mapping. Local HTTP and local-only secrets
are development conveniences; production must use HTTPS and an appropriately
secured secret store.

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
