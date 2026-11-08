# Pantry milestone evidence

- Projects and tests are in the required root `src/Pantry.*` and
  `tests/Pantry.Tests` layout and are included in `Solution.sln`.
- Protected requests without a bearer token return 401; read-only tokens cannot
  write (403). Authentication tests exercise the framework JWT bearer handler
  with test signing credentials, not development test headers.
- Mutations are persisted with the transactional outbox and published through
  the shared event bus when RabbitMQ is configured.
- No live identity provider or repository idempotency-key abstraction is
  available in this environment; these limitations are documented in
  `pantry.md`.
