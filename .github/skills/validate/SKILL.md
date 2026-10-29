---
name: validate
description: Use before marking a ChopChop issue ready for review; run the repository's actual .NET and React checks and report exact outcomes.
---

# ChopChop Validation

Read `Solution/README.md` and project scripts before choosing commands. Run
restore, Release build, focused unit/integration tests, format verification,
vulnerability audit, and the actual client typecheck/test/build scripts.

For integration tests, start SQL Server/RabbitMQ/Keycloak only when required
and report whether each dependency was available. API auth tests use the real
framework JWT bearer handler with test signing credentials. Never claim a
skipped integration or unavailable provider test passed. Fix changes caused by
the current work and rerun the failing focused check.

Report PASS/FAIL/SKIPPED for each check with the command and relevant output.
