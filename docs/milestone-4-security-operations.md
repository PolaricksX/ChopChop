# Milestone 4 Security Operations

Owner: Xavier (repository administration, CI/CD, and secret handling).

## Work items

### 1. Security validation pipeline

The `security-validation` GitHub Actions workflow runs on pull requests to
`main`, pushes to `main`, and the Milestone 4 feature branch.

Required checks:

- .NET restore and Release build with warnings treated as errors
- `dotnet format` verification
- .NET unit tests
- NuGet vulnerability audit, including transitive dependencies
- React dependency install, production build, tests, lint, and production
  dependency audit
- SQL-backed .NET integration and Pantry tests with an ephemeral SQL Server
  container

The SQL-backed job does not expose credentials to forks. Dependabot monitors
NuGet, npm, and GitHub Actions dependencies weekly.

### 2. Secret handling

Repository and deployment secrets must be stored outside Git. Use GitHub
Actions secrets for CI, user secrets for local .NET development, and the
approved deployment secret store for shared environments.

Required repository Actions secret:

| Secret | Purpose | Requirements |
|---|---|---|
| `CI_SQLSERVER_PASSWORD` | SA password for the ephemeral SQL Server Actions service | Strong unique random value; never reused for a developer, staging, or production database |

The value is not present in the workflow, examples, logs, or source. Rotate it
when a repository role changes, a leak is suspected, or the credential expires.
After rotation, rerun the `security-validation` workflow before merging.

The local `.env` file is ignored by Git. Copy `.env.example` and replace every
placeholder locally. Do not store `.env`, browser client secrets, identity
provider signing keys, Azure OpenAI keys, SQL passwords, RabbitMQ passwords, or
production connection strings in the repository.

Use these mechanisms:

| Setting | Local development | CI/deployment |
|---|---|---|
| SQL Server connection string | .NET user secret (`ConnectionStrings:ChopChop` or service-specific key) and `.env` only for Compose | Environment-specific secret store |
| RabbitMQ connection/credentials | .NET user secrets and ignored `.env` | Environment-specific secret store |
| OIDC authority/audience | User secrets; audience and authority identify resources and are not credentials | Protected environment configuration |
| Azure OpenAI endpoint/deployment/key | User secrets; only the key is sensitive | Environment-specific secret store |
| Browser `VITE_*` values | Ignored `.env.local` | Build-time public configuration only; never client secrets |
| CI SQL password | Not required locally | `CI_SQLSERVER_PASSWORD` Actions secret |

## Repository-admin setup

1. In GitHub Settings, create the `CI_SQLSERVER_PASSWORD` Actions secret.
2. Enable private-repository secret scanning and push protection where
   available. Review and resolve alerts; do not bypass push protection for
   convenience.
3. Require the `Build, test, format, and audit` and `SQL-backed integration
   tests` checks before merging to `main`.
4. Require at least one non-author review and dismiss stale approvals when new
   commits are pushed.
5. Restrict workflow write permissions to the minimum; this validation
   workflow requests read-only repository contents.
6. Revoke and rotate an exposed value at its source. Removing it from Git
   history alone is not sufficient.
7. Never place the CI password in a `VITE_*` setting. Vite values are embedded
   in browser assets.

## Reproducible verification

The workflow uses the same commands documented in `docs/local-development.md`.
To reproduce the SQL-backed job locally, start SQL Server, set
`TEST_SQLSERVER_CONNECTION` in the shell without printing it, then run:

```powershell
dotnet restore Solution.sln
dotnet build Solution.sln -c Release --no-restore
dotnet test Solution.sln -c Release --no-build
dotnet format Solution.sln --verify-no-changes
dotnet list Solution.sln package --vulnerable --include-transitive

Set-Location src/ChopChop.Client
npm ci
npm run build
npm test
npm run lint
npm audit --omit=dev
```

## Evidence and ownership

Attach the workflow run URL, audit output, required-check status, Dependabot
status, and any secret-rotation record to the Milestone 4 PR. The PR must name
Xavier as owner for both work items and identify another team member as
reviewer.
