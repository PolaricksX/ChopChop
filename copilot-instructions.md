# Repository Instructions

## Mission

Build a secure, testable cookbook platform that helps users discover, plan, and
prepare recipes that fit their dietary needs, pantry, equipment, time, and
skill. The first release prioritizes trustworthy recipe discovery and
personalization. AI-generated content is optional, labeled as generated,
validated, and never treated as authoritative medical, allergy, or food-safety
advice.

## Working Agreement

- These rules override convenience. When a request conflicts with a rule, name
  the rule and propose a compliant alternative.
- Make the smallest change that meets the request. Follow the existing patterns
  of the service you touch.
- Never invent packages, APIs, or versions. State assumptions when a request is
  ambiguous.
- Ask before creating a service that Service Ownership does not list, moving
  ownership, or adding a cross-service dependency.
- Every change ships with tests and documentation updates (see Definition of
  Done).

## Current Milestone

**Current milestone: 1 (SOA Foundation).** Scope and deliverables for every
milestone live in `docs/milestones.md`. Build what the current milestone
requires and nothing ahead of it. Validation, authorization structure, error
handling, and logging apply in every milestone.

## Technical Baseline

- Target .NET 10 with nullable reference types and implicit usings. Use ASP.NET
  Core, EF Core SQL Server, and xUnit. Configure EF Core with `UseSqlServer` and
  the `Microsoft.EntityFrameworkCore.SqlServer` provider; keep its version
  centrally managed with the other NuGet packages. SQL Server Management
  Studio (SSMS) is an optional client for administering SQL Server; the
  application connects to the SQL Server instance, not to SSMS.
- Centralize build settings in `Directory.Build.props`, `Directory.Packages.props`,
  and `.editorconfig`. Set `TreatWarningsAsErrors` to true. Suppress a warning
  only with an inline justification.
- Use async I/O and pass `CancellationToken` through every layer.
- Use Serilog for structured application and request logging with message
  templates; use `ILogger<T>` through the Serilog provider. Configure Serilog
  through the host and centrally manage the `Serilog.AspNetCore` package
  version. Enrich request logs with the correlation ID, and enable built-in
  OpenAPI metadata and RFC 9457 problem details that carry the same ID. Enable
  Swagger UI in development only.
- Expose health checks at `/health` anonymously. They check only the service's
  own dependencies (its database) and never other services, so a service stays
  healthy when a neighbor is down.
- Carry one correlation ID per request in the `X-Correlation-Id` header. Shared
  middleware reads or generates it and adds it to the logging scope and problem
  details. An `HttpClient` handler forwards it on outgoing calls, and event
  envelopes copy it.
- Use constructor injection and register services in `Program.cs`. Put
  interfaces at external boundaries (HTTP clients, event bus, AI provider,
  file storage). Do not inject `IServiceProvider` for general service
  resolution; the scoped `RepositoryFactory` may use it only to resolve
  registered repository overrides and custom repositories. Register
  `DbContext`, repository factory, repository provider, and Unit of Work as
  scoped services. Use singletons only for thread-safe services.
- Use one API style per service (Minimal APIs or controllers). Keep controllers
  and endpoints thin. Put pure invariants in Domain; put use-case orchestration
  in Api/Services. Application services depend on `IUnitOfWork`, not EF Core.
- Add a dependency only when the platform or an existing library cannot do the
  job. Review its license and known vulnerabilities first. Set
  `NuGetAuditMode` to `all` so vulnerable direct and transitive packages raise
  build warnings, which fail the build under warnings-as-errors.

## Commands

Run from the repository root. Start the SQL Server dependency first; from
Milestone 2, Docker Compose also starts the message broker. SSMS may be used to
inspect or administer the SQL Server instance.

```bash
docker compose up -d
dotnet restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --collect:"XPlat Code Coverage"
dotnet format --verify-no-changes
dotnet list package --vulnerable --include-transitive
```

Create a migration with
`dotnet ef migrations add <Name> --project src/<ServiceName>.Data --output-dir Migrations`.

## Platform Decisions

Use these defaults. Record each decision in `docs/adr/` (one file per decision)
before its milestone starts, and propose a change through an ADR instead of
introducing an alternative.

- **Local run:** Docker Compose runs SQL Server and, from Milestone 2, the
  message broker. Each service connects using its configured SQL Server
  connection string and starts with `dotnet run` on its own port. Use SSMS as
  an optional management client.
