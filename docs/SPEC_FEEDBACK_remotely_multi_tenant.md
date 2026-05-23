# Spec Review Feedback — Remotely Multi-Tenant Capability

**Reviewer:** Claude (fresh-context review pass)
**Reviewed spec:** `SPEC_remotely_multi_tenant.md` (Last Updated 2026-05-22T04:00:00Z)
**Review date:** 2026-05-22
**Review scope:** Spec document only — light code grounding against current `master` (commit `0f023d2d`) to validate concerns.

---

## Summary

The spec is unusually well-structured for one-shot implementation: clear scope boundaries, stable requirement numbering, Key Decisions with rationale, and explicit non-goals. The big architectural calls — KD-01 (scoped service over claims) and KD-02 (implicit server-admin) — are defensible and internally consistent.

The concerns below are gaps and ambiguities I'd want closed before handing this to an implementing agent, ordered roughly by severity. Items 1–4 are material (likely to bite during implementation); items 5–8 deserve explicit acknowledgement in the spec; items 9–10 are minor.

---

## Material gaps

### 1. `OrganizationAdminRequirementHandler` cannot reach `IActiveOrganizationContext`

§4.4 marks `OrganizationAdminRequirementHandler.cs` as "modified — use membership record," but auth handlers run in the ASP.NET Core HTTP authorization pipeline, not inside a Blazor Server circuit. They have no access to circuit-scoped services like `IActiveOrganizationContext`.

The current handler (verified against `Server/Auth/OrganizationAdminRequirementHandler.cs:24-31`) simply checks `userResult.Value.IsAdministrator` — a single user-level flag. Under the new model, "is this user an org admin" is meaningless without a target org.

The spec needs to explicitly answer:

- How does the handler determine *which* org to check admin status against?
- Is the target org pulled from a route value, a query parameter, a header, the request body?
- Is there one handler that works for both circuit-scoped (Blazor) and HTTP (API) callers, or are there two?
- Where does the spec assert that any endpoint protected by `OrganizationAdminRequirement` must carry an org-ID parameter the handler can read?

This is the load-bearing auth seam. Leaving the resolution mechanism implicit is the single biggest spec risk.

**Suggested resolution:** Add a new FR (e.g., FR-25) describing the handler's org-resolution rule, and add a sentence to §5.3 clarifying that the security boundary for non-Blazor callers does *not* go through `IActiveOrganizationContext`.

### 2. Refactor blast radius is wider than §8.1 admits

§8.1 enumerates `IDataService` methods that need call-site updates, framed around substituting `IActiveOrganizationContext` for `user.OrganizationID`. A spot-check of the current codebase shows `user.OrganizationID` is also read in non-circuit contexts:

| File | Context | Source of truth for org ID |
|---|---|---|
| `Server/Services/EmailSender.cs` (3 hits) | Background email composition | Probably needs explicit param |
| `Server/API/OrganizationManagementController.cs` | HTTP API controller | API token's `OrganizationID` |
| `Server/Components/Account/Pages/Register.razor` | Pre-auth registration flow | `InviteLink.OrganizationID` |
| `Server/Components/Layout/NavMenu.razor` | Blazor circuit-scoped | `IActiveOrganizationContext` |
| `Server/API/DevicesController.cs` and other API controllers | HTTP API controllers | API token header / route param |

None of these can use `IActiveOrganizationContext` (it doesn't exist in HTTP request scope, and at registration time the user isn't even authenticated).

**Suggested resolution:** Add a subsection to §8.1 (or a new §5.4) titled *"Sources of active org ID by caller type"* enumerating:

- Blazor circuit components → `IActiveOrganizationContext`
- HTTP API controllers with API token → token's `OrganizationID`
- HTTP API controllers with cookie auth → ??? (this case needs an explicit answer — there is no circuit and no token)
- Agent SignalR hubs → device's `OrganizationID`
- Registration flow → invitation's `OrganizationID`
- Background services (email, etc.) → explicit parameter from the originating context

Without this, the implementing agent will either invent ad-hoc rules per file or — worse — try to inject `IActiveOrganizationContext` into request-scoped services and fail at runtime.

### 3. DeviceGroup↔User join produces orphans on org removal

`Shared/Entities/DeviceGroup.cs:25` carries `List<RemotelyUser> Users` — an M:N join already in the schema. §6.1 dismisses any DeviceGroup change as "unaffected" because DeviceGroup carries `OrganizationID`, so group associations are "implicitly org-scoped."

This is not quite right. The join row itself is the membership: when a user is removed from org A (FR-20), entries in the `RemotelyUserDeviceGroup` join for DeviceGroups belonging to org A become orphans — the user has no membership in org A but is still linked to its DeviceGroups. Two concrete problems:

- **Cleanup:** `RemoveUserFromOrganization` (the new method in §8.1) must also strip the user's DeviceGroup assignments for that org. The spec doesn't say so.
- **Data-leak risk:** Any future code path that filters DeviceGroups via the user-join *without* a redundant org check could expose data from an org the user no longer belongs to.

It also surfaces a forward-looking question the spec is silent on: when a user is added to a *new* org (FR-08), do they get any default DeviceGroup assignments? Probably not, but worth saying explicitly.

**Suggested resolution:** Add an FR under §2.4 (e.g., FR-26): "Removing a user from an organization shall also remove all of that user's DeviceGroup assignments within that organization, in the same transaction." And add a sentence to §6.1 DeviceGroup paragraph acknowledging this cleanup.

### 4. No defined behavior when the active org is revoked mid-session

FR-14 specifies a SignalR notification on membership change, and the circuit refreshes. NFR-04 covers the *reconnect* path: if the previously-active org is no longer accessible, default to first available membership.

