# Manual local development

This is an alternative to the [local launcher](./local-development.md). It uses
the separate `docker-compose.yml` stack and its own database and broker
volumes, not the launcher's isolated test stack. Development credentials and
HTTP endpoints must not be used in shared or production environments.

## Dependencies and secrets

Copy `.env.example` to `.env` and replace every placeholder with a strong,
local-only credential. Do not commit the file. Start SQL Server, RabbitMQ, and
Keycloak explicitly with:

```powershell
docker compose -f docker-compose.yml up -d
```

SQL Server is exposed on port 1433, RabbitMQ on 5672 (management UI 15672),
and Keycloak on 8080. The imported Keycloak realm includes the `chopchop-web`
client and `review-moderator` role. Create a local user through realm
registration and assign the moderator role in Keycloak only when testing
moderation. The browser client uses Auth0, so its tokens will not work with
APIs configured for Keycloak; configure the APIs for Auth0 to use that client.

In the shell used for migrations, set the Development environment and configure
local secrets. Replace the bracketed values; never commit them.

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

For AI generation, also set `AzureOpenAI:Endpoint`, `AzureOpenAI:Deployment`,
and `AzureOpenAI:ApiKey` as user secrets on `src\AiRecipeGeneration.Api`.
The endpoint is the Azure resource URL; the adapter appends
`/openai/v1/chat/completions`.

## Migrations and startup

From the repository root, apply the migrations for the services you run:

```powershell
dotnet ef database update --project src\AiRecipeGeneration.Data --startup-project src\AiRecipeGeneration.Api --context AiRecipeDbContext
dotnet ef database update --project src\InteractiveReview.Data --startup-project src\InteractiveReview.Api --context InteractiveReviewDbContext
dotnet ef database update --project src\RecipeManagement.Data --startup-project src\RecipeManagement.Api --context RecipeManagementDbContext
$env:ConnectionStrings__Cookbook = $connection
dotnet ef database update --project src\Cookbook.Data --startup-project src\Cookbook.Api --context CookbookDbContext
```

These services own the `ai_recipe`, `interactive_review`, `recipe_management`,
and `cookbook` schemas respectively. Do not modify another service's tables.
Start each API in a separate terminal, for example:

```powershell
dotnet run --project src\AiRecipeGeneration.Api --launch-profile AiRecipeGeneration.Api
dotnet run --project src\InteractiveReview.Api --launch-profile InteractiveReview.Api
dotnet run --project src\RecipeManagement.Api --launch-profile RecipeManagement.Api
dotnet run --project src\Cookbook.Api --launch-profile Cookbook.Api
```

In another terminal, copy `src\ChopChop.Client\.env.example` to `.env.local`
if needed, then run `npm ci` and `npm run dev` from `src\ChopChop.Client`.
The client uses port 5173 for this manual setup. Its `.env.local` contains
public API URLs and Auth0 settings, never client secrets.

## Auth0

For the manual client origin, Auth0 must allow `http://localhost:5173/` as a
callback and logout URL and `http://localhost:5173` as a web origin. Configure
each API's `Auth:Authority` and its own `Auth:Audience` using local user
secrets. The audience must match the corresponding client setting. Use
separate RS256 resource servers, enable RBAC and permissions in access tokens,
and grant only the permissions needed by each role. In particular,
`reviews.moderate` is reserved for moderators; see the Reviews caveat in the
[classmate Auth0 guide](./auth0-classmate-setup.md).

Do not grant the SPA Auth0 Management API permissions or put an OAuth client
secret in browser configuration. APIs validate signed tokens and permissions;
an opaque subject receives a deterministic internal identifier and is not
automatically linked to an account from another identity provider.

For validation commands and security requirements, see
[milestone 4 security operations](./milestone-4-security-operations.md).