- **Event transport (from Milestone 2):** RabbitMQ behind `IEventBus`, with an
  in-memory fake for unit tests.
- **Identity provider (from Milestone 3):** Keycloak locally, configured only
  through `Auth:Authority` and `Auth:Audience` so another OIDC provider can
  replace it.

## Structure

- The repository root is `Solution/` and contains `Solution.sln`, `src/`,
  `tests/`, and `docs/`. `src/` holds service projects and the client;
  `tests/` holds service tests; `docs/` holds ADRs, contracts, diagrams, the
  threat model, setup steps, and milestone evidence.
- Each service uses this project and folder layout:

  ```text
  Solution/
  |-- Solution.sln
  |-- src/
  |   |-- ServiceName.Domain/
  |   |   |-- Entities/
  |   |   |-- Enums/
  |   |   |-- Exceptions/
  |   |   |-- Services/              (pure domain validation and business rules)
  |   |-- ServiceName.Data/
  |   |   |-- Context/
  |   |   |   |-- ServiceDbContext.cs
  |   |   |-- Configurations/        (EF Core entity type configurations)
  |   |   |-- Repositories/          (repository implementations)
  |   |   |-- Migrations/
  |   |-- ServiceName.Api/
  |   |   |-- Controllers/           (or Minimal API route endpoints)
  |   |   |-- Contracts/             (request and response DTOs)
  |   |   |-- Services/              (application orchestration services)
  |   |   |-- Program.cs
  |   |   |-- appsettings.json
  |   |-- ServiceName.Client/        (React or ASP.NET Core client)
  |-- tests/
      |-- ServiceName.UnitTests/
      |-- ServiceName.IntegrationTests/
  ```

- Dependencies flow from Api to Data and Domain, and from Data to Domain.
  Api is the composition root and registers Data implementations. Keep domain
  rules in Domain; keep EF Core, persistence, and provider adapters in Data.
  Domain must not reference Data, EF Core, or `Microsoft.AspNetCore` types. An
  architecture test in the unit test project enforces these boundaries
  (`NetArchTest.Rules` is an acceptable test-only dependency).
- Put `IRepository<TAggregate>` and `IUnitOfWork` abstractions in Data, alongside
  their EF Core implementations. This keeps EF types out of Api/Services while
  preserving the stated Api-to-Data dependency direction.
- Each service owns its projects and migrations. The Client project consumes
  APIs and contains no server-side business rules. A gateway is out of scope
  unless an ADR adds one.
- All services use one shared SQL Server database and connection string. Each
  service owns its tables, migrations, and persistence models, and services may
  join owned tables in application queries when a workflow needs data from more
  than one service. Do not expose EF entities across service boundaries.
- Keep domain ownership explicit even though the database is shared. A shared
  library may hold only stable cross-cutting primitives (event envelope,
  correlation ID, result types), never business rules.
- Consumers use service APIs through the Client project or other API clients.
  A gateway is out of scope unless an ADR adds one.

## Service Ownership

1. **Recipe Management:** recipes, ingredients, instructions, tags, servings,
   times, nutrition estimates, equipment requirements, validation, search, and
   filtering. Published recipe versions are immutable.
2. **User and Profile:** identity provider integration, the profile record,
   settings, allergies, dietary preferences, cooking skill, locale, nutrition
   goals.
3. **Cookbook and Favorites:** saved recipes, folders, favorites, recently
   viewed, cooking history. Stores recipe IDs and versions, not copies.
4. **Interactive Review:** ratings, reviews, suggestions, photos, moderation
   state. One rating/review per user per recipe unless a documented rule allows
   updates.
5. **AI Recipe Generation:** generation requests, provider adapters, prompts,
   draft recipes, substitutions, status. Output stays a draft until the user
   confirms.
6. **Meal Planner:** meal plans, dates, slots, servings, planning preferences.
7. **User Pantry:** ingredients, quantities, units, expiration dates,
   consumption history, user-requested pantry suggestions.
8. **User Equipment:** appliances, cookware, availability, capacity notes,
   equipment filtering preferences.

### Boundary rules

- Respect ownership when reading or changing shared data. Access another
  service's owned tables through a versioned application contract or an
  explicitly documented read-only join; never modify another service's data
  outside its owning application workflow or reference its code directly.
