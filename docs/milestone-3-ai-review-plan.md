# Milestone 3 Implementation Plan: AI Recipe and Interactive Review

## Status

**Gate 1 — approved by the assigned developer on 2026-10-29.**

This plan covers Xavier's assigned AI Recipe Generation and Interactive Review
services. It is not approval to implement until the developer reviews and
approves the plan. Pull-request peer approval and merge remain human
responsibilities.

## Research (R)

### Source requirements

- Milestone 3 assignment PDF: layered services, a connected client, OAuth/OIDC
  bearer-token validation, least-privilege policies, tracked work, custom
  layer-specific agents and skills, R-PIV, automated validation, and human
  review gates.
- GitHub issue [#18](https://github.com/PolaricksX/ChopChop/issues/18): AI
  Recipe Generation service.
- GitHub issue [#19](https://github.com/PolaricksX/ChopChop/issues/19):
  Interactive Review service.
- Repository guidance: `copilot-instructions.md`, `.github/skills/r-piv-skill.md`,
  and the API, data-access, frontend, and general R-PIV agent instructions.
- AI starter pack references: `plan-feature`, `execute`, `validate`,
  `code-review`, `prime-backend`, `prime-frontend`, and the architecture,
  backend API, and frontend best-practice references.

### Current repository state

- Current working branch:
  `feature/milestone-3-layered-service-development-xavier`, based on `main`.
- The worktree was clean before this plan.
- The repository contains mock types/interfaces and Milestone 2 guidance, but
  no solution, service projects, API, database context, test projects, React
  client, migrations, or established package versions.
- The current repository instructions say Milestone 1 is active and direct
  contributors not to build later milestones. The requested Milestone 3
  authorization means this baseline must be changed before implementation.
- There is no `docs/milestones.md`, ADR directory, test configuration, or
  existing Milestone 3 architecture to extend.
- Milestone 2's root documentation describes the starter pack and R-PIV as
  process assets, not working services.

### Scope

**In scope**

- Two separately runnable services, each with `.Domain`, `.Data`, and `.Api`
  projects:
  - AI Recipe Generation: owned requests, status, drafts, and substitutions.
  - Interactive Review: owned ratings, reviews, suggestions, photos, and
    moderation state.
- A React browser client that calls only public HTTP DTO contracts and supports
  both assigned service workflows.
- OAuth/OIDC JWT validation and named least-privilege policies in both APIs.
- SQL Server persistence, service-owned tables/migrations, automated tests,
  OpenAPI, service documentation, and R-PIV evidence.
- Copilot custom agents and skills for the layers actually used, including a
  read-only code review agent/skill.
- Local configuration and startup documentation that uses safe placeholders,
  not credentials.

**Out of scope**

- Implementing the other team's services or changing their data ownership.
- Direct writes to Recipe Management data, direct cross-service database
  access, a new gateway, or unapproved service-to-service dependencies.
- Creating/publishing a team-wide work board, milestone PR, peer signoff, or
  human validation-gate record on behalf of teammates.
- Committing, pushing, merging, or claiming human approval without explicit
  authorization and review.
- Treating generated content as authoritative allergy, medical, nutrition, or
  food-safety advice.

### Repository constraints and risks

1. **Milestone conflict:** the current-milestone line, root structure statement,
   commands, and milestone documents must be aligned with the actual new
   `Solution/` structure. Do not silently leave Milestone 1 active.
2. **No implementation exists:** do not retrofit the mock classes as production
   models or claim existing tests/ADRs/configuration are reusable.
3. **Provider decision:** no AI provider or credential has been selected in
   repository configuration. Provider integration must use an adapter behind
   the AI-provider interface, read secrets only from environment/user-secrets,
   expose explicit failure when unconfigured, and use deterministic test fakes.
   The concrete provider/HTTP contract is an open plan decision.
4. **Durable async processing:** AI generation must return `202` and persist a
   pollable status. The repository's platform decision specifies RabbitMQ
   behind `IEventBus` from Milestone 2, but no implementation is present. The
   plan must either implement/reuse that approved boundary with durable request
   state and outbox semantics, or be revised before implementation; do not use
   an in-memory queue as a success-shaped durable workflow.
5. **Identity configuration:** the repository specifies Keycloak locally and
   configurable authority/audience. The PDF requires real framework JWT
   validation, not decoded or permissive tokens.
6. **Photos:** the Review service owns photo metadata/storage boundaries.
   Storage must use a file-storage interface and safe local paths outside
   executable content; validate size/content and authorize each read. A scanner
   is used if available; absence is documented.
7. **PDF submission system:** the PDF describes Azure DevOps PRs, but this
   repository is hosted on GitHub and issues #18/#19 already live there. Use
   GitHub for this repository; do not create an Azure DevOps project.
8. **Human gates:** this plan requires approval before code. The eventual code
   still requires a human diff/test review and another team member's PR review.

## Plan (P)

### Proposed solution

Create a .NET 10 solution under `Solution/` with two independent service
verticals and a React client. Keep Domain free of framework/persistence
references, keep EF Core and provider/storage adapters in Data, and keep API
endpoints thin. Use shared SQL Server infrastructure only; each service owns
its schema/tables, migrations, and application workflows.

AI generation will be persisted and asynchronous: an authenticated,
idempotent request returns `202 Accepted` and a status URL; the user polls the
owner-scoped request. A Data-layer AI-provider adapter calls the configured
provider with bounded timeouts and cancellation. Provider output is treated as
untrusted data, parsed into a strict bounded contract, validated, and stored
only as an explicitly labelled draft. The user must confirm before the draft
can leave the generated-draft workflow. Provider outages and invalid output
surface an explicit failed/rejected status, never a fabricated successful
recipe.

Review supports a paginated review feed, one review per user/recipe, safe
updates/deletes by the owner, and moderator-only moderation. The review owner
comes from validated claims. Enforce uniqueness in the database, use
optimistic concurrency for updates, publish the approved rating/moderation
events through the platform event boundary, and persist the event with the
state change where required by the outbox pattern. A missing/unpublished recipe
must not break the Review service.

### Architecture decisions to record before service code

Create concise ADRs for the new solution's adopted decisions before
implementation, following the repository rule that decisions be recorded
before the milestone starts:

- The two-service solution layout, ownership boundaries, shared SQL Server
  instance, and independent migrations.
- Keycloak/OIDC JWT validation, authority/audience configuration, local PKCE
  client, and named scopes/policies.
- RabbitMQ/IEventBus integration for durable async generation and review
  events, including outbox/inbox, retries, and dead-letter behavior. Do not
  introduce a competing transport.
- AI-provider adapter, provider request/response contract, data minimization,
  retention, timeout, retry, circuit-breaker, and cost/rate limits. Select the
  provider using a documented environment configuration; no credentials in
  source or test data.
- Review photo storage adapter, local development storage boundary, content
  validation, size limits, and authorization.
- React client authentication and API-consumption boundaries.

If an ADR cannot be supported by this repository or dependencies, stop and
revise the plan before adding an alternative.

### Proposed solution structure

```text
Solution/
|-- Solution.sln
|-- Directory.Build.props
|-- Directory.Packages.props
|-- .editorconfig
|-- src/
|   |-- AiRecipeGeneration.Domain/
|   |-- AiRecipeGeneration.Data/
|   |-- AiRecipeGeneration.Api/
|   |-- InteractiveReview.Domain/
|   |-- InteractiveReview.Data/
|   |-- InteractiveReview.Api/
|   `-- ChopChop.Client/
|-- tests/
|   |-- AiRecipeGeneration.UnitTests/
|   |-- AiRecipeGeneration.IntegrationTests/
|   |-- InteractiveReview.UnitTests/
|   `-- InteractiveReview.IntegrationTests/
`-- docs/
    |-- adr/
    |-- contracts/
    `-- milestone-3/
```

Keep the existing root `docs/` diagrams and Milestone 2 evidence; do not move or
delete them as part of this task. Update the root README and root instructions
to point to `Solution/` once the approved structure exists.

### Work breakdown

#### Work item A — solution, common security, and two service foundations

1. Align `copilot-instructions.md` to Milestone 3 and the actual repository
   structure; add `docs/milestones.md` with completed/prior and current scope.
2. Add central build/package configuration, solution, and the six service
   projects with correct references. Pin only researched, supported compatible
   package versions centrally; enable nullable, warnings-as-errors, and full
   NuGet audit.
3. Add service-specific `DbContext`s, scoped repositories/Unit of Work, SQL
   Server configuration, owned schemas, migrations, and health checks.
4. Add framework JWT bearer validation in both APIs using environment
   `Auth:Authority` / `Auth:Audience`, named read/write/moderator scopes,
   authenticated fallback policy, public `/health`, correlation middleware,
   problem details, request logging, and secure headers.
5. Add local compose configuration for SQL Server, RabbitMQ, and Keycloak with
   safe non-production placeholders only. Document ports and startup.

#### Work item B — AI Recipe Generation service

1. Add domain types for generation request, owner, idempotency, status,
   validated draft, substitutions, and bounded recipe data.
2. Add API DTOs/validators and owner/scoped endpoints:
   - POST generation request: require `Idempotency-Key`, request size limits,
     validated caller; return `202` and `Location`/status URL.
   - GET status/result: owner-scoped, bounded response, explicit queued/running/
     completed/failed states.
   - POST explicit confirmation: owner-scoped and idempotent; never publish
     directly into Recipe Management-owned data.
3. Add a Data-layer AI-provider interface/adapter. Require explicitly
   configured provider details; no real provider call in tests. Bound prompt
   and output size, cancellation, timeout, transient retries, circuit breaker,
   per-user/IP limits, and cost controls.
4. Use a durable queue/outbox workflow via the approved event boundary; persist
   request/status before returning `202`, handle duplicate/out-of-order work
   safely, and document retry/dead-letter behavior.
5. Parse and validate strict provider output (ingredient quantities/units,
   allergen declaration, equipment, steps, and nutrition estimates). Reject
   invalid output; redact sensitive prompt/profile information from logs.
6. Add domain/orchestration/provider/HTTP tests for idempotency, ownership,
   cancellation, unsafe/oversized output, timeouts, retries, provider outages,
   and confirmation state transitions.

#### Work item C — Interactive Review service

1. Add domain types for review, rating, suggestions, photo metadata, and
   moderation status, with rating/content invariants and UTC timestamps.
2. Add persistence constraints for one review per owner/recipe, bounded fields,
   concurrency, and service-owned migrations.
3. Add DTOs/validators and versioned endpoints for paginated list, create,
   owner update/delete, photo metadata/upload/read (if contract includes it),
   and moderator state transitions. Treat each endpoint as explicitly public or
   protected; use a distinct moderator policy. Ignore server-owned body fields.
4. Add file-storage interface/adapter with generated safe file names, content
   type/size checks, non-executable storage, and authorization for each read.
   Use scanning if a scanner is available; document otherwise.
5. Publish `RecipeRated` and `ReviewModerationChanged` through the event
   boundary with correlation IDs and outbox guarantees. Do not read or write
   another service's tables.
6. Add unit and API integration tests for one-review uniqueness, update/delete
   ownership, moderator policy/state, pagination limits, upload failures,
   event/outbox behavior, missing recipes, and cancellation.

#### Work item D — React client

1. Create a typed React client with no server-side business rules or secrets.
   Authenticate using authorization-code flow with PKCE and use the APIs only
   through public HTTP DTOs.
2. Add AI request/status polling, request/error states, generated-content and
   nutrition-estimate labels, and explicit confirmation before save/publish.
   Never display drafts as verified allergy/medical/food-safety guidance.
3. Add paginated recipe-review display, create/edit own review, moderation
   status, and moderator controls gated by token permissions. Accessible forms
   and dynamic status announcements are required.
4. Add focused component/client tests for loading, success, empty state,
   validation, authorization errors, polling failure, and confirmation.

#### Work item E — custom agents, skills, evidence, and validation

1. Add Copilot custom agents under `.github/agents/` for Domain, Data/API,
   Frontend, Testing, and read-only Code Review. Each agent must define scope,
   allowed paths/tools, boundaries, and expected output; the review agent must
   not edit code.
2. Add Copilot skill definitions under `.github/skills/` for R-PIV execution,
   service implementation, validation, and code review, reusing relevant AI
   starter pack workflow guidance without copying irrelevant Claude-only
   integrations.
3. Add endpoint/event contracts, ADRs, local setup, security/threat-model
   notes, test evidence, failure behavior, data retention, and a 2–3 page
   Milestone 3 architecture/reflection document. Update README links.
4. Run automated build, unit/integration tests, format, package-vulnerability
   checks, and client checks against real project manifests.
5. Perform Gate 2 locally with a line-by-line diff and test-output review.
   Gate 3 requires a GitHub pull request, read-only code-review report, and
   approval by another team member. No code change can truthfully satisfy the
   peer approval requirement without that reviewer.

### Suggested skills and agents

Use the existing R-PIV skill and relevant AI starter pack guidance:

- `prime-backend` and `prime-frontend` for research.
- `plan-feature` for this plan (this document).
- The repository's R-PIV skill and layer-specific agents for implementation,
  sequentially from Domain to Data to API to Client.
- `execute` for approved implementation tasks.
- `validate` for the actual .NET and npm checks.
- `code-review` plus a repository read-only review agent for Gate 3 input.

Do not invoke unrelated starter-pack skills (e.g. Python-only, PRD creation,
Jira/Confluence publishing, or Claude-only MCP workflows).

### Human approval questions

The following decisions must be settled at Gate 1; they are not established by
the current code:

1. Which AI provider/API should be implemented? If no provider/account is
   approved, implement the safe interface and explicit unconfigured failure,
   but do not claim live generation is operational.
2. Are review photo uploads required in this assigned slice, and what maximum
   upload size/types should the contract allow? Proposed default is JPEG/PNG,
   maximum 5 MiB per photo, subject to approval.
3. Approve the proposed .NET 10 / SQL Server / RabbitMQ / Keycloak / React stack
   and local Docker Compose dependency setup (all match current guidance).
4. **Approved:** use Azure OpenAI, with secrets supplied only from the local
   environment/user-secrets; no secret is supplied in chat or committed.
5. **Approved:** defer photo uploads in this slice while retaining a documented
   file-storage boundary for later.
6. **Approved:** use API ports 5081 (AI) and 5082 (Review).

## Implementation sequence after Gate 1 approval

1. Record approved ADRs and baseline updates.
2. Build Domain projects and deterministic unit tests.
3. Build Data projects, persistence/outbox, and migrations.
4. Build API authorization/contracts and service orchestration.
5. Build client flows and client tests.
6. Add integration tests and cross-service-safe event checks.
7. Add agent/skill configurations and final documentation.
8. Run validation; inspect full diff; request peer review before merging.

## Validation plan

Run from `Solution/` after the projects and manifests exist:

```powershell
dotnet restore Solution.sln
dotnet build Solution.sln -c Release --no-restore
dotnet test Solution.sln -c Release --no-build --collect:"XPlat Code Coverage"
dotnet format Solution.sln --verify-no-changes
dotnet list Solution.sln package --vulnerable --include-transitive
npm ci
npm run build
npm test -- --run
```

Use the real scripts present in the approved client manifest. Integration tests
must use SQL Server migrations and the framework JWT bearer handler with test
signing credentials; do not replace authentication with a permissive fake. Run
the local compose stack for a smoke test only after reviewing its configuration.

## Acceptance and non-claims

The assigned code is ready for human review only when both services build and
their automated tests pass, security and ownership assertions are covered, the
client contract works, and docs/agents/skills match the final structure.

This plan cannot claim the full team milestone is complete by itself: the
team-wide work board, other members' services, milestone-level PR, human Gate 1
approval, Gate 3 peer signoff, and instructor submission remain outside the
assigned code work.
