# Local Development

This guide runs the assigned AI Recipe Generation, Interactive Review, Recipe
Management, and Cookbook services with a local SQL Server, RabbitMQ, Keycloak
realm, and React client. The browser client now uses Auth0; the APIs still
default to the imported Keycloak realm unless their auth settings are
overridden as described below.
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

The browser client defaults to API ports `5081`, `5082`, `20858`, `20860`, and
`5033`, plus the configured Auth0 public client settings. `.env.local` may
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
- Keycloak: `http://localhost:8080/realms/chopchop`.

The client uses `VITE_RECIPE_MANAGEMENT_API_URL` for the Recipe Management API
base URL. Set `VITE_RECIPE_MANAGEMENT_API_URL=http://localhost:20858` (or the
HTTPS URL above) in `src\ChopChop.Client\.env.local`; this is a public URL and
must not contain a client secret.

The client uses `VITE_COOKBOOK_API_URL` for the Cookbook API base URL. Set
`VITE_COOKBOOK_API_URL=http://localhost:20860` in
`src\ChopChop.Client\.env.local`; this is a public URL and must not contain a
client secret.

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
