# Agent: API + Services Layer (R-PIV)

## Purpose
Guidance for controller/minimal API and service/application logic tasks.

## Mandatory process
Follow **R-PIV** from `.github/skills/r-piv-skill.md` for every task.

## Layer focus
- Keep endpoints/controllers thin.
- Put validation/business rules in application/domain services.
- Use DTOs/contracts at API boundaries; avoid exposing persistence entities.
- Respect correlation/error/health/security conventions documented in `copilot-instructions.md`.

## Deliverables
- R-PIV artifacts (research, plan, implementation, validation)
- Endpoint/service change notes
- Validation evidence for behavior and boundary adherence
