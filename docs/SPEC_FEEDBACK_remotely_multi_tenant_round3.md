# Spec Review Feedback (Round 3) — Remotely Multi-Tenant Capability

**Reviewer:** Claude (fresh-context review pass)
**Reviewed spec:** `SPEC_remotely_multi_tenant.md` (Last Updated 2026-05-23T00:00:00Z)
**Prior feedback:** `SPEC_FEEDBACK_remotely_multi_tenant.md` (Round 1), `SPEC_FEEDBACK_remotely_multi_tenant_round2.md` (Round 2)
**Review date:** 2026-05-23
**Review scope:** Verification of Round 2 fixes.

---

## Summary

Five of six Round 2 items are addressed cleanly. One critical item is **logged as fixed in the revision log but the actual text was not updated**, leaving the spec internally contradictory on a load-bearing detail. This single edit is the only blocker remaining.

---

## Round 2 verification matrix

| Round 2 item | Status | Notes |
|---|---|---|
| #1 §8.1 ApiToken methods contradict FR-28/KD-05 | **❌ NOT FIXED** | Revision log claims fix landed; §8.1 text unchanged |
| #2 §10 contradicts §8.2 | ✅ Fixed | §10 fully rewritten; breaking changes enumerated |
| #3 §2 section numbering | ✅ Fixed | Sections 2.1–2.10 now contiguous |
| #4 FR-30 precedence rule | ⚠ Partially fixed | Added to §10, not to FR-30 itself |
| #5 FR-31 integration point | ✅ Fixed | `ApiAuthorizationFilter` named in §10 |
| #6 §8.1 missing `JoinViaInvitation` / `AddInvite` | ✅ Fixed | New bullets at §8.1 lines 547–549 |

---

## Blocker — §8.1 ApiToken bullets still contradict FR-28 / KD-05

### What the revision log claims

Revision log entry dated `2026-05-23T00:00:00Z` (line 756):

> "§8.1 ApiToken method bullets corrected to reflect FR-28/KD-05: CreateApiToken, DeleteApiToken, GetAllApiTokens, RenameApiToken no longer take or filter by organizationId; tokens are user-scoped only."

### What §8.1 actually says (lines 524–530)

```
- `CreateApiToken(string userName, string tokenName, string secretHash)` —
  stamps `OrganizationID = user.OrganizationID` on the new token;
  shall accept an explicit `organizationId` parameter.
- `DeleteApiToken(string userName, string tokenId)` — scopes the token lookup
  by `user.OrganizationID`; shall accept an explicit `organizationId` parameter.
- `GetAllApiTokens(string userId)` — filters tokens by `user.OrganizationID`;
  shall accept an explicit `organizationId` parameter.
- `RenameApiToken(string userName, string tokenId, string tokenName)` —
  scopes the token lookup by `user.OrganizationID`;
  shall accept an explicit `organizationId` parameter.
```

These four bullets are **unchanged from the previous revision**. They still direct the implementer to add an `organizationId` parameter and continue org-scoping the token lookups — the exact opposite of what FR-28, KD-05, and §10 say.

### Why this is a blocker

The contradiction is now between two top-level sections of the spec:

- **FR-28 / KD-05 / §10** say: tokens are identity-only; `OrganizationID` is not used for new tokens; scoping is by user.
- **§8.1** says: the four ApiToken methods should take `organizationId` and filter by it.

§8.1 is the *authoritative method-change list* for the implementing agent. Reading it as written, the agent will produce code that filters tokens by org, which contradicts the rest of the spec and the migration plan in §6.2.

This is exactly the situation Round 2 flagged. The revision log says it was addressed, but the diff did not reach the actual document text.

### Required edit

Replace the four ApiToken bullets in §8.1 (lines 524–530) with text matching FR-28. Suggested wording:

```markdown
- `CreateApiToken(string userName, string tokenName, string secretHash)` —
  previously stamped `OrganizationID = user.OrganizationID`; shall set
  `OrganizationID = null` per FR-28. No `organizationId` parameter required;
  tokens are identity-only.
- `DeleteApiToken(string userName, string tokenId)` — previously scoped the
  lookup by `user.OrganizationID`; shall scope by `userId` only. Tokens belong
  to users, not orgs (FR-28).
- `GetAllApiTokens(string userId)` — previously filtered by
  `user.OrganizationID`; shall return all tokens belonging to `userId` without
  any org filter (FR-28).
- `RenameApiToken(string userName, string tokenId, string tokenName)` —
  previously scoped the lookup by `user.OrganizationID`; shall scope by
  `userId` only (FR-28).
```

Once this is in place, §8.1, FR-28, KD-05, §6.2, and §10 are all consistent.

---

## Minor items (non-blocking)

### M-1. FR-30 precedence rule lives in §10, not in FR-30 itself

The route-data-over-query-string precedence rule for `OrganizationAdminRequirementHandler` is correctly described in §10 (line 635) but not in FR-30 itself (line 216). An implementing agent reading FR-30 in isolation gets less than the full rule.

**Suggested edit:** Append to FR-30: "If both route data and query string contain `organizationId`, route data takes precedence. Request body values are not consulted."

### M-2. Revision log typo

Revision log line 756 says "renumbered 2.1–2.11 contiguously" but the actual sections are 2.1–2.10. Cosmetic, no impact on implementation.

---

## Verdict

**Not yet ready — one edit away.**

The §8.1 ApiToken bullet rewrite (the blocker above) is a single, surgical change but it is genuinely required: §8.1 currently directs the implementer to do the opposite of what FR-28 / KD-05 / §10 require for ApiToken handling.

Once those four bullets are rewritten to match FR-28, the spec is implementation-ready. The minor items M-1 and M-2 can be addressed inline by the implementing agent or deferred.

**Pre-implementation action list:**

1. **Must fix:** Replace the four ApiToken bullets in §8.1 (lines 524–530) per the suggested wording above. After the edit, verify that §8.1, FR-28, KD-05, §6.2, and §10 all agree.
2. *Nice to fix:* Append the precedence rule to FR-30 (M-1).
3. *Cosmetic:* Correct the revision log typo (M-2).

---

*End of Round 3 feedback.*
