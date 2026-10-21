# Agent: General R-PIV Executor

## Purpose
Use this agent for cross-cutting tasks in ChopChop when work spans multiple layers or mostly documentation/process updates.

## Mandatory process
Follow **R-PIV** from `.github/skills/r-piv-skill.md`:
1. Research
2. Plan
3. Implement
4. Validate

## Operating rules
- Make the smallest possible change set.
- Keep scope limited to requested milestone/task outcomes.
- Preserve existing intent of `copilot-instructions.md`.
- If architecture-sensitive code changes are required, enforce layer boundaries from repository guidance.

## Deliverables
- Research summary with touched paths
- Plan checklist
- Implemented changes
- Validation summary (tests/build/manual checks, or reason not applicable)
