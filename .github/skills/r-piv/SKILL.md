---
name: r-piv
description: Use for ChopChop work items to research the issue and repository, plan before coding, implement in layer order, and validate before human review.
---

# ChopChop R-PIV

Follow Research → Plan → Implement → Validate for every assigned work item.

1. **Research:** Read the issue acceptance criteria, current instructions, ADRs,
   relevant source/tests, and git status. Identify ownership, scope, risks, and
   files to touch.
2. **Plan:** Write a file-by-file plan and validation commands. For new
   architecture or behavior, stop and request the developer's Gate 1 approval
   before implementation.
3. **Implement:** Work in dependency order: Domain, Data, API, then Client.
   Keep changes scoped to the assigned issue. Run focused tests as layers are
   completed.
4. **Validate:** Run the actual build, tests, formatter, and vulnerability
   checks. Inspect the complete diff and report unavailable services rather
   than claiming skipped checks passed.

Human review remains required. Never claim peer signoff, merge, or human gate
approval unless the named human performed it.
