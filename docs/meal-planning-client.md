# Meal Planning browser feature

Meal Planner is a feature of the authenticated ChopChop browser client in
`src/ChopChop.Client`. It shares the ChopChop sign-in session and calls the
versioned Meal Planner API; it does not connect to service databases, decide
ownership, or implement server-side business rules. `ChopChop.Shared` remains
the .NET server-side shared library.

## Local setup

Use Node.js 22.12 or newer (or Node.js 24 or newer) and npm. Start the Meal
Planner API as described in [the service guide](./meal-planner.md), allow the
client origin through `Cors__AllowedOrigins__0`, then start ChopChop:

```powershell
Copy-Item src\ChopChop.Client\.env.example src\ChopChop.Client\.env.local
Set-Location src\ChopChop.Client
npm ci
npm run dev
```

The development server is normally available at `http://localhost:5173`.
`npm run build` creates the production assets in `dist`; `npm run preview`
serves those assets locally.

## Configuration and authentication

The client uses the existing ChopChop OIDC configuration and session. Its
`.env.local` file may override these public settings:

| Variable | Purpose |
|---|---|
| `VITE_MEAL_PLANNER_API_URL` | Meal Planner API origin, without `/api/v1/meal-plans`; defaults to `http://localhost:5033`. |
| `VITE_OIDC_AUTHORITY` | OpenID Connect issuer; defaults to `http://localhost:8080/realms/chopchop`. |
| `VITE_OIDC_CLIENT_ID` | Public ChopChop browser-client identifier. |

ChopChop requests the `mealplans.read` and `mealplans.write` scopes during its
normal sign-in. Configure the identity provider to grant those scopes and
issue tokens with the audience expected by the Meal Planner API, in addition
to the existing ChopChop API scopes. The local Compose realm may need
identity-provider configuration changes for this API. These are public
build-time settings: never put a client secret, password, private key, or
other credential in a `VITE_*` variable or browser bundle.

The client keeps the OIDC session in `sessionStorage` through the existing
`oidc-client-ts` user manager. A `401` from the Meal Planner API removes that
session and returns the user to ChopChop's sign-in screen; a `403` remains an
authorization error. The API derives ownership from the validated token's
`sub` claim; the client never sends an owner field.

Set `Cors__AllowedOrigins__0` for the APIs to the actual Vite origin. If Vite
selects another port because the default is occupied, use that origin in the
API CORS configuration and the identity provider's allowed web origins. The
Meal Planner API must expose `ETag` and allow `Authorization`,
`Idempotency-Key`, `If-Match`, `Content-Type`, and `X-Correlation-Id`.

## API contract

The feature calls only the public versioned Meal Planner API:

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

Meal plans use inclusive ISO calendar dates (`YYYY-MM-DD`), are limited to
366 days, and do not undergo timezone conversion. Breakfast, Lunch, and
Dinner allow one meal per date and slot; Snack entries are unlimited. Plan
lifecycle transitions are `Draft -> Active -> Completed` or
`Draft/Active -> Cancelled`. Completed and cancelled plans are read-only.
Meal progress transitions are `Planned -> InProgress/Completed/Skipped` and
`InProgress -> Completed/Skipped`; completed and skipped meals are terminal.
Only planned meals can be edited or removed. An optional recipe reference is
a stable recipe UUID; the client does not fetch private or cross-service data
to resolve it.

The interface surfaces API validation errors, expired authentication,
forbidden access, missing resources, stale-version or duplicate-slot
conflicts, rate limits, and service/network failures. It preserves
problem-detail correlation IDs and does not substitute sample data when the
API is unavailable.
