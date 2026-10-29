# Milestone 3 Evidence — AI Recipe and Interactive Review

## Research

- Implemented the approved scope in [the plan](./milestone-3-ai-review-plan.md)
  and assigned GitHub work items #18 and #19.
- Aligned application dependencies with repository rules: .NET 10, SQL Server,
  Keycloak JWT validation, RabbitMQ behind `IEventBus`, Azure OpenAI behind a
  provider adapter, and React with OIDC authorization-code + PKCE.
- Kept recipe catalog ownership, photos, the other team's services, and team
  milestone approval outside this assignment.

## Plan and Implement

- Added independent Domain/Data/Api project groups, service-owned SQL schemas,
  initial migrations, repository and Unit of Work abstractions, API contracts,
  health checks, structured request logging, correlation propagation, and
  OpenAPI.
- AI generation persists owner-scoped, idempotent requests; publishes
  generation work via a transactional outbox; validates bounded provider JSON;
  supports status polling and explicit user confirmation; and records
  completed/failed events.
- Review supports owner-scoped create/read/update/delete, database-enforced
  one-review-per-user-per-recipe, published-only feed pagination, moderator
  policy, state transitions, and transactional review events.
- Created the browser workflow for OIDC sign-in, opt-in pantry use, asynchronous
  recipe status, labelled recipe drafts, review submission, and moderation.
- Added local infrastructure and setup guidance without storing live
  credentials.
- Added workspace-specific layer agents and skills before implementation, as
  requested.

## Validate

Automated validation includes domain invariant and architecture tests,
framework-JWT integration tests, React authentication/API tests, Release
builds, and package-vulnerability checks. Database-backed tests and an end-to-
end browser run require Docker dependencies and a configured Azure OpenAI
deployment. The current environment did not provide a running Docker daemon,
so those external validations must be completed locally.

## Reflect and Improve

- Keep all generated content visibly labelled and subject to human
  confirmation; never represent AI allergen/nutrition output as authoritative.
- The outbox and processing lease recovery preserve work across transient
  broker, provider, or process failures. The RabbitMQ event archive queue is
  bounded by message count and a seven-day TTL.
- Human code review, another team member's PR review, team-board tracking, and
  merge remain explicit follow-up gates and are not claimed complete.
