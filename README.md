# ChopChop

<img width="1408" height="768" alt="ChoppyMascot" src="https://github.com/user-attachments/assets/e34de999-86a1-4990-b3d5-039da40445cb" />

## Milestones

- Milestone 2 established the repository process assets (R-PIV, layer guidance,
  diagrams, and reflection). See [docs/reflection.md](docs/reflection.md).
- Milestone 3 implements Xavier's assigned AI Recipe Generation and
  Interactive Review services. See [docs/milestone-3-ai-review-plan.md](docs/milestone-3-ai-review-plan.md)
  and the GitHub work items [#18](https://github.com/PolaricksX/ChopChop/issues/18)
  and [#19](https://github.com/PolaricksX/ChopChop/issues/19).
- Service setup and API contracts are documented in
  [docs/local-development.md](docs/local-development.md) and
  [docs/api-contracts.md](docs/api-contracts.md). The .NET solution and React
  client live under `src/` (client: `src/ChopChop.Client`).
  Pantry is in `src/Pantry.*`, with tests in `tests/Pantry.Tests`; see
  [docs/pantry.md](docs/pantry.md).

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
  `Pantry.*`, `ChopChop.Shared`, `ChopChop.Client`, and `MealPlanner.Client` (React)
- `tests/` — unit and integration tests for each service
- `compose.yaml` — local SQL Server and RabbitMQ dependencies; set required credentials through environment variables
- `Solution.sln` — build with `dotnet build`, test with `dotnet test`

Services use the shared `ConnectionStrings:ChopChop` database connection and own separate tables.

The React browser client is in `src/MealPlanner.Client`. See the
[Meal Planning client guide](docs/meal-planning-client.md) for setup,
authentication, and API behavior.
