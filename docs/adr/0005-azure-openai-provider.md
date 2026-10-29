# ADR 0005: Azure OpenAI provider adapter

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

AI Recipe Generation needs an external model, but no provider had been chosen.
The developer selected Azure OpenAI for this assigned work. The platform
requires provider isolation, minimal user data, strict output validation,
explicit timeout/retry/cost controls, and no credentials in source.

## Decision

Keep the provider behind `IAiRecipeProvider` in Data. Use Azure OpenAI's
documented inference API and strict structured output supported by the selected
deployment. Configure endpoint, deployment, timeout, and key per environment;
the key is supplied through environment variables/user-secrets. Do not send
pantry context unless the user explicitly opts in. Do not log prompts or
personal dietary details. Require a real provider configuration in production;
return an explicit unavailable/failure response when unconfigured. Unit tests
use a deterministic fake.

## Consequences

- Generated output is untrusted until parsed and validated against the
  application schema.
- The provider must not receive secrets or unnecessary profile information.
- The user must configure a valid Azure OpenAI resource and deployment locally
  to exercise live generation.
