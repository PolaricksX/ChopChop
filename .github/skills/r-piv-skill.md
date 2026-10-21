# R-PIV Skill for ChopChop (Milestone 2)

This repository uses the **R-PIV process** for all coding and documentation tasks.

- **R = Research**
- **P = Plan**
- **I = Implement**
- **V = Validate**

The goal is to keep changes small, traceable, and aligned with the repository's .NET service architecture guidance in `copilot-instructions.md`.

## 1) Research

Understand the request and current repository state before editing.

### Expected artifacts
- Brief scope summary (what will and will not change)
- Paths of files to be touched
- Risks/assumptions list

### Required checks
- Confirm existing behavior/structure first
- Reuse repository conventions (layer boundaries, security, docs style)
- Avoid inventing architecture outside current milestone scope

## 2) Plan

Define minimal steps that satisfy the requirement.

### Expected artifacts
- Ordered checklist of changes
- Validation plan (tests, build, or manual checks)

### Required checks
- Plan is minimal and focused
- No unrelated refactors
- Documentation updates included when structure changes

## 3) Implement

Apply the smallest correct set of file changes.

### Expected artifacts
- Focused commits grouped by objective
- Updated files only where required

### Required checks
- Respect architecture direction in `copilot-instructions.md`
  - Thin API boundary approach
  - Application/domain rules separated from infrastructure concerns
  - Data access patterns through repositories/UOW/EF boundaries
- Do not introduce secrets or unsafe defaults

## 4) Validate

Prove the change is correct and safe.

### Expected artifacts
- Evidence of checks run (tests/build/manual link checks)
- Short summary of what was validated and any limitations

### Required checks
- Run existing tests/build/lint when available and relevant
- For docs-only or structure-only work, verify:
  - File tree/path correctness
  - Markdown links and references
  - No missing moved assets
- Confirm no accidental unrelated file changes

## R-PIV completion criteria

A task is complete only when:
1. Research, plan, implementation, and validation artifacts are all present.
2. Changes are minimal and scoped to the request.
3. Documentation reflects the final repository structure.