- Recipe Management owns equipment requirements and all recipe filtering.
  User Equipment owns availability only. The caller (the client or an
  orchestrating service such as Meal Planner) fetches the user's equipment and
  passes it as a filter to Recipe search. Recipe search works with User
  Equipment offline.
- User and Profile owns nutrition goals. Meal Planner reads them and never
  calculates or stores them as truth.
- The identity provider owns credentials and login. User and Profile owns the
  profile record keyed by the token `sub` claim, creates it on first
  authenticated use, and publishes `UserDeleted` after removing the identity
  provider account.
- Pantry data reaches AI Recipe Generation only when the user requests it, and
  only the minimum fields. Pantry never pushes inventory to the AI service.
- Cookbook and Review tolerate recipes that are missing or unpublished.
- Every service-to-service call sets a timeout (default 5 seconds), retries only
  transient failures on idempotent calls, and returns a documented fallback
  when the dependency fails.

## API and Contract Rules

- Use REST with plural resource nouns under `/api/v{n}/`. Return correct
  status codes: 201 with `Location`, 202, 204, 400 with problem details, 401,
  403, 404, 409, 413, 429. Keep PUT and DELETE idempotent.
- Define request and response DTOs (records). Never expose EF entities. Never
  bind server-owned fields (`Id`, owner ID, moderation state) from request
  bodies. A PUT route may carry a client-generated ID, and the service returns
  404 when that ID belongs to another user.
- Validate all input at the API boundary. Enforce business invariants in
  Api/Services and Domain.
- Paginate every collection endpoint (default 20, maximum 100). Bound filters,
  allowlist sort fields, use `AsNoTracking()` for reads, and avoid N+1 queries.
- Prefer additive changes. Add a new version for breaking changes and document
  compatibility in `docs/`.
- Use GUID identifiers, UTC `DateTimeOffset` timestamps, explicit units, and
  `decimal` for quantities and nutrition values.
- Require an `Idempotency-Key` on POST commands that create resources or
  trigger AI generation. A PUT with a client-generated ID is idempotent by
  design and needs no key. Return the stored result for a repeated key and
  reject the same key with a different payload using 409.
- Use optimistic concurrency on updatable resources and return 409 on conflict.

## Data

- Add a descriptive migration for every model change. Never edit an applied
  migration. Never call `EnsureCreated()`. Review each generated migration and
  script destructive changes before deployment.
- `DbSet<T>` already provides repository behavior and `DbContext` already
  provides Unit of Work behavior. Use the abstractions below to hide EF Core
  from Api/Services and simplify testing, not as redundant wrappers with no
  boundary benefit.
- A generic `IRepository<T>` loads and stores one aggregate type. It never
  calls `SaveChangesAsync`; repositories stage changes only. Keep query APIs
  specific enough not to expose EF Core types such as `IQueryable` to
  Api/Services.
- `IRepositoryFactory` first resolves a registered `IRepository<T>` for the
  requested type, then falls back to `EfRepository<T>` using the scoped
  `DbContext`. It also resolves registered custom repository types. This is
  the only place that uses `IServiceProvider` for repository resolution.
- `IRepositoryProvider` uses the factory and caches each repository instance
  by requested type for the lifetime of the Unit of Work. Its cache need not be
  thread-safe because the provider is scoped to one request. The factory,
  provider, Unit of Work, repositories, and `DbContext` must all share the same
  request scope and `DbContext` instance.
- `IUnitOfWork` delegates generic and custom repository access to the
  provider, owns the scoped `DbContext`, and exposes one `SaveChangesAsync`
  operation to persist all staged changes together. Do not add a Unit of Work
  property for every entity. Controllers and endpoints stay thin; application
  services in Api/Services depend on `IUnitOfWork`, perform a use case, and
  save once after its changes are ready. Keep pure domain invariants in Domain.
- Register specific repositories against both their custom interface and
  `IRepository<T>`. Unregistered aggregate types use the generic EF
  implementation. In unit tests, replace `IRepositoryFactory` with a fake that
  returns in-memory repositories.

Factory and provider contracts and implementations:

