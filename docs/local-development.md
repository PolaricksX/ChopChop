# Local Development

This guide runs the assigned AI Recipe Generation and Interactive Review
services with a local SQL Server, RabbitMQ, Keycloak realm, and React client.
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
Add the same SQL connection string and RabbitMQ URI as user secrets to both
API projects. Replace the bracketed values locally; do not commit them:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$connection = "Server=localhost,1433;Database=ChopChop;User Id=sa;Password=<your-sql-password>;Encrypt=True;TrustServerCertificate=True"
$rabbit = "amqp://<rabbit-user>:<rabbit-password>@localhost:5672/"

dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project Solution\src\AiRecipeGeneration.Api
dotnet user-secrets set "Messaging:ConnectionString" $rabbit --project Solution\src\AiRecipeGeneration.Api
dotnet user-secrets set "ConnectionStrings:ChopChop" $connection --project Solution\src\InteractiveReview.Api
dotnet user-secrets set "Messaging:ConnectionString" $rabbit --project Solution\src\InteractiveReview.Api
```

For AI generation, also configure the Azure OpenAI v1 endpoint, deployment
name, and API key only in the AI API's user secrets:

```powershell
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com" --project Solution\src\AiRecipeGeneration.Api
dotnet user-secrets set "AzureOpenAI:Deployment" "<deployment-name>" --project Solution\src\AiRecipeGeneration.Api
dotnet user-secrets set "AzureOpenAI:ApiKey" "<secret>" --project Solution\src\AiRecipeGeneration.Api
```

The adapter appends `/openai/v1/chat/completions` to the endpoint. Never place
an API key, SQL password, broker password, or OAuth client secret in source
control, `.env.example`, browser configuration, or test data.

## 3. Apply each service's migrations

Each service owns its schema and migration history in the shared `ChopChop`
database. Run each migration from the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet ef database update --project Solution\src\AiRecipeGeneration.Data --startup-project Solution\src\AiRecipeGeneration.Api --context AiRecipeDbContext
dotnet ef database update --project Solution\src\InteractiveReview.Data --startup-project Solution\src\InteractiveReview.Api --context InteractiveReviewDbContext
```

The AI service uses schema `ai_recipe`; the review service uses
`interactive_review`. Do not modify the other service's tables.

## 4. Start APIs and client

Open separate PowerShell terminals from the repository root:

```powershell
dotnet run --project Solution\src\AiRecipeGeneration.Api --launch-profile AiRecipeGeneration.Api
```

```powershell
dotnet run --project Solution\src\InteractiveReview.Api --launch-profile InteractiveReview.Api
```

In another terminal:

```powershell
Copy-Item Solution\src\ChopChop.Client\.env.example Solution\src\ChopChop.Client\.env.local
Set-Location Solution\src\ChopChop.Client
npm ci
npm run dev
```

The browser client defaults to API ports `5081` and `5082`, the local Keycloak
realm URL, and the public client ID. `.env.local` may override those public
URLs; it must never contain a client secret.

Available endpoints:

- AI Recipe API: `http://localhost:5081`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Interactive Review API: `http://localhost:5082`; OpenAPI document at
  `/openapi/v1.json`; health at `/health`.
- Keycloak: `http://localhost:8080/realms/chopchop`.

The APIs validate signed bearer tokens against Keycloak issuer and audience.
The moderation policy expects the realm role as a `role` access-token claim.
The imported realm configures that mapping. Local HTTP and local-only secrets
are development conveniences; production must use HTTPS and an appropriately
secured secret store.

## 5. Validate changes

```powershell
dotnet restore Solution\ChopChop.sln
dotnet build Solution\ChopChop.sln -c Release --no-restore
dotnet test Solution\ChopChop.sln -c Release --no-build
dotnet format Solution\ChopChop.sln --verify-no-changes
dotnet list Solution\ChopChop.sln package --vulnerable --include-transitive

Set-Location Solution\src\ChopChop.Client
npm ci
npm run build
npm test
npm run lint
```

API integration tests validate JWT behavior using a framework bearer handler
and test signing key. Database-backed integration tests require the local
SQL Server and RabbitMQ services.
