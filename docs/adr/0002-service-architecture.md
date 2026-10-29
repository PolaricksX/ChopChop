# ADR 0002: Layered services and owned persistence

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The Milestone 3 PDF and repository instructions require independently
runnable, strictly layered services and explicit data ownership.

## Decision

Create separate Domain, Data, and Api projects for AI Recipe Generation and
Interactive Review under `Solution/src/`, plus one React client under
`Solution/src/`. Each service owns its schema, DbContext, migrations, and
service workflows in the shared SQL Server instance. Dependencies flow Api to
Data/Domain and Data to Domain. Domain has no EF Core or ASP.NET Core
references. Only stable event/correlation/result primitives may be shared.

## Consequences

- Public API contracts use DTOs; EF entities never cross the API boundary.
- Each service is testable and runnable independently.
- Cross-service reads/writes require a documented versioned contract or
  approved event workflow; direct table access is prohibited.
