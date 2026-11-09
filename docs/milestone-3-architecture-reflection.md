# Milestone 3 Architecture & Reflection

## Architecture

Milestone 3 uses independently runnable ASP.NET Core services organized into
three backend layers: **Domain**, **Data**, and **API**. Domain contains
framework-independent business rules and invariants. Data owns EF Core,
SQL Server persistence, service-specific schemas and migrations, repositories,
and external-system adapters. API exposes HTTP contracts and coordinates
application use cases while registering the Data implementations. Dependencies
flow from API to Data and Domain, and from Data to Domain; persistence entities
do not cross the public API boundary.

Each service owns its data and remains responsible for its own authorization
and health. Services share SQL Server infrastructure, not tables or direct
database access to one another. The API layer applies authentication and
least-privilege policies, validates public DTOs, and returns consistent
problem details. Correlation IDs are propagated through requests and logs.
Asynchronous AI generation demonstrates the same boundary discipline: requests
are persisted, work is dispatched through the event/outbox path, and generated
content remains a user-confirmed draft. The service and ownership decisions are
described in [ADR 0002](./adr/0002-service-architecture.md) and the
[Milestone 3 evidence](./milestone-3-ai-review-evidence.md).

The browser frontend is **React**, not server-rendered ASP.NET Core UI. The
shared Vite/React application in
[`src/ChopChop.Client`](../src/ChopChop.Client) calls each service over HTTP
using public DTO contracts. This keeps backend business and persistence
rules on the server while allowing multiple service workflows to share one
client rather than introducing a separate client for every service. The SPA
uses OIDC authorization-code flow with PKCE and obtains audience-specific,
scoped access tokens; it contains no client secret. ASP.NET Core remains the
backend API technology. This division is recorded in
[ADR 0003](./adr/0003-identity-and-client-auth.md) and the
[client guide](./meal-planning-client.md).

## Team work distribution

Milestone assignments are scoped by backend service, with corresponding
shared-client and validation work. Fill in the team allocation below.

| Team member | Service |
|---|---|
| Léa | Recipe |
| Léa | Cookbook |
| Vasken | Pantry |
| Vasken | Equipment |
| Xavier | AI recipe |
| Xavier | Review |
| Sean | User |
| Sean | Mealplan |

## Skill-based agents and manual correction

The R-PIV workflow (Research, Plan, Implement, Validate) and layer-specific
agents made work easier to partition and review. Domain, Data/Persistence,
API/Services, Frontend, and Testing agents each have explicit file boundaries,
responsibilities, and completion reports. The intended implementation order
—Domain, Data, API, then Client—helped keep dependencies and ownership visible
while multiple service workflows were developed. The recorded Milestone 3
evidence confirms that workspace-specific agents and skills were added before
implementation; no time-saved metric was recorded, so the benefit is qualitative
rather than measured.

Agent assistance did not remove the need for integration and manual checks.
The [Cookbook evidence](./milestone-3-cookbook-evidence.md) records issues
found and corrected during human validation, including a database connection
string mismatch, a client form-validation behavior problem, missing saved
recipe titles in the view, and test-database interference when tests ran in
parallel. It also records a duplicate-save case that would otherwise reach a
database error and an API startup failure when client origins were not
configured. These examples show where end-to-end configuration and UI behavior
needed human attention. The evidence does not establish that an agent
introduced any particular defect, so the corrections should be understood as
integration findings, not attributed to AI without supporting history.

## Effectiveness of human validation gates

The gates were useful because they tested more than whether code compiled.
Planning and architecture review kept service ownership and client/API
boundaries explicit. Focused manual and integration checks then exposed
configuration, persistence, parallel-test, and user-interface defects that
needed correction. Automated tests complement those checks: for example, the
service evidence describes authorization tests for invalid tokens, missing
scopes, and ownership isolation.

The record also shows why a gate is only effective when its result is captured.
Some milestone evidence labels test totals as placeholders, and the AI/Review
evidence reports that its .NET build and API integration tests were not verified
in that environment because the required .NET 10 SDK was unavailable. The
recorded evidence also leaves another team member's peer review as a follow-up;
this document does not claim that sign-off occurred. Before treating the
milestone as fully validated, record final build/test results and the actual
peer-review outcome. Overall, human validation found practical integration
defects and reduced risk, while incomplete or placeholder evidence limits what
can be claimed about full release readiness.

## Reflection

The strongest outcome is a consistent separation of backend ownership and a
single API-driven React client, supported by explicit agent scopes and
validation expectations. The main improvement is to make each gate's outcome
more traceable: record the exact checks run, distinguish automated failures
from manual findings, and capture peer approval separately. That will make
future agent contributions easier to correct and the team's completion status
easier to verify.