```csharp
public interface IRepositoryFactory
{
  IRepository<T> Create<T>() where T : class;
  TRepository CreateCustom<TRepository>() where TRepository : class;
}

public sealed class RepositoryFactory(
  IServiceProvider services,
  AppDbContext context) : IRepositoryFactory
{
  public IRepository<T> Create<T>() where T : class =>
    services.GetService<IRepository<T>>() ?? new EfRepository<T>(context);

  public TRepository CreateCustom<TRepository>() where TRepository : class =>
    services.GetRequiredService<TRepository>();
}

public interface IRepositoryProvider
{
  IRepository<T> GetRepository<T>() where T : class;
  TRepository GetCustomRepository<TRepository>() where TRepository : class;
}

public sealed class RepositoryProvider(IRepositoryFactory factory)
  : IRepositoryProvider
{
  private readonly Dictionary<Type, object> cache = [];

  public IRepository<T> GetRepository<T>() where T : class =>
    GetOrCreate(() => factory.Create<T>());

  public TRepository GetCustomRepository<TRepository>() where TRepository : class =>
    GetOrCreate(() => factory.CreateCustom<TRepository>());

  private TService GetOrCreate<TService>(Func<TService> create)
    where TService : class
  {
    if (!cache.TryGetValue(typeof(TService), out var repository))
      cache[typeof(TService)] = repository = create();

    return (TService)repository;
  }
}
```

The Unit of Work exposes repositories without per-entity properties:

```csharp
public interface IUnitOfWork
{
  IRepository<T> Repository<T>() where T : class;
  TRepository CustomRepository<TRepository>() where TRepository : class;
  Task<int> SaveChangesAsync(CancellationToken token = default);
}

public sealed class UnitOfWork(
  AppDbContext context,
  IRepositoryProvider provider) : IUnitOfWork
{
  public IRepository<T> Repository<T>() where T : class =>
    provider.GetRepository<T>();

  public TRepository CustomRepository<TRepository>() where TRepository : class =>
    provider.GetCustomRepository<TRepository>();

  public Task<int> SaveChangesAsync(CancellationToken token = default) =>
    context.SaveChangesAsync(token);
}
```

Register the factory/provider/Unit of Work and map specific repositories to
both interfaces in `Program.cs`:

```csharp
builder.Services.AddScoped<IRepositoryFactory, RepositoryFactory>();
builder.Services.AddScoped<IRepositoryProvider, RepositoryProvider>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IRepository<Order>>(
  services => services.GetRequiredService<IOrderRepository>());
```

- Enforce keys, uniqueness, and foreign keys in the database. Use transactions
  Application services use the custom repository for specialized queries and
  the generic repository otherwise, then save the complete use case once:

  ```csharp
  public sealed class OrderService(IUnitOfWork unitOfWork)
  {
    public Task<Order?> GetWithItemsAsync(int id, CancellationToken token) =>
      unitOfWork.CustomRepository<IOrderRepository>()
        .GetWithItemsAsync(id, token);

    public async Task AddProductAsync(Product product, CancellationToken token)
    {
      await unitOfWork.Repository<Product>().AddAsync(product, token);
      await unitOfWork.SaveChangesAsync(token);
    }
  }
  ```

  - Enforce keys, uniqueness, and foreign keys in the database. Use transactions
    within one service only. Cross-service consistency uses REST or events.

## Event-Driven Integration

- Publish through an `IEventBus` abstraction. Keep transport adapters in
  Data.
- Publish facts, named in the past tense: `RecipePublished`, `RecipeUpdated`,
  `RecipeUnpublished`, `RecipeRated`, `ReviewModerationChanged`,
  `MealPlanChanged`, `PantryItemExpiring`, `UserProfileUpdated`,
  `UserDeleted`, `EquipmentUpdated`.
- On `UserDeleted` (payload: user ID only), every service that stores personal
  data erases or anonymizes it and records the outcome. The handler is
  idempotent.
- Every event carries an event ID, event type, schema version, occurred-at UTC
  timestamp, source service, correlation ID, and a minimal payload. Events are
  immutable and useful without the producer's database. Exclude secrets and
  unnecessary personal data.
- Assume at-least-once delivery. Consumers deduplicate with an inbox record
  saved in the same transaction as the side effect, ignore unknown fields, and
  handle out-of-order arrival.
- Retry at most three times with backoff. Never retry permanent validation
  failures. Send poison messages to a dead-letter destination.
- Use an outbox when a database change and its event must not separate.
- Document delivery guarantees, ordering assumptions, retry policy, and
  eventual-consistency behavior.

## Reference Examples

Follow these shapes for new endpoints and event handlers.

A thin endpoint: DTO in, validation at the boundary, caller from `ICurrentUser`,
named policy, and `CancellationToken`. The PUT with a client-generated ID makes
the create idempotent, so it needs no `Idempotency-Key`.

