---
name: code-review
description: Use for read-only pre-merge review of ChopChop diffs, especially security, layer boundaries, API ownership, and failure-path tests.
---

# ChopChop Read-Only Code Review

Use the read-only Code Review agent. Inspect the complete diff and surrounding
files against the issue acceptance criteria, repository instructions, and
approved ADRs.

Report only substantiated findings with severity, file and line, impact, and
suggested correction. Check architectural boundaries, authn/authz, ownership,
input/output limits, idempotency, cancellation, concurrency, event delivery,
secrets/logs, and failure-path tests. Do not edit files or claim another
team-member's approval.
