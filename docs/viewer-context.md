# Internal viewer context

`GET /user-service/internal/users/{userId:guid}/viewer-context?companyId={companyId}` requires the configured `X-Internal-Secret`. Callers must derive `userId` from a trusted gateway identity or persisted chat owner and `companyId` from verified membership, never from model arguments. The read checks current membership again and resolves only the user's exact assigned role, either a global default or a custom role owned by that company.

The response is an explicit allowlist: `{ "userId": "...", "companyId": "...", "role": { "id": "...", "name": "Финансист", "isDefault": false }, "observedAt": "..." }`. Unassigned role is `null`; no director fallback is applied. `observedAt` is the current observation time, not the assignment time, and this endpoint does not provide historical role state. Names, email, password hashes and permission grants are absent. Role names are framing data, not instructions or authorization.

Missing/invalid identifiers return 400; missing internal authentication returns 401; absent or different company membership returns 403; absent user returns 404. Unavailable membership or an unverifiable assigned role returns 502, never a successful `role: null`. The read does not create or modify roles, assignments, membership, or ACLs. Consumers may adapt questions/actions to role responsibilities while preserving canonical insight facts and existing source access checks.

Run the repository's DB-free regression runner with `dotnet run --project tests/ResourceSmokeTest/ResourceSmokeTest.csproj`. It covers the existing SQL resource checks plus viewer DTO allowlisting, null/custom/default roles, company scope, failures and internal-secret enforcement through fake repositories and the actual controller/service.
