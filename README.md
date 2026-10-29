# ChopChop

<img width="1408" height="768" alt="ChoppyMascot" src="https://github.com/user-attachments/assets/e34de999-86a1-4990-b3d5-039da40445cb" />

## Milestone 2 repository structure

- `.github/skills/r-piv-skill.md` — R-PIV skill definition used for project work.
- `.github/agents/` — reusable R-PIV-based agent instructions by layer:
  - `r-piv-general-agent.md`
  - `frontend-agent.md`
  - `api-services-agent.md`
  - `data-access-agent.md`
- `docs/` — documentation assets for milestone evidence:
  - `KitchenHelperMultiDB_Update.drawio`
  - `Services Diagram.png`
  - `reflection.md`
  - `user-service.md` — User Service contract, configuration and limitations
- `src/` — `UserService.Domain`, `UserService.Data`, `UserService.Api`
- `tests/` — `UserService.UnitTests`, `UserService.IntegrationTests`
- `Solution.sln` — build with `dotnet build`, test with `dotnet test`
