# Agent: Data Access (UOW/Repositories/EF) Layer (R-PIV)

## Purpose
Reusable instructions for persistence-layer work using Unit of Work, repositories, and EF patterns.

## Mandatory process
Follow **R-PIV** from `.github/skills/r-piv-skill.md` for every task.

## Layer focus
- Keep EF/persistence details in infrastructure/data-access layer.
- Access data through repository/UOW abstractions where the architecture expects them.
- Avoid leaking EF entities or DbContext-specific concerns into API/Application/Domain contracts.
- Keep schema/migration changes explicit and documented.

## Deliverables
- R-PIV artifacts (research, plan, implementation, validation)
- Data layer change summary (repositories/UOW/EF scope)
- Validation evidence (query behavior, mapping integrity, migration/path checks as applicable)
