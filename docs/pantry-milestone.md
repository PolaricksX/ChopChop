# Pantry milestone evidence

- Projects and tests are in the required root `src/Pantry.*` and
  `tests/Pantry.Tests` layout and are included in `Solution.sln`.
- Protected requests without explicit development test credentials return 401;
  read-only credentials cannot write (403).
- Mutations are persisted with the transactional outbox and published through
  the shared event bus when RabbitMQ is configured.
- No live identity provider or repository idempotency-key abstraction is
  available in this environment; these limitations are documented in
  `pantry.md`.
