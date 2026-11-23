# ADR 0008: Admin and Pro Chef roles map to scopes

- **Status:** Proposed (design approved by the developer; peer review pending)
- **Date:** 2026-11-22

## Context

Authorization is scope-based: each API enforces named policies such as
`reviews.moderate` and never reads role names. Keycloak and Auth0 expose roles
in different claims, so role-name checks in the APIs would tie them to one
identity provider. The repository also requires administrator permissions to
be separate from user permissions and enforced at the owning service.

## Decision

Add two roles, **Admin** and **Pro Chef**, alongside the default user (no
role). Roles live in the identity provider and are mapped to permission scopes
that the APIs already read from `scope`, `scp`, or `permissions`.

| Role | Permissions |
|---|---|
| User (no role) | Existing scopes only; owner-only access to private data |
| Pro Chef | `recipe-generations.create`, `recipe-generations.confirm` |
| Admin | `reviews.moderate`, `users.admin`, `recipes.admin` |

- `users.admin` (User Service): read any profile; disable (soft-delete) any
  profile.
- `recipes.admin` (Recipe Management): unpublish or remove any recipe.
- Admin and Pro Chef do not get cross-user access to private data in Pantry,
  Meal Planner, Cookbook, or Equipment. Admin keeps owner-only rules on its own
  data. No `equipment.admin` is defined because Equipment has no shared catalog.
- Admin routes live under `/api/v1/admin/...` with their own policy. Admin
  actions are logged with the acting subject and the target ID.

## Consequences

- AI generation becomes Pro Chef only (existing behavior change); reading a
  generation (`recipe-generations.read`) stays open to users.
- Granting a role in the identity provider is the only way to obtain these
  scopes. Never add `users.admin`, `recipes.admin`, or `reviews.moderate` as a
  client scope that a user can request on their own.
- Admin unpublish is reversible by the recipe owner; admin removal is not.
