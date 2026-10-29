# AI Recipe and Interactive Review API Contracts

Both APIs use framework-validated OpenID Connect bearer tokens, RFC 9457
problem details, JSON string enum values, and `X-Correlation-Id`. A missing
or invalid token is rejected. Each API accepts only its configured
`Client:Origin`.

## AI Recipe Generation — port 5081

| Method and route | Policy | Result |
|---|---|---|
| `POST /api/recipe-generations` | `recipe:create` | `202 Accepted`; persists an idempotent request and status URL |
| `GET /api/recipe-generations/{id}` | `recipe:read` | Owner-only status and, when ready, labelled draft |
| `POST /api/recipe-generations/{id}/confirm` | `recipe:confirm` | Owner-only transition from `Completed` to `Confirmed` |
| `GET /health` | Anonymous | Checks only this service's database |

Example request (the pantry is sent only after the user opts in):

```http
POST /api/recipe-generations
Authorization: Bearer <access-token>
Idempotency-Key: <unique-client-generated-key>
Content-Type: application/json

{
  "prompt": "A quick vegetarian dinner with a bright sauce",
  "includePantry": true,
  "pantryItems": ["beans", "lemon"]
}
```

Request prompt length is bounded to 2000 characters and pantry use to 50
items (120 characters each). Generation runs asynchronously through RabbitMQ;
the request, status, and request event outbox row are committed together.
Duplicate idempotency keys return the existing request unless the submitted
content differs, in which case the API returns `409 Conflict`.

Status transitions are `Queued -> Processing -> Completed -> Confirmed`, with
provider or validation failures ending as `Failed`. Stale processing leases
are recovered and republished. Provider output is untrusted JSON and must pass
bounded domain validation before storage. A failed or unconfigured provider
does not produce a success-shaped recipe.

Every stored draft has an explicit AI-generated label and estimated-nutrition
label. Allergen declarations are required; an empty list means the generator
identified none, not that the recipe is safe for a particular person. The
client displays this distinction and a food-safety disclaimer. A user must
confirm a completed draft before it can leave the generated-draft workflow.

## Interactive Review — port 5082

| Method and route | Policy | Result |
|---|---|---|
| `POST /api/reviews` | `review:create` | Owner-authored pending review |
| `GET /api/recipes/{recipeId}/reviews?page=1&pageSize=20` | `review:read` | Published-only, bounded page |
| `GET /api/reviews/{id}` | `review:read` | Owner-only review |
| `PUT /api/reviews/{id}` | `review:create` | Owner-only update; sends it back to moderation |
| `DELETE /api/reviews/{id}` | `review:create` | Owner-only deletion |
| `GET /api/moderation/reviews?limit=50` | `review:moderate` | Bounded moderation queue |
| `POST /api/moderation/reviews/{id}` | `review:moderate` | Moderator decision and reason |
| `GET /health` | Anonymous | Checks only this service's database |

Review ratings are 1–5, review text is limited to 2000 characters, and
suggestions to 1000 characters. A unique database index enforces one review
per user per recipe. New and edited reviews are private until approved.
Supported moderation transitions are `Pending -> Published/Rejected` and
`Published -> Hidden`. Published-review listing never returns pending,
rejected, or hidden reviews.

Review photos are not accepted in this milestone. Do not send photo URLs as a
substitute; future uploads must use the approved storage boundary and
validation controls in ADR 0006.

## Events

Service state changes and event records are persisted together in service
outboxes. The outbox publisher uses durable RabbitMQ messages, confirms
publishes, exponential retry, and a database dead-letter state after eight
failed attempts. Correlation IDs are included in event envelopes and forwarded
to the AI-provider HTTP request.

AI event routes include `ai_recipe.generation.requested`,
`ai_recipe.generation.completed`, `ai_recipe.generation.failed`, and
`ai_recipe.generation.confirmed`. Review routes include
`interactive_review.review.submitted`, `.updated`, `.deleted`, `.published`,
`.rejected`, and `.hidden`. Event payloads contain identifiers, rating/status
metadata, and action—not review text or pantry contents.
