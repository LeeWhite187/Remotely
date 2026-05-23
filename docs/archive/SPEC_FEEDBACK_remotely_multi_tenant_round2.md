# Spec Review Feedback (Round 2) — Remotely Multi-Tenant Capability

**Reviewer:** Claude (fresh-context review pass)
**Reviewed spec:** `SPEC_remotely_multi_tenant.md` (Last Updated 2026-05-22T05:00:00Z)
**Prior feedback:** `SPEC_FEEDBACK_remotely_multi_tenant.md` (Round 1)
**Review date:** 2026-05-23
**Review scope:** Re-review of revisions made in response to Round 1 feedback.

---

## Summary

The new revision addresses **every concern** from Round 1, and in two places the response is stronger than what was suggested:

- **API token decoupling (KD-05 / FR-28 / FR-31).** Round 1 flagged the lifecycle ambiguity and suggested a KD. The author chose the bolder route: tokens are now identity-only with `OrganizationID = null`, legacy tokens are forcibly invalidated on first use, and the column is retained only as a legacy marker (with future drop noted in OI-08). This is a cleaner architectural answer than the soft "make it a decision" path Round 1 suggested.
- **InviteLink migration (FR-19).** Round 1 suggested acknowledging the `IsAdmin` data loss; the author opted to delete all `InviteLink` rows in the migration as an intentional truncation. Cleaner — no half-migrated invitations carrying ambiguous semantics into the new model.

The architectural picture is now coherent. The auth-handler resolution rule (FR-30), the caller-type matrix (§5.4), the DeviceGroup cleanup (FR-26), the live-revocation behavior (FR-29), and the explicit reinforcement of `DataService` re-verification (NFR-01) all close real gaps cleanly.

However, the new content has introduced some **editorial and internal-consistency issues** because §8.1 and §10 were not updated alongside the new FR-28 / KD-05 / KD-06 decisions. None are architectural — they are places where two parts of the spec now disagree. They will confuse the implementing agent if not reconciled.

---

## Material issues (must fix before implementation)

### 1. §8.1 API-token methods contradict FR-28 / KD-05

The new token model in **KD-05** and **FR-28** states tokens are identity-only — they no longer carry an org ID, and new tokens are created with `OrganizationID = null`. But §8.1's enumeration of methods requiring call-site updates still describes the four token methods as requiring an `organizationId` parameter:

| Method | §8.1 current direction | Actual requirement per FR-28 / KD-05 |
|---|---|---|
| `CreateApiToken` | "shall accept an explicit `organizationId` parameter" | Should **not** take `organizationId`; `OrganizationID` is left null per FR-28 |
| `DeleteApiToken` | "shall accept an explicit `organizationId` parameter" | Should scope by `userId` only (tokens belong to users, not orgs) |
| `RenameApiToken` | "shall accept an explicit `organizationId` parameter" | Same as `DeleteApiToken` — scope by `userId` |
| `GetAllApiTokens` | "shall accept an explicit `organizationId` parameter" | Should return tokens by `userId` without org filter |

