# ChopChop

<img width="1408" height="768" alt="ChoppyMascot" src="https://github.com/user-attachments/assets/e34de999-86a1-4990-b3d5-039da40445cb" />

## Milestones

- Milestone 2 established the repository process assets (R-PIV, layer guidance,
  diagrams, and reflection). See [docs/reflection.md](docs/reflection.md).
- Milestone 3 includes AI Recipe Generation and Interactive Review, plus the
  individually authorized User Equipment service for issue #51. See
  [docs/milestone-3-ai-review-plan.md](docs/milestone-3-ai-review-plan.md),
  [docs/equipment-api.md](docs/equipment-api.md), and
  [ADR 0007](docs/adr/0007-user-equipment-service-scope.md).
- Service setup and API contracts are documented in
  [docs/local-development.md](docs/local-development.md) and
  [docs/api-contracts.md](docs/api-contracts.md). The .NET solution and React
  client live under `src/` (client: `src/ChopChop.Client`).

## Repository structure

- `.github/skills/r-piv-skill.md` — R-PIV skill definition used for project work.
- `.github/agents/` — reusable R-PIV-based agent instructions by layer:
  - `r-piv-general-agent.md`
  - `frontend-agent.md`
  - `api-services-agent.md`
  - `data-access-agent.md`
- `docs/` — documentation assets for milestone evidence and service contracts:
  - `KitchenHelperMultiDB_Update.drawio`
  - `Services Diagram.png`
  - `reflection.md`
  - `user-service.md` — User Service contract, configuration and limitations
  - `meal-planner.md` — Meal Planner contract, configuration, eventing and limitations
- `src/` — `UserService.*`, `MealPlanner.*`, `AiRecipeGeneration.*`,
  `InteractiveReview.*`, and `RecipeManagement.*` services, plus
  `ChopChop.Shared` and the unified `ChopChop.Client` React app
- `tests/` — unit and integration tests for each service
- `compose.yaml` — local SQL Server and RabbitMQ dependencies; set required credentials through environment variables
- `Solution.sln` — build with `dotnet build`, test with `dotnet test`

Services own separate tables. Most use the shared `ConnectionStrings:ChopChop`
database connection; Equipment uses `ConnectionStrings:Equipment`.

The React browser client is in `src/ChopChop.Client`; meal planning is available
from its Meal Plans tab. See the [Meal Planning client guide](docs/meal-planning-client.md)
for setup and API behavior.
