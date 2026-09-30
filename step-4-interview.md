# Step 4: Agent Interview

## 1. Restatement

Issue #6 requests an authenticated User Pantry collection endpoint at `GET /api/v1/pantry-items/expiring?days=N`. It should return only the caller's pantry items whose expiration falls from the current UTC date through N days inclusively, excluding expired and non-expiring items. Results must be paginated and sorted by expiration, name, and ID. Validation, authorization, ownership filtering, deterministic time, cancellation, problem details, and DTO mapping must follow the repository rules. It must not add events, broker integration, cross-service calls, real JWT validation, packages, or unrelated endpoint changes.

## 2. Files and layers

The repository is currently at the mock/interface stage. Only the first two files below are confirmed to exist.

| File path | Layer | Expected change |
|---|---|---|
| [Repositories.cs](Repositories.cs#L59-L68) | Application boundary placeholder | Read only; existing `IPantry` contract is context, not confirmed final design |
| [MockClasses.cs](MockClasses.cs#L53-L61) | Domain placeholder | Read only; `PantryItem` is explicitly a mock |
| `docs/milestones.md` | Documentation | Read only; [not found] |
| `src/<Service>/Api/[Pantry endpoint file]` | Api | Modify; [not found] |
| `src/<Service>/Api/[DTO and validator files]` | Api | Modify or reuse; [not found] |
| `src/<Service>/Application/[Pantry query/service files]` | Application | Create or modify; [not found] |
| `src/<Service>/Domain/[Pantry item/domain files]` | Domain | Read or modify only if the real model requires it; [not found] |
| `src/<Service>/Infrastructure/[Pantry repository files]` | Infrastructure | Create or modify; [not found] |
| `src/<Service>/Infrastructure/[DbContext/configuration files]` | Infrastructure | Read only unless a schema change is proven; [not found] |
| `src/<Service>/Api/[authorization and ICurrentUser files]` | Api/Application | Reuse; [not found] |
| `src/<Service>/Application/[pagination result type]` | Application | Reuse; [not found] |
| `tests/<Service>.Tests/[Pantry unit/integration tests]` | Tests | Create or modify; [not found] |
| `docs/[Pantry endpoint and contract documentation]` | Documentation | Create or modify; [not found] |

The repository root contains no `src/`, `tests/`, or `docs/` directory in the current checkout.

## 3. Reuse

Verified reusable symbols:

- [`IPantry`](Repositories.cs#L59-L68)
  - `GetPantry(int userId)`
  - `AddItem(PantryItem item)`
  - `UpdateItem(PantryItem item)`
  - `DeleteItem(int itemId)`
  - `GetExpiringItems(int userId, TimeSpan horizon)`
- [`PantryItem`](MockClasses.cs#L53-L61)
  - `Id`
  - `UserId`
  - `Name`
  - `Quantity`
  - `Unit`
  - `Expiration`
- Repository API rules in [copilot-instructions.md](copilot-instructions.md#L104-L126)
- Authentication and ownership rules in [copilot-instructions.md](copilot-instructions.md#L190-L213)
- Thin endpoint and named-policy pattern in [copilot-instructions.md](copilot-instructions.md#L75-L90)

Not found:

- Final Pantry entity
- Final response DTO
- Pagination response type
- Pantry repository implementation
- Pantry authorization policy
- `ICurrentUser`
- Time abstraction
- DbContext
- Existing Pantry endpoint
- Existing Pantry tests

## 4. Assumptions

- **Verified:** User Pantry owns ingredients, quantities, units, expiration dates, and pantry suggestions. Evidence: [copilot-instructions.md](copilot-instructions.md#L55-L72)
- **Verified:** API routes use versioned plural resource nouns. Evidence: [copilot-instructions.md](copilot-instructions.md#L104-L107)
- **Verified:** Collections require pagination with default 20 and maximum 100. Evidence: [copilot-instructions.md](copilot-instructions.md#L113-L115)
- **Verified:** Private queries take ownership from authenticated claims through `ICurrentUser`. Evidence: [copilot-instructions.md](copilot-instructions.md#L190-L213)
- **Verified:** Reads should use `AsNoTracking()` and cancellation must pass through every layer. Evidence: [copilot-instructions.md](copilot-instructions.md#L67-L75), [copilot-instructions.md](copilot-instructions.md#L113-L115)
- **Verified:** Events and broker integration are later-milestone concerns and are excluded by issue #6. Evidence: [copilot-instructions.md](copilot-instructions.md#L133-L158)
- **Verified:** The only current Pantry query contract is `IPantry.GetExpiringItems(int userId, TimeSpan horizon)`. Evidence: [Repositories.cs](Repositories.cs#L59-L68)
- **Verified:** The current Pantry mock uses `int` identifiers and nullable `DateTime Expiration`. Evidence: [MockClasses.cs](MockClasses.cs#L53-L61)
- **Unverified:** The real service uses GUID identifiers. This is required by repository rules, but no real service model is present.
- **Unverified:** Expiration is represented as UTC `DateTimeOffset`, and whether it is date-only or timestamp-based.
- **Unverified:** The named Pantry read policy is `pantry.read`.
- **Unverified:** The existing collection response shape and pagination parameter names.
- **Unverified:** `TimeProvider` is registered or used elsewhere.
- **Unverified:** The Development authentication handler and `ICurrentUser` map directly to the Pantry owner identifier.

## 5. Gaps and contradictions

- `docs/milestones.md` is [not found], so the milestone-specific contract could not be independently verified.
- The actual Pantry service is [not found], so no implementation-level contradiction can currently be established.
- The route `/api/v1/pantry-items/expiring` follows the verified versioned plural-noun rule.
- The mock's `int Id` and `int UserId` conflict with the rule requiring GUID identifiers. The mock is explicitly non-final, so this must be resolved from the real model rather than by changing the mock.
- The mock's nullable `DateTime Expiration` conflicts with the UTC `DateTimeOffset` rule. The implementation must confirm whether expiration is a date or timestamp before choosing boundary comparisons.
- The issue requires owner filtering through `ICurrentUser`, but the mapping from authenticated identity to Pantry owner is [not found].
- `IPantry.GetExpiringItems(int userId, TimeSpan horizon)` does not express pagination, cancellation, explicit UTC boundaries, or fixed tie-breaking. It is a placeholder contract and is not implementation-ready.
- The issue requires a paginated response, but the existing pagination type is [not found].
- The request route names `days`, while the example also uses `page` and `pageSize`; the repository does not confirm those parameter names.
- The issue requires the existing Pantry read policy, but no such policy was found. Adding `pantry.read` is only an unverified fallback.
- No schema change is currently proven necessary. An owner-plus-expiration index should remain a follow-up unless the real DbContext demonstrates a need.
- No event, broker, package, cross-service call, or later-milestone feature is required by the issue.

## 6. Questions for the team

1. **Where is the real User Pantry implementation?**
   - Recommended answer: provide the branch, commit, or repository path containing `src/`, `tests/`, and `docs/`.
   - Consequence otherwise: implementation planning can only target placeholders.

2. **Which identifier type should the service use?**
   - Recommended answer: use the real service's GUID item and owner identifiers and map `ICurrentUser` to the owner GUID.
   - Consequence of choosing integers: this conflicts with the repository rule and requires an explicit exception or ADR.

3. **Is expiration date-only or timestamp-based?**
   - Recommended answer: use UTC `DateTimeOffset` and compare `[today at 00:00, today + days + 1 at 00:00)` in UTC.
   - Consequence of choosing date-only semantics: the persistence and DTO contract must document that explicitly.

4. **What pagination contract should the endpoint use?**
   - Recommended answer: reuse the service's existing collection response and parameter names; if none exists, use `page`, `pageSize`, `items`, and `totalCount`, with defaults 1/20 and maximum page size 100.
   - Consequence otherwise: the issue cannot define a stable API contract.

5. **What is the existing Pantry read policy name?**
   - Recommended answer: reuse the real named policy; use `pantry.read` only if no policy exists.
   - Consequence otherwise: the endpoint may violate the service's authorization contract.

6. **Which interface owns the query?**
   - Recommended answer: evolve the real Pantry repository/query interface rather than inventing a parallel abstraction; include owner, UTC bounds, pagination, and `CancellationToken`.
   - Consequence otherwise: duplicate data-access boundaries may be introduced.

7. **Where is the project's milestone documentation?**
   - Recommended answer: provide `docs/milestones.md` or confirm that the root instructions are authoritative for this step.
   - Consequence otherwise: milestone compliance remains partially unverified.

## 7. Out of scope

I will not:

- Write or modify code during this interview step.
- Modify or comment on issue #6.
- Change the mock classes solely to satisfy final repository rules.
- Add or design events such as `PantryItemExpiring`.
- Add the message broker.
- Add real JWT validation.
- Add cross-service calls.
- Add a new service or package.
- Require a schema migration without evidence from the real DbContext.
- Change existing Pantry endpoints.
- Choose final identifier, timestamp, DTO, policy, or pagination types without the real service code or the team's decision.
