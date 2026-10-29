# ADR 0001: Milestone 3 scope and repository baseline

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The repository guidance named Milestone 1 as current, while the assigned
Milestone 3 PDF and issues #18/#19 describe AI Recipe Generation and Interactive
Review as this developer's work. The repository has no production solution
projects yet.

## Decision

Set the repository guidance to Milestone 3 and implement only the assigned AI
Recipe Generation and Interactive Review services, their connected client, and
direct supporting infrastructure/documentation. Keep the two services
independently runnable under the repository root `src/` and `tests/`. Do not claim completion of other
team members' services or team-level milestone submission, PR, or peer gates.

## Consequences

- The Copilot instructions, README, and milestone scope document must agree.
- Existing root process docs, diagrams, mock classes, and Milestone 2 evidence
  remain intact.
- A later change of service scope requires a separate explicit decision.
