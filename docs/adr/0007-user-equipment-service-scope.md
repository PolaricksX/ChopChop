# ADR 0007: User Equipment service scope

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

ADR 0001 limited the initial Milestone 3 assignments to AI Recipe Generation
and Interactive Review. The repository's service ownership list separately
assigns appliances, cookware, availability, and equipment filtering to User
Equipment. Issue #51 and its child issues #52–#57 define an independently
assigned Equipment service implementation.

## Decision

Authorize issue #51 as an additional individual Milestone 3 assignment.
Implement the Equipment Domain, Data, and API projects, tests, and the
Equipment client flow in `ChopChop.Client`. Equipment owns its records and
schema. It may use the approved shared event envelope and `IEventBus`, but it
must not read or write another service's tables or introduce a cross-service
database dependency.

This decision expands the individual issue scope only. It does not claim
completion of another team's assignment, human review, peer signoff, or
milestone-level submission.

## Consequences

- The accepted scope in ADR 0001 is extended for issue #51; its other
  architectural decisions remain unchanged.
- `docs/milestones.md` and `copilot-instructions.md` list issue #51 among the
  current assigned work.
- The service follows the existing SQL Server, JWT bearer, transactional
  outbox, correlation, and client/API boundary decisions.
