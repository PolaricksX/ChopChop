# Milestone 2 Reflection

For Milestone 2, we focused on aligning the repository structure with the instructor's requirements while avoiding unnecessary application changes.

## What we learned

- The milestone expectation is not only about code, but also about having a repeatable process and repository structure.
- Making the workflow explicit with **R-PIV (Research, Plan, Implement, Validate)** helps keep changes scoped and auditable.
- Layer-oriented agent instructions are useful even before all layers are fully implemented, because they set expectations for future work.

## How R-PIV, skills, and agents are represented

- The skill definition is in `.github/skills/r-piv-skill.md`.
- Reusable agent instructions are in `.github/agents/`:
  - `r-piv-general-agent.md`
  - `frontend-agent.md`
  - `api-services-agent.md`
  - `data-access-agent.md`
- Each agent requires following the same R-PIV stages and producing research/plan/implementation/validation outputs.

## What was difficult

- The repository currently mixes milestone assets and starter-pack material in different locations, so the main challenge was deciding what to relocate versus what to keep unchanged.
- We also had to define practical agent instructions for layers (frontend and services) that are not yet fully implemented, without inventing unsupported application behavior.

## What remains incomplete or assumed

- This update provides structure and process guidance; it does not add missing frontend or complete production service implementations.
- Agent files are reusable instruction templates for future work rather than runtime-enforced automation.
- Validation here is mainly structural/documentation validation, because this milestone task is not introducing executable application code.

## How the architecture diagrams relate to this milestone

- `docs/KitchenHelperMultiDB_Update.drawio` and `docs/Services Diagram.png` are now colocated with reflection and process documentation.
- Keeping diagrams in `docs/` makes architecture intent easier to find and keeps milestone evidence in one place.
