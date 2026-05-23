# Remotely Multi-Tenant Capability — Specification

**Project:** Remotely Multi-Tenant Capability
**Short description:** Extends the Remotely remote administration platform to allow a single user account to hold membership in multiple organizations, with per-org roles and a real-time org-switching UI.
**Author:** Lee
**Status:** In Design
**Created:** 2026-05-22T00:00:00Z
**Last Updated:** 2026-05-22T05:00:00Z
**Related Documents:** None.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Functional Requirements](#2-functional-requirements)
3. [Non-Functional Requirements](#3-non-functional-requirements)
4. [Constraints and Technology Decisions](#4-constraints-and-technology-decisions)
5. [Architectural Overview](#5-architectural-overview)
6. [Data Model](#6-data-model)
7. [Solution Structure](#7-solution-structure)
8. [Key Interfaces and Contracts](#8-key-interfaces-and-contracts)
9. [Protocols and Flows](#9-protocols-and-flows)
10. [API Surface](#10-api-surface)
11. [Infrastructure](#11-infrastructure)
12. [Key Decisions](#12-key-decisions)
13. [Open Items](#13-open-items)
14. [Revision Log](#14-revision-log)

---

## 1. Project Overview

### 1.1 Background

Remotely is an open-source remote control and remote scripting platform built with .NET 8, Blazor Server, and SignalR. It is self-hostable, MIT-licensed, and maintained at [immense/Remotely](https://github.com/immense/Remotely). The platform allows administrators to manage fleets of devices, execute scripts remotely, and perform attended and unattended remote control sessions through a web-based UI.

The platform's data model is organized around organizations. An organization is the primary tenancy boundary: it owns devices, device groups, users, scripts, logs, and branding. Remotely supports multiple organizations on a single server instance, controlled by the `MaxOrganizationCount` server configuration setting. This multi-org capability exists at the data level but has a significant limitation at the user level: each user account is bound to exactly one organization via a direct foreign key on the `RemotelyUser` entity. There is no mechanism for a single user to participate in more than one organization.

The author operates a self-hosted Remotely fork and uses it to manage devices for multiple independent tenants as well as their own personal device fleet. Each tenant corresponds to an organization on the server.

### 1.2 Purpose

This project extends Remotely's user and organization model to allow a single user account to hold membership in multiple organizations. It introduces per-org roles, a real-time org-switching UI, an invitation system for onboarding new users to an org, and the administrative safeguards necessary to prevent accidental lockout of org and server admins.

### 1.3 Scope

This project covers:

- Replacing the single-org FK on `RemotelyUser` with a many-to-many membership model.
- Per-org role tracking (administrator, invite privileges) on the membership record.
- A UI organization switcher that scopes all device and device group data to the selected org.
- A per-circuit scoped service holding the active org context for a connected Blazor Server session.
- Real-time SignalR notification to connected clients when their membership or role changes.
- An email-based invitation flow for onboarding new users to an organization.
- Direct-add capability for org admins to add existing users without an invitation.
- Lockout prevention for org admins and server admins.
- An EF Core migration delivering all schema changes without data loss.

This project does not cover cross-org device operations (e.g., moving or copying devices between organizations), invite expiry, or any changes to the agent, desktop client, or remote control protocol.

### 1.4 Problem Statement

A single Remotely user account is bound to exactly one organization. An operator managing multiple tenants must maintain a separate login per tenant, which is operationally cumbersome and does not reflect the real-world structure where one administrator serves multiple client organizations from a single identity.

Additionally, the `IsAdministrator` flag is stored on the user record rather than scoped to an organization, meaning it cannot vary per org — a user is either an admin everywhere or nowhere.

### 1.5 Goals

The primary goal is to allow a single Remotely user account to be a member of one or more organizations, with a role (administrator or member) that may differ across orgs. The user should be able to switch their active organization through a UI control without logging out, and the entire UI — devices, device groups, scripts, logs — should immediately reflect the selected org's data.

A secondary goal is to support controlled onboarding of users into organizations. Org admins and users with invite privileges should be able to invite new users by email, and directly add existing users without ceremony. The system should notify connected users in real time when their memberships or roles change.

A tertiary goal is to harden the admin model against accidental lockout. Every organization must always have at least one active administrator (or a server admin as fallback), and server admins must be protected from self-demotion and last-admin removal.

### 1.6 User Requirements

**UR-01 — Multi-org membership.** A user shall be able to belong to more than one organization.

**UR-02 — Org switching.** A user shall be able to switch their active organization from the UI without logging out and back in.

**UR-03 — Org-scoped data visibility.** A user shall see only devices and device groups belonging to their currently active organization.

**UR-04 — Per-org roles.** A user shall be able to have a different role in each organization they belong to.

**UR-05 — Invite new users.** An organization admin, or a user with invite privileges, shall be able to invite a new user to the server by email, with the invitation pre-associating them with the inviting organization.

**UR-06 — Direct add existing users.** An organization admin shall be able to add an existing user to their organization directly, without requiring the user's acceptance.

**UR-07 — Server admin global access.** A server admin shall be able to switch into any organization's context in the UI, regardless of explicit membership.

**UR-08 — Unclaimed invitation persistence.** Unclaimed invitations shall persist until claimed or manually deleted by an org admin, consistent with existing Remotely behavior.

### 1.7 Non-Goals

- **Cross-org device operations.** Moving or copying devices between organizations is not in scope. A device's `OrganizationID` is immutable within this project.
- **Invite expiry.** Automatic expiry of unclaimed invitations is not in scope. Invitations persist indefinitely, consistent with existing behavior (UR-08). This is noted as a potential future improvement in OI-01.
- **Invitation flow for existing users.** Existing users are added to orgs directly by an admin (FR-08). The email invitation flow is reserved for new users only.
- **Self-service org creation.** Users cannot create new organizations. Org creation remains a server admin function.
- **Cross-org aggregate views.** The UI will never show devices or data aggregated across multiple organizations simultaneously. Org context is always singular.
- **Agent and desktop client changes.** The agent, desktop client, and remote control protocol are unaffected by this project.

### 1.8 Glossary

- **Organization** — The primary tenancy boundary in Remotely. Owns devices, device groups, users, scripts, logs, and branding. See §6.1.
- **Active organization** — The organization whose data the user is currently viewing and acting within. One per connected circuit. See §5.1.
- **Circuit** — A Blazor Server persistent connection between a browser tab and the server. Each circuit has its own scoped service instances, including the active org context.
- **UserOrganizationMembership** — The join entity replacing the `OrganizationID` FK on `RemotelyUser`. Carries per-org role information. See §6.1.
- **Server admin** — A user with `IsServerAdmin = true`. Has implicit full org admin privileges in every organization without requiring membership. See FR-18.
- **Org admin** — A user whose `UserOrganizationMembership.IsAdministrator` is `true` for the active organization, or any server admin.
- **Invite privilege** — A per-membership flag (`CanInvite`) that allows a non-admin member to send email invitations to new users.

### 1.9 Spec Conventions

**Item identifiers.** Items in this spec are identified by a type prefix and a two-digit zero-padded sequence number:

- **UR-NN** — User Requirement
- **FR-NN** — Functional Requirement
- **NFR-NN** — Non-Functional Requirement
- **KD-NN** — Key Decision
- **OI-NN** — Open Item

Sequence numbers are assigned in the order items are *created*, not the order they appear in the document. Items may be moved within the document as the spec evolves; their sequence number does not change once assigned.

**Number stability.** Item numbers are stable across all revisions of the spec. A removed item is not deleted from its section — it remains in place with title `### XX-NN — (withdrawn)` and a brief note explaining the removal. New items always take `max(existing_number) + 1` for their type, where the maximum includes all withdrawn entries. Numbers are addresses; addresses are not recycled.

**Cross-references.** Sections are referenced by number (e.g., `§6.5`). Items are referenced by their full identifier (e.g., `FR-14`, `KD-03`).

**Document order is independent of item numbering.** A section may contain `FR-03, FR-15, FR-04, FR-22` in that order because requirements are grouped topically, not by creation order. The identifier is the address; the position is the organization.

**Open Item disposition flags.** Open Items carry one of three dispositions, indicated in the item's title:

- `⚠ NEEDS YOUR INPUT` — requires the project owner to make a decision.
- `⚠ NEEDS YOUR REVIEW` — the author has a recommendation; the project owner reviews before commit.
- (no flag) — known gap, intentionally not addressed at this stage of the spec.

**The template is a superset.** Sections that do not apply remain in place with a brief design statement explaining why.

**Requirement language.** Requirements use RFC 2119 vocabulary: SHALL / MUST — mandatory; SHOULD — strong recommendation; MAY — optional.

**Timestamp consistency.** The `Last Updated` field in the title block must match the timestamp of the most recent entry in the Revision Log (§14).

---

## 2. Functional Requirements

### 2.1 Membership Model

**FR-01 — Replace org FK with membership entity.** The system shall replace the `OrganizationID` foreign key on `RemotelyUser` with a `UserOrganizationMembership` join entity linking users to organizations. The `OrganizationID` property and the `Organization` navigation property shall be removed from `RemotelyUser`.

**FR-02 — Membership entity fields.** The `UserOrganizationMembership` entity shall carry `UserId`, `OrganizationId`, `IsAdministrator`, and `CanInvite`. The entity shall be designed to accommodate additional per-membership permissions in future without schema redesign.

**FR-10 — Remove IsAdministrator from user.** The `IsAdministrator` flag shall be removed from `RemotelyUser`. Administrator status for an org shall be determined solely from the `UserOrganizationMembership` record for the active organization, or by `IsServerAdmin` per FR-18.

**FR-11 — Preserve IsServerAdmin on user.** The `IsServerAdmin` flag shall remain on `RemotelyUser` unchanged. It is a user-level, not org-scoped, property.

**FR-18 — Server admin implicit org privileges.** A user with `IsServerAdmin = true` shall have full organization administrator privileges in every organization on the server, without requiring an explicit `UserOrganizationMembership` record.

### 2.2 Active Organization Context

**FR-03 — Per-circuit active org context.** The system shall maintain a per-circuit active organization context, implemented as a scoped Blazor Server service, holding the organization ID currently selected by the user for the lifetime of that circuit.

**FR-06 — Org-scoped queries.** All device and device group queries shall be scoped to the active organization context rather than any stored organization ID on the user record.

**FR-07 — Context switch validation.** The system shall validate on context switch that the requesting user is either a member of the target organization or a server admin before accepting the switch. If validation fails, the switch shall be rejected and the active org context shall remain unchanged.

### 2.3 Organization Switcher UI

**FR-04 — Org switcher for regular users.** The system shall provide an organization switcher in the UI, populated with only the organizations the current user has explicit membership in, that updates the active organization context on selection.

**FR-05 — Org switcher for server admins.** For server admins, the organization switcher shall be populated with all organizations on the server, regardless of explicit membership.

### 2.4 User Onboarding

**FR-08 — Direct add existing user.** An organization admin shall be able to add an existing user to their organization directly, without requiring the user's acceptance, creating a `UserOrganizationMembership` record with base member privileges (`IsAdministrator = false`, `CanInvite = false`) immediately.

**FR-09 — Email invitation for new users.** An organization admin, or a user with `CanInvite = true`, shall be able to invite a new user by email. The new user's account shall be created and a `UserOrganizationMembership` record shall be created for the inviting organization upon registration, with base member privileges.

**FR-12 — Invitations for new users only.** The `InviteLink` entity and invitation flow shall be scoped to new users only. Adding an existing user to an organization is performed via direct add (FR-08), not via invitation.

**FR-20 — Remove user from org.** An organization admin shall be able to remove a user from their organization, deleting that user's `UserOrganizationMembership` record. This shall not delete the user's account. Removal shall be subject to FR-24 (last membership prevention) and FR-15 (last org admin prevention). The existing full account deletion action is preserved as a separate operation.

**FR-24 — Last membership removal prevention.** The system shall prevent removal of a `UserOrganizationMembership` record if it is the user's last membership and the user is not a server admin. The operation shall be rejected with a message directing the admin to delete the user account instead.

**FR-26 — DeviceGroup assignment cleanup on org removal.** Removing a user from an organization (FR-20) shall also remove all of that user's DeviceGroup assignments within that organization, in the same database transaction. A user added to a new org receives no default DeviceGroup assignments; their visible devices are determined by subsequent explicit group assignments by an org admin.

**FR-21 — Grant and revoke invite privilege.** An organization admin shall be able to grant or revoke `CanInvite` on any membership record within their organization, subject to the lockout prevention rules in FR-15.

### 2.5 API Token Changes

**FR-28 — ApiToken org divorce.** `ApiToken.OrganizationID` shall be made nullable in the schema. New API tokens shall be created with `OrganizationID = null`. API tokens authenticate user identity only; the target org is supplied per-request via an explicit `organizationId` parameter (FR-25).

**FR-31 — Legacy token invalidation.** On receipt of any API token where `OrganizationID` is non-null (a legacy token), the server shall immediately invalidate that token (delete or revoke it), return HTTP 401, and include a response message directing the client to log in again and generate a new token. This applies regardless of whether the legacy token is otherwise valid or expired. Legacy token detection and invalidation shall occur before any other request processing.

### 2.6 Server Admin Organization Management

**FR-22 — Server admin org listing.** A server admin shall be able to view a list of all organizations on the server, including organization name, ID, member count, and device count.

**FR-23 — Server admin org creation.** A server admin shall be able to create a new organization from the server admin UI, specifying the organization name.

### 2.6 Request Org Context

**FR-25 — Explicit org ID on every request.** Every request that requires org context shall carry an explicit `organizationId` parameter. The source of that value depends on the caller type:

- **Blazor circuit components** — supplied by `IActiveOrganizationContext`, passed explicitly to `DataService` methods.
- **HTTP API calls (API token auth)** — supplied as a route or query parameter by the API caller. `ApiToken.OrganizationID` is not used for this purpose (see FR-28).
- **HTTP API calls (cookie auth)** — supplied as a route or query parameter. There is no circuit in an HTTP request context; `IActiveOrganizationContext` is not available.
- **Agent SignalR hubs** — the device's own `OrganizationID` is the org context; this is not the active-user org and is not affected by this project.
- **Registration flow** — the `InviteLink.OrganizationID` supplies the org context before the user is authenticated.
- **Background services (e.g., email sender)** — org ID is passed as an explicit parameter from the originating call context.

`IActiveOrganizationContext` is a Blazor Server circuit-scoped service. It MUST NOT be injected into HTTP controllers, auth handlers, background services, or any non-circuit context. See §5.3.

**FR-27 — Server-side org membership validation on every request.** For every request carrying an `organizationId`, the server shall verify that the authenticated user is a member of the specified organization (or is a server admin) before acting. This check is performed in `DataService` methods, not solely in the auth handler or the circuit-side context. See NFR-01.

**FR-30 — OrganizationAdminRequirementHandler org resolution.** The `OrganizationAdminRequirementHandler` shall resolve the target org ID from the HTTP request route data or query string parameter named `organizationId`. It shall then check the authenticated user's `UserOrganizationMembership` for that org, or fall back to `IsServerAdmin`. If no `organizationId` is present in the request, the handler shall deny access. This handler applies to HTTP pipeline authorization only; Blazor circuit pages gate org-admin access through `IActiveOrganizationContext.IsOrgAdmin` and `DataService` method checks.

### 2.7 Claims and Session Security

**FR-13 — Thin claims.** User claims shall contain only identity and `IsServerAdmin`. Org membership and per-org roles shall not be stored in claims or cookies. They shall be loaded into a per-circuit scoped security service on connection and refreshed on membership changes.

**FR-14 — Real-time membership notification.** When a user's org membership or per-org role changes, the server shall notify any connected circuits for that user via SignalR. The circuit shall refresh its local security state and display a toast notification to the user only if the refresh reveals a change from the previously held state.

**FR-29 — Live org revocation behavior.** If a user's active organization is revoked while they have an active circuit (i.e., their `UserOrganizationMembership` for the currently active org is deleted), the circuit shall detect this via the `MembershipChangedMessage` refresh cycle (FR-14), display a warning toast notifying the user that their access to the organization has been revoked, and present the org switcher prominently to prompt selection of a new active org. The UI shall not silently switch to another org. If no memberships remain (only possible if FR-24 was bypassed due to a server admin acting as fallback), the circuit shall display an access-revoked full-page notice directing the user to contact the server admin.

### 2.9 Lockout Prevention

**FR-15 — Org admin lockout prevention.** The system shall prevent the last active administrator of an organization from being demoted or removed from that organization, unless at least one active server admin exists on the server.

**FR-16 — Server admin last-admin lockout prevention.** The system shall prevent `IsServerAdmin` from being removed from a user if they are the last active server admin on the server.

**FR-17 — Server admin self-demotion prevention.** A server admin shall not be permitted to remove their own `IsServerAdmin` flag, regardless of how many other active server admins exist.

### 2.10 Schema Migration

**FR-19 — EF Core migration.** The schema changes required by this project shall be delivered as an EF Core migration. All existing user, organization, device group, and device data shall be preserved without loss. Existing single-org user records shall be migrated to `UserOrganizationMembership` records carrying their existing `IsAdministrator` value. **Exception:** all existing `InviteLink` records shall be deleted as part of the migration; unclaimed invitations are not forward-compatible with the new invitation model (see KD-04 and §6.2). This is an intentional data truncation, not an unintended loss.

---

## 3. Non-Functional Requirements

### 3.1 Performance

Not applicable: this project introduces no new query patterns beyond the addition of a membership join. The existing data access patterns remain structurally similar. No specific latency or throughput targets are introduced by this project.

### 3.2 Reliability and Availability

**NFR-04 — Circuit resilience.** Loss or reconnection of a Blazor Server circuit shall not corrupt the active organization context. On reconnection, the circuit shall reload membership state from the server and restore a valid active org context. If the previously active org is no longer accessible to the user, the circuit shall default to the first available membership. For the live-revocation case (active org removed while circuit is connected), behavior is defined in FR-29.

### 3.3 Security

**NFR-01 — Server-side authorization authority.** All membership and permission checks shall be performed server-side on every request. The client-side scoped security service is a reflection of server state for UX purposes only and shall never be the sole authority for access control decisions. Authorization checks in `IDataService` methods MUST NOT trust the calling circuit's `IActiveOrganizationContext` state and MUST re-verify membership and role against the current database state on every call. The window between a membership change event and circuit refresh is non-zero; stale circuit state must never result in unauthorized data access.

**NFR-02 — Org data isolation.** A user shall never be able to retrieve devices, device groups, scripts, logs, or any other org-scoped data from an organization other than their currently active organization, regardless of how requests are constructed.

**NFR-03 — Lockout prevention atomicity.** The system shall enforce org admin and server admin lockout prevention rules within a single database transaction, such that no concurrent operation can result in an org or the server having no active administrator. This shall hold across all supported database backends (SQLite, SQL Server, PostgreSQL).

### 3.4 Observability

**NFR-05 — Membership audit logging.** Changes to org membership and per-org roles should be recorded in the existing Remotely server log with sufficient detail to reconstruct who changed what, when, and for which user. This is a SHOULD; it shall not block other work if deferred to a follow-on pass.

### 3.5 Accessibility

Not applicable: this project introduces one new UI control (the org switcher). Accessibility of that control should follow the existing UI conventions of the Remotely Blazor Server frontend. No new accessibility targets are introduced by this project.

### 3.6 Regulatory and Compliance

Not applicable: Remotely is an internal self-hosted tool with no PII beyond user email addresses and device names. No regulatory constraints apply to this project.

### 3.7 Compatibility

**NFR-06 — Schema migration without data loss.** The EF Core migration shall preserve all existing data (see FR-19).

**NFR-07 — No regressions to unrelated functionality.** Existing functionality unrelated to the user and org model — remote control, script execution, device management, alerts, branding, API tokens — shall be unaffected by these changes.

---

## 4. Constraints and Technology Decisions

### 4.1 Tech Stack

These constraints are inherited from the existing Remotely codebase and are not negotiable within this project.

**CT-01 — .NET 8 / C#.** The system is implemented in C# targeting .NET 8.

**CT-02 — ASP.NET Core / Blazor Server.** The server is an ASP.NET Core application. The UI is Blazor Server; UI state lives in server-side circuits.

**CT-03 — SignalR.** Real-time communication between server and connected clients uses SignalR, which is already present in the codebase for agent and remote control communication.

**CT-04 — Entity Framework Core.** The data layer uses EF Core with support for SQLite, SQL Server, and PostgreSQL backends. The active backend is determined by configuration at runtime.

**CT-05 — ASP.NET Core Identity.** Identity and authentication are provided by ASP.NET Core Identity. `RemotelyUser` inherits from `IdentityUser`. Changes to the user model must be compatible with the Identity framework's constraints.

### 4.2 Library Dependencies

No new library dependencies are introduced by this project. All required capabilities (EF Core migrations, SignalR, Blazor Server scoped services, ASP.NET Core Identity) are already present in the solution.

### 4.3 Environment Constraints

**CT-06 — SQLite migration.** The EF Core migration shall be authored and tested against SQLite, which is the author's deployed backend. SQL Server and PostgreSQL compatibility is deferred per OI-02.

### 4.4 Repository Structure

This project modifies the existing Remotely solution structure rather than introducing new projects. The affected projects are:

```
Remotely/
  Shared/
    Entities/
      RemotelyUser.cs           [modified — remove OrganizationID FK, IsAdministrator]
      Organization.cs           [modified — update navigation properties]
      UserOrganizationMembership.cs   [new entity]
      InviteLink.cs             [modified — scope to new users only; all rows deleted in migration]
      ApiToken.cs               [modified — OrganizationID made nullable]
  Server/
    Data/
      AppDb.cs                  [modified — new entity registration, updated model config]
      Migrations/               [new migration]
    Services/
      DataService.cs            [modified — all org-scoped queries updated]
      IActiveOrganizationContext.cs   [new interface]
      ActiveOrganizationContext.cs    [new scoped service]
    Auth/
      OrganizationAdminRequirementHandler.cs  [modified — use membership record]
    Components/
      Shared/
        OrgSwitcher.razor       [new UI component]
```

No new projects are added to the solution. All changes are contained within `Shared` and `Server`.

### 4.5 Non-Requirements (Explicit Exclusions)

- **Invite expiry.** Invitations do not expire automatically. This matches existing behavior and is deferred as OI-01.
- **Cross-org device operations.** Device `OrganizationID` is immutable within this project.
- **Self-service org creation.** Out of scope; remains a server admin function.
- **Horizontal scaling / distributed session.** The active org context is per-circuit in-process state. No distributed session store is introduced.

---

## 5. Architectural Overview

### 5.1 Tiers and Components

The existing Remotely architecture is three-tier: a Blazor Server web application (server process), an EF Core data layer (same process), and a database (external). Agents and desktop clients connect to the server via SignalR over HTTPS.

This project introduces changes to two of those tiers:

**Scoped security service (new, server process).** A new `IActiveOrganizationContext` scoped service, one instance per Blazor Server circuit, holds the active organization ID for the user's current session. It is injected into components and data service calls wherever org context is required. It is populated on circuit initialization from the user's membership records and updated when the user switches orgs via the switcher UI.

**Membership data layer (modified, server process).** The `DataService` is updated to read org context from `IActiveOrganizationContext` rather than from `RemotelyUser.OrganizationID`. The new `UserOrganizationMembership` entity is added to `AppDb` and participates in all membership-related queries.

**Org switcher UI (new, Blazor Server component).** A new Blazor component rendered in the navigation bar. Populated from the user's membership list (or all orgs for server admins). On selection, it calls `IActiveOrganizationContext.SetActiveOrganization`, which triggers a cascading state change that re-renders the device grid and other org-scoped UI.

**SignalR membership notification (new, server process).** When `DataService` modifies a user's membership or role, it pushes a SignalR message to any connected circuits for that user. Each circuit's `IActiveOrganizationContext` handles the message by refreshing from the database and, if state has changed, triggering a toast notification and a UI refresh.

### 5.2 Data Flow

**Org context initialization.** When a Blazor circuit connects, `IActiveOrganizationContext` is initialized. It queries `UserOrganizationMembership` for the authenticated user and selects a default active org (see OI-03 for default selection policy). The context is available to all components in the circuit's DI scope for the lifetime of the connection.

**Org switch.** The user selects an org from the switcher. The component calls `IActiveOrganizationContext.SetActiveOrganization(orgId)`. The service validates that the user is a member (or is a server admin), updates the held org ID, and raises a state change event. Components subscribed to the event re-render. The device grid re-fetches using the new org ID. No token or cookie is reissued.

**Membership change (server-initiated).** An org admin adds a user or changes their role via `DataService`. After persisting the change, `DataService` pushes a SignalR notification to all connected circuits for the affected user. Each circuit's `IActiveOrganizationContext` handles the notification, re-queries its membership state, and if a change is detected, raises a toast and updates the local state.

**Device query.** Any call to `DataService.GetDevicesForUser` (or equivalent) resolves the org ID from `IActiveOrganizationContext` rather than from `user.OrganizationID`. The query logic otherwise remains structurally unchanged.

### 5.3 Security Boundary

The `IActiveOrganizationContext` is a convenience layer for the UI. It is not a security boundary. Every `DataService` method that accepts an org ID shall independently verify that the calling user has membership in (or server admin access to) the specified org. The context can be wrong or stale; the data layer must not trust it blindly.

`IActiveOrganizationContext` is registered as a Blazor Server circuit-scoped service. It is only available within the Blazor circuit's DI scope. It MUST NOT be injected into or relied upon by: HTTP API controllers, ASP.NET Core authorization handlers (including `OrganizationAdminRequirementHandler`), background services, SignalR agent hubs, or the registration flow. These callers resolve org context from the request itself (FR-25). See §8.1 for the caller-type matrix.

### 5.4 Sources of Active Org ID by Caller Type

| Caller type | Source of org ID | Notes |
|---|---|---|
| Blazor circuit component | `IActiveOrganizationContext.ActiveOrganizationId` | Passed explicitly to DataService |
| HTTP API — API token auth | Route or query param `organizationId` | Token carries no org ID (FR-28) |
| HTTP API — cookie auth | Route or query param `organizationId` | No circuit available |
| `OrganizationAdminRequirementHandler` | Route data / query param `organizationId` | FR-30 |
| Agent SignalR hub | `device.OrganizationID` | Unaffected by this project |
| New user registration | `InviteLink.OrganizationID` | Pre-auth; no circuit |
| Background/email services | Explicit parameter from originating context | Never from IActiveOrganizationContext |

---

## 6. Data Model

### 6.1 Entity Overview

**RemotelyUser (modified).** The `OrganizationID` FK and `Organization` navigation property are removed. `IsAdministrator` is removed. `IsServerAdmin` remains. A new `Memberships` navigation property (`ICollection<UserOrganizationMembership>`) is added.

**UserOrganizationMembership (new).** The join entity between `RemotelyUser` and `Organization`. Each record represents one user's membership in one org. Carries `IsAdministrator` (org-scoped) and `CanInvite`. Created when a user is directly added to an org or when they accept an invitation. Deleted when a user is removed from an org.

**Organization (modified).** The `RemotelyUsers` navigation property changes from `ICollection<RemotelyUser>` to `ICollection<UserOrganizationMembership>`. All other organization-owned collections are unchanged.

**InviteLink (modified).** The `IsAdmin` field is removed, since invited users always receive base member privileges (FR-08, FR-09). The entity otherwise remains structurally unchanged. Invitations are for new users only (FR-12).

**DeviceGroup (unchanged structurally).** The many-to-many between `RemotelyUser` and `DeviceGroup` remains. Since `DeviceGroup` already carries `OrganizationID`, group associations are implicitly org-scoped. The join table structure is unaffected. However, when a user is removed from an org (FR-20), all rows in the `RemotelyUserDeviceGroup` join table linking that user to DeviceGroups belonging to that org must be deleted in the same transaction (FR-26). Failure to do so leaves orphaned join rows that could produce data-access anomalies if the join is ever queried without a redundant org check.

**ApiToken (modified).** `ApiToken.OrganizationID` is made nullable. New tokens are created with `OrganizationID = null`. Legacy tokens (non-null `OrganizationID`) are detected on first use, immediately invalidated, and the caller is directed to re-authenticate (FR-28, FR-31). The `OrganizationID` column is retained in the schema for legacy detection purposes; it is dropped in a future migration once all legacy tokens have been cycled out (OI-08).

**Device (unchanged).** `Device.OrganizationID` and `Device.DeviceGroupID` are unchanged.

### 6.2 Schema

**UserOrganizationMembership — new table**

| Column | Type | Constraints |
|---|---|---|
| Id | string | PK, server-assigned |
| UserId | string | FK → RemotelyUsers.Id, required |
| OrganizationId | string | FK → Organizations.Id, required |
| IsAdministrator | bool | required, default false |
| CanInvite | bool | required, default false |

Unique constraint on `(UserId, OrganizationId)` — a user may have at most one membership record per org.

**RemotelyUsers — modified**

Columns removed: `OrganizationID`, `IsAdministrator`.
No columns added (membership is in the new table).

**InviteLinks — modified**

Column removed: `IsAdmin`. All existing rows deleted as part of the migration (FR-19).

**ApiTokens — modified**

`OrganizationID` column type changed from `string NOT NULL` to `string NULL`. Existing token rows retain their current `OrganizationID` value; this non-null value is what marks them as legacy tokens subject to FR-31. New tokens are inserted with `OrganizationID = null`.

### 6.3 Identifiers

`UserOrganizationMembership.Id` follows the existing Remotely convention: a server-assigned string (EF Core `DatabaseGeneratedOption.Identity`), consistent with `Organization.ID`, `DeviceGroup.ID`, and `InviteLink.ID`.

`UserId` and `OrganizationId` are string FKs consistent with `IdentityUser.Id` and `Organization.ID`.

### 6.4 Versioning and Soft Delete

Not applicable: Remotely does not use soft delete or optimistic concurrency versioning for these entities. Membership records are hard-deleted when a user is removed from an org, consistent with existing Remotely data model conventions.

### 6.5 Data Retention

Membership records are retained for the lifetime of the association. When a user is removed from an org, the `UserOrganizationMembership` record is hard-deleted immediately. When a user account is deleted, all their membership records are cascade-deleted. These behaviors are consistent with how the existing `OrganizationID` FK is handled today.

---

## 7. Solution Structure

### 7.1 Libraries and Projects Overview

This project does not add new projects to the solution. The existing project structure is preserved:

**Remotely.Shared** — Entities, view models, shared models. This project receives `UserOrganizationMembership` (new entity), modifications to `RemotelyUser`, `Organization`, and `InviteLink`.

**Remotely.Server** — ASP.NET Core application, Blazor Server UI, data service, auth handlers, migrations. This project receives `IActiveOrganizationContext`, `ActiveOrganizationContext`, the org switcher component, updates to `AppDb`, `DataService`, and `OrganizationAdminRequirementHandler`.

**Remotely.Tests** — The tests project will require updates. Every `IDataService` method signature that changes (see §8.1), the new auth handler behavior (FR-30), and the new migration (FR-19) will affect existing test coverage. The implementing agent should update or add tests as part of each change rather than treating the tests project as out of scope.

`Agent` and `Desktop.*` projects are unaffected.

### 7.2 Library Boundary Rationale

The existing library split is preserved unchanged. Entities belong in `Shared` because they are referenced by both the server and (in principle) any future client. Services, DI registrations, and Blazor components belong in `Server`. No new cross-project dependencies are introduced.

### 7.3 Reference Rules

Existing reference rules are unchanged:

- `Shared` references nothing in the solution.
- `Server` references `Shared`.
- No other project references `Server`.

---

## 8. Key Interfaces and Contracts

### 8.1 Internal Interfaces

**IActiveOrganizationContext**

The central seam introduced by this project. Scoped to a Blazor Server circuit. Holds the currently selected org ID and the user's resolved membership state for that circuit.

```csharp
public interface IActiveOrganizationContext
{
    // The currently active organization ID. Null until initialized.
    string? ActiveOrganizationId { get; }

    // The user's membership record for the active org.
    // Null if the user is a server admin acting in an org without membership.
    UserOrganizationMembership? ActiveMembership { get; }

    // True if the current user is an org admin in the active org,
    // either via membership or via IsServerAdmin.
    bool IsOrgAdmin { get; }

    // True if the current user can invite new users in the active org.
    bool CanInvite { get; }

    // All orgs available to this user in the switcher.
    IReadOnlyList<Organization> AvailableOrganizations { get; }

    // Switch the active org. Validates membership or server admin status.
    // Returns false if the switch is rejected.
    Task<bool> SetActiveOrganizationAsync(string organizationId);

    // Called by the SignalR notification handler to refresh membership state.
    // Returns true if state changed (triggers toast).
    Task<bool> RefreshAsync();

    // Raised when active org or membership state changes.
    event Action? StateChanged;
}
```

**IDataService (modified)**

The following existing methods read `user.OrganizationID` directly from the user entity and must be updated to receive the active org ID as an explicit parameter instead. The parameter already exists on most of these signatures; the change is at the call site, where the caller supplies the value from `IActiveOrganizationContext` rather than from the user record.

Methods requiring call-site update (org ID must come from `IActiveOrganizationContext`, not `user.OrganizationID`):

- `AddOrUpdateSavedScript(SavedScript script, string userId)` — sets `script.OrganizationID = user.OrganizationID`; shall accept an explicit `organizationId` parameter instead.
- `CreateApiToken(string userName, string tokenName, string secretHash)` — stamps `OrganizationID = user.OrganizationID` on the new token; shall accept an explicit `organizationId` parameter.
- `DeleteApiToken(string userName, string tokenId)` — scopes the token lookup by `user.OrganizationID`; shall accept an explicit `organizationId` parameter.
- `DoesUserHaveAccessToDevice(string deviceId, RemotelyUser remotelyUser)` — compares `device.OrganizationID == remotelyUser.OrganizationID`; shall compare against an explicit `organizationId` parameter.
- `GetAllApiTokens(string userId)` — filters tokens by `user.OrganizationID`; shall accept an explicit `organizationId` parameter.
- `GetDeviceGroups(string username)` — filters device groups by `user.OrganizationID` and checks `user.IsAdministrator`; shall accept explicit `organizationId` and `isOrgAdmin` parameters.
- `GetDevicesForUser(string userName)` — scopes the admin device query by `user.OrganizationID`; shall accept an explicit `organizationId` parameter.
- `RenameApiToken(string userName, string tokenId, string tokenName)` — scopes the token lookup by `user.OrganizationID`; shall accept an explicit `organizationId` parameter.
- `FilterUsersByDevicePermissionInternal(AppDb, IEnumerable<string> userIDs, string deviceID)` — private method that filters org users by `user.OrganizationID == device.OrganizationID`; this comparison is against `device.OrganizationID` (correct — device owns its org) and does not need an active-org parameter, but the method must be verified to not rely on the user's stored org ID.

The following new methods shall be added to `IDataService`:

- `Task<Result> AddUserToOrganization(string orgId, string userId)` — creates a `UserOrganizationMembership` record with base privileges. Enforces FR-24 (last membership prevention is not applicable on add; this is an add, not a remove).
- `Task<Result> RemoveUserFromOrganization(string orgId, string userId)` — deletes the `UserOrganizationMembership` record. Enforces FR-15 (last org admin) and FR-24 (last membership).
- `Task<Result> SetMemberIsAdmin(string orgId, string userId, bool isAdmin)` — updates `UserOrganizationMembership.IsAdministrator`. Enforces FR-15.
- `Task<Result> SetMemberCanInvite(string orgId, string userId, bool canInvite)` — updates `UserOrganizationMembership.CanInvite`.
- `Task<UserOrganizationMembership?> GetMembership(string orgId, string userId)` — retrieves a single membership record.
- `Task<IReadOnlyList<UserOrganizationMembership>> GetMembershipsForUser(string userId)` — retrieves all memberships for a user; used by `IActiveOrganizationContext` on circuit initialization.
- `Task<IReadOnlyList<Organization>> GetAllOrganizations()` — retrieves all orgs; used by the server admin switcher (FR-05) and the server admin org listing page (FR-22).
- `Task<Result<Organization>> CreateOrganization(string organizationName)` — creates a new org (FR-23).

The existing `ChangeUserIsAdmin(string organizationId, string targetUserId, bool isAdmin)` method is superseded by `SetMemberIsAdmin` above and shall be removed.

### 8.2 External Contracts

The existing Remotely HTTP API (`/swagger`) is updated by this project in the following ways:

- **API token org scope removed.** `ApiToken.OrganizationID` is made nullable (FR-28). All API endpoints that previously derived org context from the token must now accept an explicit `organizationId` route or query parameter. This is a **breaking change** for existing API consumers; see FR-31 for the legacy token handling that signals re-authentication.
- **Org ID required on org-scoped endpoints.** Any endpoint that operates on org-scoped data must accept an `organizationId` parameter and validate the caller's membership in that org server-side (FR-27).
- **Endpoint paths and HTTP methods** are otherwise unchanged. The Swagger surface is modified only to add the `organizationId` parameter where missing.

### 8.3 DTOs and Wire Types

**OrganizationUser (modified view model).** The existing `OrganizationUser` view model (`ID`, `UserName`, `IsAdmin`) is updated to reflect that `IsAdmin` is now sourced from `UserOrganizationMembership.IsAdministrator` rather than `RemotelyUser.IsAdministrator`.

**MembershipChangedMessage (new message record)**

The existing codebase uses `Bitbound.SimpleMessenger` for intra-circuit messaging (record types in `Server/Models/Messages/`). The membership notification follows the same pattern rather than introducing a new SignalR hub method.

```csharp
// Server/Models/Messages/MembershipChangedMessage.cs
namespace Remotely.Server.Models.Messages;

public record MembershipChangedMessage(string UserId);
```

When `DataService` modifies a membership record, it resolves all active `ICircuitConnection` instances for the affected user via `ICircuitManager.Connections`, filtered by `connection.User.Id == userId`. For each matching connection, it sends a `MembershipChangedMessage` via `IMessenger.Send(message, connectionId)`.

`IActiveOrganizationContext` subscribes to `MembershipChangedMessage` on circuit initialization and handles it by calling `RefreshAsync()`. If `RefreshAsync()` returns `true` (state changed), the context raises `StateChanged` and the toast is displayed.

---

## 9. Protocols and Flows

### 9.1 Protocols

Not applicable: this project uses the existing SignalR infrastructure for membership notifications. No new bespoke protocol is introduced; the notification is a standard named hub method invocation on the existing SignalR connection.

### 9.2 Architectural Data Flows

**New user invitation and registration.**
An org admin or user with `CanInvite = true` submits an invitation for a new user email address via the Manage Organization page. `DataService.AddInvite` creates an `InviteLink` record associated with the org, with `IsAdmin` removed (since invited users always receive base privileges). The server sends an invitation email containing a registration link that embeds the `InviteLink.ID`. The invitee navigates to the registration page, completes registration, and the registration handler calls `DataService.JoinViaInvitation`. This method creates the user account and a `UserOrganizationMembership` record with base privileges, then deletes the `InviteLink`.

**Direct add of existing user.**
An org admin navigates to the Manage Organization page and selects an existing user by username or email. `DataService.AddUserToOrganization` creates a `UserOrganizationMembership` record for that user in the current org with base privileges. If the user has a connected circuit, the server pushes a SignalR membership notification. The circuit refreshes its `IActiveOrganizationContext` state, detects the new membership, and displays a toast.

**Org context switch.**
The user selects an org from the switcher dropdown. The Blazor component calls `IActiveOrganizationContext.SetActiveOrganizationAsync(orgId)`. The service validates the switch (membership or server admin), updates `ActiveOrganizationId` and `ActiveMembership`, and raises `StateChanged`. The device grid and other org-scoped components, subscribed to `StateChanged`, re-render and re-fetch data using the new org ID.

### 9.3 User Workflow Narration

**A user with multiple org memberships opens the application.**
After login, the Blazor circuit initializes `IActiveOrganizationContext`. The service loads the user's membership records and selects a default active org (see OI-03). The org switcher in the nav bar displays the active org name and a dropdown of available orgs. The device grid shows devices for the active org only. The user selects a different org from the switcher; the entire UI immediately reflects the new org's data.

**A server admin switches into a tenant org.**
The server admin's switcher is populated with all orgs on the server. They select a tenant org. `SetActiveOrganizationAsync` validates via `IsServerAdmin`, sets the active org, and raises `StateChanged`. The server admin sees the tenant's devices and can act with full org admin privileges. They switch back to their personal org using the same control.

**ManageOrganization page — refactored surface.**
The existing `ManageOrganization` page is refactored as follows to reflect the new model:

*Users section.* The existing user list and `SetUserIsAdmin` handler are updated to source admin status from `UserOrganizationMembership.IsAdministrator` rather than `RemotelyUser.IsAdministrator`. Two new per-user actions are added alongside the existing admin toggle: a `CanInvite` toggle (FR-21), and a "Remove from org" action (FR-20). The existing "Delete user" action (full account deletion) is retained as a separate, more destructive action. When "Remove from org" is attempted and FR-24 or FR-15 fires, the operation is blocked and a toast message is shown explaining the constraint and directing the admin to delete the account if that is their intent.

*Invite section.* The existing single invite form (which handled both new and existing users via the same email input) is split into two distinct UI paths:
  - **Add existing user** — a text input for username or email, a submit button labeled "Add to org", no email sent. Calls `DataService.AddUserToOrganization`. (FR-08)
  - **Invite new user** — an email input and a submit button labeled "Send invitation". Calls `DataService.AddInvite` and sends the invitation email. The existing `_inviteAsAdmin` checkbox and `IsAdmin` field on `InviteLink` are removed. (FR-09, FR-12)

The existing `_inviteAsAdmin` field and all references to `InviteViewModel.IsAdmin` are removed from the page.

**Server admin org management — new page.**
A new Blazor page (`/ServerConfig/Organizations` or a new tab on the existing `ServerConfig` page) provides the server admin org listing (FR-22) and org creation (FR-23). The page is restricted to server admins via the existing `[Authorize]` + `IsServerAdmin` check pattern used in `ServerConfig`. It displays a table of all organizations with name, ID, member count, and device count, and a form to create a new org by name.

---

## 10. API Surface

The existing Remotely HTTP API is unchanged by this project. The `OrganizationManagementController` may require internal modifications to source org context correctly, but its external surface (endpoint paths, authentication, request/response shapes) is not changed.

New org membership management operations (direct add, remove user from org, update role) are performed through the existing Blazor Server UI and `DataService`, not through new API endpoints. Adding API endpoints for membership management is deferred as OI-04.

---

## 11. Infrastructure

This project introduces no infrastructure changes. Remotely is deployed as a Docker container with Caddy as a reverse proxy. That topology is unchanged. The EF Core migration runs automatically on startup via the existing migration execution pattern in the codebase.

---

## 12. Key Decisions

### KD-01 — Active org context in scoped service, not claims

**Decision.** The active organization ID and per-org role are held in a per-circuit scoped service (`IActiveOrganizationContext`) rather than baked into the ASP.NET Core claims principal or the auth cookie.

**Rationale.** Storing org context in claims would require reissuing the auth cookie on every org switch, introducing a negotiation round-trip and latency. Blazor Server's circuit model provides a natural per-connection scoped lifetime that is a better fit for transient UI state like the active org. The server is the authority for membership data; the scoped service is a cached reflection of that authority for the current circuit.

**Alternatives considered.** Storing org ID in a cookie (without claims reissuance) was considered but rejected because it creates a client-controlled value that the server would need to validate on every request anyway. Storing it in claims was rejected for the latency reason above. A server-side session store (Redis or similar) was considered but rejected as over-engineering for a single-process Blazor Server deployment.

**Consequences.** The scoped service is not a security boundary (see §5.3). All data service methods must perform independent authorization checks. Multiple browser tabs produce independent circuits with independent active org contexts, which is acceptable and beneficial for multi-tenant workflows (UR-03, UR-07). See FR-03, FR-06, FR-07, FR-13.

### KD-02 — IsServerAdmin implies implicit org admin everywhere

**Decision.** A user with `IsServerAdmin = true` has full org admin privileges in every organization without requiring a `UserOrganizationMembership` record.

**Rationale.** The alternative — creating membership records for server admins in every org, and updating those records when new orgs are created — introduces bookkeeping that will drift and cause bugs. A flag check is a single authoritative rule with no state to maintain. Server admins are a small, privileged set; the implicit grant is appropriate.

**Alternatives considered.** Explicit membership for server admins in all orgs was rejected due to maintenance burden. A separate "super-admin org role" concept was considered but added unnecessary complexity.

**Consequences.** The `IActiveOrganizationContext` and all data service authorization checks must check `IsServerAdmin` before checking membership records. See FR-18, FR-05. A deliberate consequence of this decision is that an org may have zero explicit org admins — if the last explicit org admin is demoted while a server admin exists (FR-15 permits this). In that state, only server admins can perform admin-level actions within that org. This is an accepted steady state; "the primary admin contact for tenant org X" is an organizational convention, not a system invariant.

### KD-03 — Invitations are for new users only

**Decision.** The email invitation flow is reserved for users who do not yet have a Remotely account. Existing users are added to organizations by direct admin action without an invitation ceremony.

**Rationale.** Sending an invitation email to a user who already has an account adds friction with little benefit — the admin already knows the user exists. Direct add is simpler and more appropriate for the trust relationship implied by an admin deliberately adding a known user.

**Alternatives considered.** A unified invitation flow for both new and existing users was considered but rejected as unnecessarily complex. Existing users receiving invitations could lead to confusion about whether they need to take action.

**Consequences.** `InviteLink` is scoped to new user registration only. The `IsAdmin` field is removed from `InviteLink` since invited users always receive base privileges. See FR-08, FR-09, FR-12.

### KD-04 — New members always receive base privileges

**Decision.** All new org members — whether directly added or joining via invitation — receive base member privileges (`IsAdministrator = false`, `CanInvite = false`). An admin elevates privileges explicitly after the user is active.

**Rationale.** Granting elevated privileges at the moment of addition introduces a window where a user has admin rights before the admin has verified they are correctly set up. Base-first is the safer default.

**Alternatives considered.** Allowing the admin to specify the role at add-time was considered. It was rejected for direct-add in favor of simplicity; it was rejected for invitations because the original `InviteLink.IsAdmin` field is being removed as part of this project.

**Consequences.** `InviteLink.IsAdmin` is removed. `DataService.AddUserToOrganization` always creates memberships with default-false flags. See FR-08, FR-09.

### KD-05 — API tokens are identity-only; org ID is always a request parameter

**Decision.** `ApiToken.OrganizationID` is removed as an active field. Tokens authenticate user identity only. The target organization for any API call is supplied as an explicit `organizationId` parameter on the request. Legacy tokens (non-null `OrganizationID`) are invalidated immediately on receipt.

**Rationale.** Org-scoped tokens require minting a new token each time a user switches org context, introducing latency and coupling token lifecycle to UI navigation state. Since the goal of this project is to allow a single identity to span multiple orgs, binding a token to a single org is architecturally inconsistent with that goal. An explicit per-request org parameter is cleaner, stateless, and consistent with how the rest of the system now works.

**Alternatives considered.** Including all org memberships as claims in the token was considered. Rejected because it requires token reissuance whenever memberships change, reintroducing the latency and complexity problem. Keeping `OrganizationID` on tokens but making it optional was considered but would create two code paths for every token-authenticated request.

**Consequences.** This is a breaking change to the existing API surface. All existing API consumers must update their requests to pass `organizationId` explicitly. Legacy tokens are forcibly invalidated on first use with a clear re-authentication message. `ApiToken.OrganizationID` is retained as a nullable column for legacy detection; it is dropped in a follow-on migration (OI-08). See FR-28, FR-31.

### KD-06 — Org ID is a universal explicit request parameter

**Decision.** Regardless of how a caller is authenticated (API token, cookie, Blazor circuit), the target organization for any org-scoped operation is always supplied as an explicit parameter. There is no implicit "current org" derived from the auth token or session at the HTTP pipeline level.

**Rationale.** A uniform rule across all caller types eliminates special-casing in the auth handler, the data service, and the API layer. It is also the only rule that works correctly given that `IActiveOrganizationContext` is only available inside Blazor circuits and that tokens no longer carry an org ID (KD-05).

**Alternatives considered.** Deriving org context from a custom HTTP header was considered but rejected as non-standard and harder to document. Deriving it from a session cookie was rejected as it would require server-side session state outside of Blazor circuits.

**Consequences.** `OrganizationAdminRequirementHandler` reads org ID from route/query data (FR-30). All org-scoped `IDataService` methods accept an explicit `organizationId` parameter. The implementing agent must audit all HTTP API controller methods for this parameter. See FR-25, FR-27.

---

## 13. Open Items

### OI-01 — Invite expiry

Invitations currently persist indefinitely until claimed or manually deleted, consistent with existing Remotely behavior (UR-08). Automatic expiry (e.g., 7-day TTL on `InviteLink`) would improve security by limiting the window during which a stale invitation link could be used. This is deferred to a follow-on pass. Would be resolved by adding an `ExpiresAt` field to `InviteLink` and a scheduled cleanup job or on-access expiry check.

### OI-02 — Migration testing against SQL Server and PostgreSQL

Resolved. The author's self-hosted instance runs SQLite and that is the only backend the migration needs to target at this stage. CT-06 is narrowed accordingly: the EF Core migration shall be authored and tested against SQLite only. SQL Server and PostgreSQL compatibility is deferred to a future pass if those backends are ever adopted. The implementing agent need not author provider-specific migration paths.

### OI-03 — Default active org selection policy

Resolved. On circuit initialization, `IActiveOrganizationContext` shall read a user-scoped key from browser `localStorage` (via JS interop) to determine the last active org. If the stored org ID is found and the user retains membership in that org (or is a server admin), it is used as the default. Otherwise the circuit falls back to the user's first membership by creation date. On every org switch, the switcher component writes the new org ID to `localStorage` under the same user-scoped key. This gives the user a shared org preference across tabs, consistent with how other per-site preferences (e.g., theme) behave. The multi-tab edge case — where two tabs intentionally show different orgs simultaneously — is accepted as a known limitation and deferred to OI-07.

**Implementation note (Blazor prerender):** JS interop calls cannot execute during Blazor Server prerendering or during `OnInitializedAsync`. The `localStorage` read must be deferred to `OnAfterRenderAsync(firstRender: true)`. This means the first render of the device grid will use the fallback default (first membership by creation date) and may briefly swap to the stored preference on the subsequent render cycle. This flicker is accepted as a known minor UX limitation of the v1 implementation.


### OI-06 — Docker image build for fork

The author currently runs the upstream `immybot/remotely:latest` Docker image. Changes introduced by this project require building and publishing a custom Docker image from the fork. The build tooling, Dockerfile, and image publishing workflow need to be established before the modified server can be deployed. This is a deployment concern deferred to the implementation phase; the implementing agent should confirm the existing `Dockerfile` (if present) or establish a build process from the solution's CI artifacts. No design decisions are blocked by this item.

### OI-07 — Per-tab independent org context

The current design stores the last active org in `localStorage`, which is shared across all tabs in the same browser (OI-03). A user who intentionally opens two tabs to manage two different orgs simultaneously will find that switching orgs in one tab updates `localStorage` and affects any subsequently opened tabs. This is accepted as a known limitation of the v1 design. A future refinement could use `sessionStorage` (per-tab, not shared) for the active org, falling back to `localStorage` for the initial default. Deferred; no decision needed before initial implementation.

### OI-08 — Drop ApiToken.OrganizationID column

Once all legacy API tokens have been invalidated and cycled out (FR-31), the `OrganizationID` column on `ApiTokens` serves no further purpose and should be removed in a follow-on EF Core migration. The column is retained in v1 solely to detect legacy tokens. The implementing agent should note that the column must not be dropped in the v1 migration. A future pass should confirm no legacy tokens remain before dropping.

### OI-04 — API endpoints for membership management

Membership management (add user, remove user, update role, list members) is currently handled through the Blazor Server UI only. Exposing these operations through the existing REST API (`/swagger`) would allow external tools and automation to manage org membership. This is deferred; no decision is needed before initial implementation.

### OI-05 — Notification email on direct add

When an org admin directly adds an existing user to an org, the added user receives no email notification. They are notified via toast if connected (FR-14), or see updated state on next login. Whether a courtesy email should also be sent is deferred. No decision is needed before initial implementation.

---

## 14. Revision Log

### 2026-05-22T05:00:00Z

Post-CLI-review pass addressing all material and editorial gaps from external spec review. FR-25 and FR-30 added: explicit org ID on every request; OrganizationAdminRequirementHandler resolution rule. FR-26 added: DeviceGroup assignment cleanup on org removal. FR-27 added: server-side org membership validation on every request. FR-28 and FR-31 added: ApiToken org divorce and legacy token invalidation. FR-29 added: live org revocation behavior. FR-19 softened: InviteLink row deletion acknowledged as intentional. NFR-01 reinforced: DataService must not trust circuit-side state. NFR-04 extended to reference live-revocation case. §5.3 expanded: IActiveOrganizationContext circuit-only constraint. §5.4 added: sources-of-org-ID-by-caller-type table. §6.1 updated: DeviceGroup cleanup note; ApiToken modification noted. §6.2 updated: ApiToken schema entry; InviteLink migration note. §7.1 updated: Tests project noted as requiring updates. §8.2 updated: API token breaking change documented. KD-02 updated: zero-explicit-admin-org steady state acknowledged. KD-05 added: API token org divorce rationale. KD-06 added: org ID as universal explicit request parameter. OI-03 updated: Blazor prerender JS interop timing note. OI-08 opened: future ApiToken.OrganizationID column drop. §4.4 file map updated: ApiToken.cs added.

### 2026-05-22T04:00:00Z

Spec expanded for CLI readiness. §8.1 IDataService updated with full enumeration of methods requiring call-site changes and new methods to be added (AddUserToOrganization, RemoveUserFromOrganization, SetMemberIsAdmin, SetMemberCanInvite, GetMembership, GetMembershipsForUser, GetAllOrganizations, CreateOrganization). ChangeUserIsAdmin marked for removal. §8.3 SignalR notification mechanism fully specified using existing SimpleMessenger pattern with new MembershipChangedMessage record. §9.3 expanded with ManageOrganization page refactor narration (user section, invite section split, new server admin org management page). §5.2 localStorage key format specified as remotely:activeOrg:{userId}.

### 2026-05-22T03:00:00Z

FR-24 added: last membership removal prevention — system blocks removal of a user's last org membership and directs the admin to delete the account instead. FR-20 updated to cross-reference FR-24 and FR-15.

### 2026-05-22T02:00:00Z

FR-20 added: remove user from org (distinct from account deletion). FR-21 added: grant/revoke CanInvite privilege. FR-22 added: server admin org listing. FR-23 added: server admin org creation. Section 2 renumbered to accommodate new §2.5 Server Admin Organization Management; Lockout Prevention becomes §2.7, Schema Migration becomes §2.8. OI-02 resolved: migration scoped to SQLite only. OI-03 resolved: last active org in localStorage. OI-06 opened: Docker build. OI-07 opened: per-tab org context limitation. CT-06 narrowed to SQLite only.

### 2026-05-22T00:00:00Z

Initial draft produced from design conversation. All URs (UR-01 through UR-08), FRs (FR-01 through FR-19), NFRs (NFR-01 through NFR-07), and CTs (CT-01 through CT-06) established. Key Decisions KD-01 through KD-04 recorded. Open Items OI-01 through OI-05 opened. Sections 5 through 9 drafted at architectural level; detail sections (§8 interfaces, §9 flows) are initial drafts pending review.

---

*End of specification.*