But the *live-revocation* path is undefined: org admin removes user U from org A while U has org A actively selected in their circuit. The user sees a toast — then what?

- Does the active org silently swap to first remaining membership? (Likely user-hostile — the device grid abruptly shows a different org's data.)
- Does the UI freeze at an "access revoked" empty state and force a manual re-selection?
- What if no memberships remain (last org was their only one and FR-24 didn't apply because there's a fallback server admin)?
- Does any in-flight action against org A get a graceful error, or a 403?

The spec needs an explicit FR covering this transition, or at minimum a sentence in §9.2 / NFR-04 covering both the reconnect and live-revocation cases.

**Suggested resolution:** Extend NFR-04 (or add a new FR in §2.6) to cover live revocation explicitly — including the zero-remaining-memberships case.

---

## Worth surfacing explicitly

### 5. API tokens are silently uncoupled from membership

§8.2 says the existing HTTP API is unchanged and that API tokens are org-scoped via `ApiToken.OrganizationID`. The implicit consequence: if user U creates an API token while a member of org A, then gets removed from org A, the token *continues to work* — it's scoped to the org, not the user.

This may be the intended behavior (token belongs to the org, like a service principal), but the spec treats it as a non-issue rather than a decision. It deserves a KD with the reasoning, or an FR specifying whether/how token lifecycle ties to membership changes.

**Suggested resolution:** Add KD-05 or an FR clarifying the intended token-vs-membership lifecycle.

### 6. `InviteLink.IsAdmin` migration loses data, contradicting FR-19's claim

FR-19 says "All existing user, organization, device group, and device data shall be preserved without loss." But the migration drops `InviteLink.IsAdmin`, and any unclaimed invitation with `IsAdmin=true` loses that intent.

This is probably fine per KD-04 (base-first), but FR-19's blanket "no data loss" claim is slightly overstated as written. Either acknowledge the exception in FR-19 or in §6.2 under the InviteLink schema change.

### 7. FR-15 permits orgs with zero explicit admins

FR-15 wording: *"prevent the last active administrator of an organization from being demoted or removed from that organization, unless at least one active server admin exists on the server."*

This means: as long as *any* server admin exists *anywhere on the server*, the last org admin of org X can be demoted, leaving org X with zero explicit administrators (coverage only via the implicit server-admin grant from KD-02).

Per KD-02 this is coherent. But it means concepts like "the primary admin contact for tenant org X" become undefined for affected orgs. Worth confirming this is the intended steady-state and adding a sentence to either KD-02 or FR-15 acknowledging it.

### 8. Cached `IActiveOrganizationContext` state vs. server authority

§5.3 correctly states the context is not a security boundary. But cached `IsOrgAdmin`/`CanInvite` bools will be read by the UI for button visibility and form gating. The spec should reinforce that *every* `IDataService` method must re-check authorization server-side, even when the call originates from a UI element gated by the cached flag.

The window between (a) admin revokes membership, (b) `MembershipChangedMessage` arrives, and (c) circuit refreshes is small but non-zero. Without explicit reinforcement, an implementing agent could reasonably treat a "trusted" circuit-side flag as sufficient and skip a redundant DB check.

**Suggested resolution:** Add a sentence to NFR-01 or §5.3 explicitly stating: "Authorization checks in `IDataService` methods MUST NOT trust the calling circuit's `IActiveOrganizationContext` state and MUST re-verify membership and role against the current database state."

---

## Minor

### 9. Tests project marked unaffected — likely overstated

§7.1 says: "All other projects (`Agent`, `Desktop.*`, `Tests`) are unaffected."

With every `IDataService` signature shifting, the auth handler changing, and a new migration introduced, the Tests project surface area will inevitably be touched. Better to call this out as "test updates required" than to discover it mid-implementation and treat it as scope creep.

### 10. OI-03 localStorage default has a Blazor Server prerender wrinkle

JS interop (used to read `localStorage`) cannot fire during Blazor Server prerendering or during `OnInitializedAsync` — it must wait until `OnAfterRenderAsync(firstRender: true)`. This means the first paint of the device grid will use the fallback default ("first membership by creation date") before the stored preference swaps in, causing a brief visible flicker for users whose preferred org isn't their first-by-creation.

This is likely tolerable, but the implementing agent should be aware. Worth a sentence in OI-03 (or wherever the implementation note is most discoverable).

---

## Things that look right

For balance, the items below are explicit calls that the spec gets right and that I'd resist if challenged:

- **KD-01 (scoped service over claims).** Correct call. Reissuing the auth cookie on every org switch would be painful.
- **KD-02 (implicit server-admin grant).** Correct call. The bookkeeping alternative is a maintenance trap.
- **KD-03 (invitations are new-user-only).** Clean separation. Direct-add for existing users matches the trust model.
- **KD-04 (new members get base privileges).** Safer default; admin elevates explicitly.
- **FR-24 (last membership prevention).** Good catch — would otherwise produce zombie accounts.
- **Number-stability convention in §1.9.** Genuinely useful for spec longevity.
- **OI-02 resolution (SQLite-only migration).** Pragmatic given the author's deployment.

---

## Recommended pre-implementation actions

In priority order, before handing this to an implementing agent:

1. Resolve gap **#1** with a concrete answer for how `OrganizationAdminRequirementHandler` determines its target org.
2. Add the "sources of active org ID by caller type" table from gap **#2**.
3. Add the DeviceGroup cleanup FR from gap **#3**.
4. Define live-revocation behavior from gap **#4**.
5. Make decisions on **#5** (API token lifecycle) and **#7** (zero-admin orgs steady state).
6. Soften FR-19's data-loss claim per **#6**.
7. Reinforce NFR-01 per **#8**.

Items 9 and 10 can be addressed in passing during implementation.

---

*End of feedback.*
