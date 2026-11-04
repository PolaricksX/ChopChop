# ADR 0004: RabbitMQ events and durable AI generation

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The repository platform decision specifies RabbitMQ behind `IEventBus` from
Milestone 2. AI generation must be asynchronous, return `202 Accepted`, and
retain a pollable status. The repository currently has no event adapter or
service code.

## Decision

Use RabbitMQ behind an event-bus abstraction. Persist AI generation requests
and an outbox message in the same service transaction before returning 202.
Dispatch the outbox to a durable generation queue, and make processing
idempotent so duplicate delivery cannot create conflicting results. Review
events use the same event envelope and an outbox transaction when state and
event publication must not separate. Document retry limits, poison/dead-letter
handling, ordering, and eventual consistency.

## Consequences

- In-memory channels are not the durable work queue.
- Broker/provider outages produce explicit pending/failed behavior, never a
  fabricated successful recipe.
- Each service owns its database records; event payloads remain minimal and
  carry correlation metadata.