```csharp
app.MapPut("/api/v1/folders/{folderId:guid}", async (
    Guid folderId,
    SaveFolderRequest request,
    ICurrentUser user,
    FolderService folders,
    CancellationToken token) =>
{
    var errors = SaveFolderValidator.Validate(request);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    await folders.SaveAsync(user.Id, folderId, request.Name, token);
    return Results.NoContent();
})
.RequireAuthorization("cookbook.write");

public sealed record SaveFolderRequest(string Name);
```

An idempotent handler: the inbox record and side effect are staged through
repositories and committed together by the Unit of Work. `ProcessedEvent.Id` is
the primary key, so a concurrent duplicate fails on commit and goes back
through retry.

```csharp
public async Task HandleAsync(
    EventEnvelope<RecipeUnpublished> message,
    IUnitOfWork unitOfWork,
    CancellationToken token)
{
    var processedEvents = unitOfWork.Repository<ProcessedEvent>();
    if (await processedEvents.AnyAsync(x => x.Id == message.EventId, token))
        return;

    var savedRecipes = unitOfWork.Repository<SavedRecipe>();
    var matches = await savedRecipes.ListAsync(
        x => x.RecipeId == message.Payload.RecipeId, token);
    foreach (var savedRecipe in matches)
        savedRecipe.MarkUnavailable();

    processedEvents.Add(new ProcessedEvent { Id = message.EventId });
    await unitOfWork.SaveChangesAsync(token);
}
```

## Security

Apply these rules from Milestone 1. Until Milestone 3, the exceptions are real
JWT validation (see Authentication), rate limiting, CORS and header hardening,
and the threat model.

- **Authentication:** Require a bearer access token for every protected API
  endpoint. Validate JWT signature, issuer, audience, and lifetime using the
  configured OIDC authority and supported signing algorithms; do not write
  custom token validation or accept tokens from query strings. Reject missing,
  invalid, expired, or incorrectly scoped tokens. Run `UseAuthentication`
  before `UseAuthorization`. Apply a fallback policy that requires
  authentication, and mark public endpoints explicitly. Read the caller
  through `ICurrentUser`, implemented from authenticated claims. Until
  Milestone 3, a Development-only authentication handler signs in a configured
  test user holding every scope the policies require, so endpoints keep
  `RequireAuthorization` and their named policies unchanged. Startup fails if
  that handler is enabled outside Development. Mark `/health` and
  development-only OpenAPI endpoints `AllowAnonymous`.
- **Authorization:** Use named scope and claim policies (for example
  `recipes.write`). Separate administrator and moderator permissions from user
  permissions. Enforce authorization at the owning service.
- **Ownership:** Take the user ID from authenticated claims only. Filter every
  private query by owner, and return 404 for resources the caller does not own.
- **Service identities:** Use least-privilege scopes for service-to-service
  calls, database accounts, and AI provider credentials.
- **Secrets:** Store secrets in environment variables, user secrets, or a secret
  manager. Never commit keys, passwords, connection strings, tokens,
  certificates, or production data. Commit safe placeholders for local setup.
- **Cryptography:** Use the framework password hasher or delegate to the
  identity provider. Never write custom hashing, token signing, or encryption.
- **Input limits:** Cap request body size (1 MB default), string lengths, page
  sizes, filter complexity, upload size and type, and AI prompt and output
  size.
- **Injection and web attacks:** Use parameterized EF queries and safe output
  encoding. Never concatenate SQL, shell commands, HTML, or log templates from
  user input. Prevent IDOR, mass assignment, path traversal, SSRF, unsafe
  deserialization, and sensitive-data exposure.
- **Transport and browser:** Use HTTPS and HSTS outside local development,
  secure headers, and a CORS allowlist of explicit origins.
- **Rate limiting:** Limit reviews, uploads, and AI generation per user and IP
  and answer with 429. Configure login brute-force protection in the identity
  provider.
- **Errors and logs:** Return problem details with a correlation ID and no stack
  traces, SQL, or tokens. Use Serilog request logging and structured message
  templates; never build log messages by interpolating untrusted values. Never
  log passwords, bearer tokens, API keys, full allergy profiles, or sensitive
  prompts. Redact personal data and write structured audit records for
  moderation, permission changes, deletions, and AI generation requests.
