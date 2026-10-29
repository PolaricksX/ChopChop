---
name: service-implementation
description: Use when implementing an assigned ChopChop .NET service across Domain, Data, API, and client layers.
---

# Assigned ChopChop Service Implementation

Before editing, load the approved work item, R-PIV plan, applicable ADRs, and
relevant `.github/agents/*.agent.md` specialization.

- Keep the service in its owning projects and its data in its owned schema.
- Work from Domain to Data to API. Keep Domain free of framework and I/O
  dependencies; put EF Core and external adapters in Data; keep API endpoints
  thin and DTO-only.
- Use scoped constructor injection, async I/O, cancellation, input limits,
  problem details, structured redacted logs, correlation IDs, and explicit
  authorization policies.
- Keep provider calls, event transport, and file storage behind their approved
  interfaces. Treat model output as untrusted and cross-service data as
  externally owned.
- Add deterministic tests and update contracts/docs in the same work item.
- Stop and ask before adding packages, changing ownership, changing an ADR, or
  adding a cross-service dependency.
