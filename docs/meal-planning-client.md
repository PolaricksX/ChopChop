# Meal Planning in ChopChop

Meal planning is part of the unified React application in
`src/ChopChop.Client`, available from its **Meal Plans** tab. It uses the
shared ChopChop Auth0 session and calls the versioned Meal Planner API; it does
not connect to service databases, decide ownership, or implement server-side
business rules.

## Milestone alignment

`copilot-instructions.md` names Milestone 1 as current, while the frontend
agent/R-PIV guidance is labeled Milestone 2. The referenced `docs/milestones.md`
is not present in this checkout. Issue #22 was explicitly authorized as a
Milestone 1 exception before implementation; this is a scoped exception, not a
change to the repository-wide milestone or authorization to add an identity
provider or backend capability.

## Local setup

Use Node.js 22.12 or newer (or Node.js 24 or newer) and npm. Start the Meal
Planner API as described in [the service guide](./meal-planner.md), and allow
the ChopChop client origin through `Cors__AllowedOrigins__0`.

```powershell
Copy-Item src\ChopChop.Client\.env.example src\ChopChop.Client\.env.local
Set-Location src\ChopChop.Client
npm ci
npm run dev
```

The development server is available at `http://localhost:5173`. See the
[ChopChop client guide](../src/ChopChop.Client/README.md) for shared Auth0
setup. The Meal Planner API audience must be configured as described in the
[local development guide](./local-development.md).

## Configuration and authentication

Set these public values in `src/ChopChop.Client/.env.local`:

| Variable | Purpose |
|---|---|
| `VITE_MEAL_PLANNER_API_URL` | Meal Planner API origin, without `/api/v1/meal-plans`; defaults to `http://localhost:5033`. |
| `VITE_AUTH0_MEAL_PLANNER_AUDIENCE` | Meal Planner API audience; defaults in `.env.example` to `https://api.chopchop.local/meal-planner`. |

The unified app uses the Auth0 SPA and its authorization-code flow with PKCE.
Configure its callback/logout URLs and web origin as described in the
[ChopChop client guide](../src/ChopChop.Client/README.md). In Auth0, enable
the Meal Planner API's `mealplans.read` and `mealplans.write` delegated
permissions and assign them only to the appropriate user roles. The app
requests the read or write permission needed for each operation and retries a
401 once with a fresh token.

These are public build-time settings. Never put a client secret, password,
private key, or other credential in a `VITE_*` variable or browser bundle. The
API derives ownership from the validated token's `sub` claim; the client never
sends an owner field.

Set `Cors__AllowedOrigins__0=http://localhost:5173` for the API, and make sure
the identity provider allows the API origin and the configured client scopes.
The development API's CORS allowlist must also expose `ETag` and allow
`Authorization`, `Idempotency-Key`, `If-Match`, `Content-Type`, and
`X-Correlation-Id`, as configured by the service.

## Routes and API contract

Meal planning is available inside the unified app at `/` after signing in. It
calls only the public versioned Meal Planner API:

| Operation | API route |
|---|---|
| List plans | `GET /api/v1/meal-plans?page={page}&pageSize=20` |
| Retrieve a plan | `GET /api/v1/meal-plans/{planId}` |
| Create a plan | `POST /api/v1/meal-plans` |
| Update or delete a plan | `PUT` or `DELETE /api/v1/meal-plans/{planId}` |
| Change plan status | `PATCH /api/v1/meal-plans/{planId}/status` |
| List meals | `GET /api/v1/meal-plans/{planId}/meals?page={page}&pageSize=20[&date=YYYY-MM-DD]` |
| Add a meal | `POST /api/v1/meal-plans/{planId}/meals` |
| Edit or remove a meal | `PUT` or `DELETE /api/v1/meal-plans/{planId}/meals/{entryId}` |
| Record meal progress | `PATCH /api/v1/meal-plans/{planId}/meals/{entryId}/status` |

The client honors API pagination and uses page size 20. Create operations send
an `Idempotency-Key`; writes include the latest plan `version`, and delete
operations quote the latest version in `If-Match`. Version conflicts are
reported with a refresh action; writes are not blindly retried.

Meal plans use inclusive ISO calendar dates (`YYYY-MM-DD`), are limited to 366
days, and do not undergo timezone conversion. Breakfast, Lunch, and Dinner
allow one meal per date and slot; Snack entries are unlimited. Plan lifecycle
transitions are `Draft -> Active -> Completed` or `Draft/Active -> Cancelled`.
Completed and cancelled plans are read-only. Meal progress transitions are
`Planned -> InProgress/Completed/Skipped` and
`InProgress -> Completed/Skipped`; completed and skipped meals are terminal.
Only planned meals can be edited or removed. The UI offers only these
transitions and explains the read-only behavior.

A meal title is a bounded snapshot. An optional recipe reference is a stable
recipe UUID; the client does not fetch private or cross-service data to resolve
it, so an unavailable recipe does not block plan management.

## Failure handling and accessibility

The interface has loading and empty states and surfaces API validation errors,
expired authentication, forbidden access, missing resources, stale-version or
duplicate-slot conflicts, rate limits, and service/network failures. It
preserves problem-detail correlation IDs for support and does not substitute
sample data when the API is unavailable.

Forms enforce the API's name/title, date range, serving, and recipe-ID limits
before submitting. The API remains authoritative for ownership, transitions,
concurrency, and duplicate entries. The interface uses semantic headings,
labels, tables, status messages, keyboard-visible focus, and responsive
layouts.

## Validation

Run from `src/ChopChop.Client`:

```powershell
npm test
npm run build
npm run lint
```