- **Allergies and diets:** Treat them as safety-sensitive. Recipes state
  allergens explicitly, and missing allergen data counts as unknown. Filtering
  fails closed on missing or ambiguous data. API responses mark allergy
  exclusions separately from preference exclusions, so any client can show the
  difference.
- **Uploads:** Verify content type and size, generate safe file names, store
  files outside executable paths, scan when a scanner exists, and authorize
  every read.
- **Dependencies:** Scan in CI. Record each finding with severity, owner, and
  remediation date.

## AI Safety

- Keep provider calls behind an interface in Data. Never call a
  provider from controllers, endpoints, or domain logic.
- Send the minimum user data. Document retention. Never send secrets or
  unneeded profile data.
- Treat model output as untrusted. Parse it into a strict schema, enforce
  length limits, and validate ingredients, allergens, equipment, steps, and
  nutrition before display. Reject invalid output.
- Treat recipe text, pantry items, and reviews as data, never instructions.
  Model output cannot invoke tools, URLs, SQL, shell commands, or privileged
  operations without a server-side allowlist and authorization check.
- Run generation asynchronously: POST returns 202 with a status URL and
  clients poll it. Set a separate, documented provider timeout, since model
  calls outlast the 5 second service default.
- Set limited retries, circuit breakers, rate limits, cost limits, and a
  graceful fallback for provider failure.
- Label generated recipes and nutrition estimates. Require user confirmation
  before saving or publishing them.

## Testing and Quality Gates

- Write deterministic xUnit tests that use no production services or real user
  data.
- **Unit:** Domain services and rules, API orchestration services, validators,
  filtering, authorization decisions, and layer rules (architecture tests).
- Verify repository factory results share the Unit of Work's `DbContext`,
  repositories do not commit, and one Unit of Work commit persists changes
  across multiple repositories.
- **Integration:** `WebApplicationFactory<Program>` against a SQL Server test
  database with migrations applied. Cover persistence, API behavior,
  authentication, and ownership. Verify missing, malformed, expired, wrong
  issuer/audience, and insufficient-scope bearer tokens are rejected, while a
  valid token reaches authorized endpoints.
- Keep contract and selective end-to-end tests in the integration test project
  when those test types are needed. Cover registration, preference-based
  search, saving a recipe, meal planning, pantry and equipment filtering, and
  review submission.
- **Failure paths:** timeouts, retries, dependency outage with fallback,
  duplicate and out-of-order events, dead-lettering, cancellation, pagination
  limits, invalid and oversized input, 401, 403, IDOR, injection payloads,
  secret leakage in logs and responses, AI timeouts, invalid AI output.
- Collect coverage with `dotnet test --collect:"XPlat Code Coverage"`. Report
  it from Milestone 1 and gate the build at 80% line coverage (or the threshold
  documented in `docs/`) from Milestone 4. Coverage flags untested code and
  never replaces behavior tests.

## CI/CD

- Define pipelines in `azure-pipelines.yml`. Add an equivalent
  `.github/workflows/` file only if the repo is hosted on GitHub.
- CI restores, builds, checks formatting (`dotnet format --verify-no-changes`),
  tests with coverage, publishes results, scans dependencies (any finding
  fails the build), and publishes artifacts only after every check passes.
- CD builds once and promotes the same artifact through staging to production.
  Keep configuration and secrets outside the artifact (variable groups or Key
  Vault), require environment approvals, and use least-privilege service
  connections. Run a post-deployment health check. Document the rollback
  procedure and who decides to roll back.

## Documentation

For every service and cross-service feature, update `docs/` with:

- Purpose, ownership, boundaries, and data model summary.
- Endpoint or event contracts, including authentication and authorization.
- Local configuration, port, and startup commands, without secrets.
- Failure modes, retry and idempotency behavior, and observability signals.
- Tests run, evidence, trade-offs, and known limitations.

Keep a context diagram, ADRs, and a threat model (assets, trust boundaries,
threats, mitigations, residual risks) current.

## Definition of Done

- The change sits in the owning service and respects the boundary rules.
- Validation, authorization, error handling, logging, and cancellation are
  present.
- No secrets or sensitive data appear in source, logs, events, or tests.
- Database changes include reviewed migrations and keep existing contracts
  intact.
- New behavior has unit tests plus integration or contract tests where it
  crosses a boundary.
- `dotnet restore`, `dotnet build`, the format check, and the relevant tests
  pass, or the limitation is documented.
- OpenAPI, `docs/`, startup instructions, and milestone evidence are current.