Without fixing this, the implementing agent will either invent a contradictory hybrid (org-filtered token lookups against a column that's now nullable and unused) or pick the wrong side of the contradiction.

**Suggested resolution:** Rewrite the four ApiToken bullets in §8.1 to reflect FR-28's identity-only model. The methods stop reading or writing `OrganizationID` and scope purely by `userId`.

### 2. §10 contradicts §8.2

§8.2 now correctly describes the API token change as a **breaking change**, with mandatory `organizationId` parameters on org-scoped endpoints and the legacy token invalidation path. But §10 (API Surface) still reads:

> "The existing Remotely HTTP API is unchanged by this project."

These cannot both be true. §10 was correct in v1 of the spec but is now stale.

**Suggested resolution:** Rewrite §10 to reflect that the API surface IS changing, specifically:
- Legacy token invalidation (FR-31)
- Mandatory `organizationId` parameter on org-scoped endpoints (FR-25, KD-06)
- The audit obligation on the implementing agent to update all org-scoped controllers (KD-06 Consequences)

`OrganizationManagementController` should also be called out specifically rather than handled with vague "may require internal modifications" language.

### 3. Section numbering bugs in §2

Two numbering issues introduced by the new sections:

- **`§2.6` is used twice.** Once for "Server Admin Organization Management" (FR-22, FR-23), once for "Request Org Context" (FR-25, FR-27, FR-30).
- **`§2.8` is skipped.** The sequence jumps from `§2.7 Claims and Session Security` to `§2.9 Lockout Prevention` to `§2.10 Schema Migration`.

Easy editorial fix; renumber so the section sequence is contiguous and unique.

---

## Worth flagging (minor — can be addressed inline by implementer)

### 4. FR-30 ambiguous on parameter precedence

FR-30 says the handler reads `organizationId` from "route data or query string parameter" without specifying precedence if both are present, or whether request-body values count. Likely "route data takes precedence over query string; body values not consulted" is the right rule, but it should be stated.

### 5. FR-31 integration point unspecified

FR-31 says legacy detection happens "before any other request processing" but doesn't name the hook. The existing codebase routes API token requests through `ApiAuthorizationFilter` (referenced via `[ServiceFilter(typeof(ApiAuthorizationFilter))]` on controllers like `DevicesController.cs`). Naming that filter as the integration point would remove implementation ambiguity.

### 6. §8.1 missing two methods affected by the new model

- **`JoinViaInvitation`** — referenced in §9.2 as the registration-time method that creates the user and membership and deletes the `InviteLink`. Behavior is described in prose but is not in §8.1's enumerated change list.
- **`AddInvite`** — needs its signature change called out: the `isAdmin` parameter should be dropped per KD-04.

§8.1 reads as the authoritative method-change list; these two omissions undermine that.

---

## Confirmation of Round 1 resolutions

For audit clarity, the following items from Round 1 are fully resolved in this revision:

| Round 1 item | Resolution in v2026-05-22T05:00:00Z |
|---|---|
| #1 `OrganizationAdminRequirementHandler` org resolution | FR-30 + KD-06 (universal explicit request parameter) |
| #2 Refactor blast radius beyond `DataService` | FR-25 + §5.4 (sources-of-org-ID matrix) |
| #3 DeviceGroup↔User join orphans | FR-26 + §6.1 DeviceGroup paragraph |
| #4 Live org-revocation behavior | FR-29 |
| #5 API token lifecycle | FR-28 + FR-31 + KD-05 (stronger than suggested) |
| #6 `InviteLink.IsAdmin` data loss | FR-19 exception clause (stronger than suggested — full row deletion) |
| #7 Zero-explicit-admin org steady state | KD-02 Consequences expanded |
| #8 Cached `IActiveOrganizationContext` vs server authority | NFR-01 reinforced with MUST language |
| #9 Tests project not unaffected | §7.1 updated |
| #10 OI-03 prerender JS interop timing | OI-03 Implementation Note added |

---

## Verdict

**Conditionally ready.** All original architectural concerns are resolved convincingly. The spec is internally complete on the hard questions.

What remains is a focused consistency pass through **§8.1, §10, and the §2 numbering** to align them with the new FR-28 / KD-05 / KD-06 decisions.

**Recommended pre-implementation actions:**

1. Fix the four ApiToken method bullets in §8.1 to match FR-28 (item #1 above) — **must fix**.
2. Rewrite §10 to reflect the API breaking changes (item #2 above) — **must fix**.
3. Renumber the duplicate `§2.6` and missing `§2.8` (item #3 above) — **must fix** (easy).
4. Address items #4–#6 inline; they are clarifications, not gaps.

Once items 1–3 are reconciled, the spec is implementation-ready.

---

*End of Round 2 feedback.*
