# PRD: DDD Cleanup — Fennec.Api

## Introduction

Fennec.Api has accumulated 8 DDD violations from rapid AI-driven feature development. Business logic is scattered across command handlers, domain models are anemic data bags, and primitive obsession creates silent correctness bugs (especially in federation). This PRD drives a focused cleanup to restore domain integrity so the team can move fast again without stepping on invariants.

No new user-facing features. All stories are internal refactoring backed by behavioral tests.

---

## Goals

- Business rules live on domain models, not command handlers
- Compiler prevents ID type confusion (`ServerId` vs `ChannelId`)
- `InstanceUrl` is always normalized — federation never silently misbehaves
- Membership authorization lives in one place
- `Server` is a true aggregate root — children cannot be mutated without going through it
- All behavior is covered by fast, isolated unit tests
- Test suite gives genuine confidence before every commit

---

## User Stories

### US-001: `ServerInvite.CanRedeem(Instant)` — expiry and use-count guard
**Description:** As a developer, I want expiry and use-count checking to live on `ServerInvite` so every call site gets the same invariant automatically.

**Acceptance Criteria:**
- [ ] `ServerInvite` exposes `bool CanRedeem(Instant now)` that returns `false` when `ExpiresAt <= now` or `Uses >= MaxUses`
- [ ] `ServerInvite` exposes `void Redeem()` that increments `Uses`
- [ ] `JoinServerFederateCommandHandler` uses `invite.CanRedeem(now)` and `invite.Redeem()` instead of inline checks
- [ ] Unit tests on `ServerInvite`: expired invite cannot redeem, exhausted invite cannot redeem, valid invite can redeem, `Redeem()` increments `Uses`
- [ ] All existing handler tests continue to pass

---

### US-002: `KnownUser.MarkStale()` — stale-user detection on the model
**Description:** As a developer, I want stale-user detection to be a model method so `JoinServerFederateCommandHandler` doesn't contain domain policy about identity recycling.

**Acceptance Criteria:**
- [ ] `KnownUser` exposes `void MarkStale()` that sets `IsDeleted = true`
- [ ] Handler calls `staleUser.MarkStale()` instead of `staleUser.IsDeleted = true`
- [ ] Unit test: `MarkStale()` sets `IsDeleted` to `true`
- [ ] All existing handler tests continue to pass

---

### US-003: `FederationInstanceUrl` value object — construction and normalization
**Description:** As a developer, I want `InstanceUrl` to be a value object so normalization and well-formedness are enforced at construction, not scattered across call sites.

**Acceptance Criteria:**
- [ ] `record FederationInstanceUrl` is introduced in `Fennec.Api/Models/` (or a `ValueObjects/` subfolder)
- [ ] Constructor normalizes by stripping trailing slash and lower-casing scheme+host
- [ ] Constructor throws `ArgumentException` for non-HTTP(S) or malformed URLs
- [ ] `KnownUser.InstanceUrl`, `KnownServer.InstanceUrl`, `IAuthPrincipal.Issuer`, and relevant command fields accept `FederationInstanceUrl` (or are compared via it)
- [ ] EF Core column type stays `varchar` / `text` — add value converter so DB schema is unchanged
- [ ] Unit tests: trailing slash stripped, scheme normalized, invalid URL throws, equality by normalized value
- [ ] All existing tests continue to pass

---

### US-004: `ServerId`, `ChannelId`, `UserId`, `KnownUserId`, `MessageId` typed ID wrappers
**Description:** As a developer, I want typed ID wrappers so the compiler rejects passing a `ChannelId` where a `ServerId` is expected.

**Acceptance Criteria:**
- [ ] `record ServerId(Guid Value)`, `ChannelId`, `UserId`, `KnownUserId`, `MessageId` introduced
- [ ] `Server`, `Channel`, `ChannelGroup`, `ServerMember`, `KnownUser`, `ChannelMessage`, `Notification` entity ID fields updated to typed IDs
- [ ] EF Core value converters added so DB schema is unchanged (still stores `uuid`)
- [ ] Command/query records updated to use typed IDs where they reference these aggregates
- [ ] Unit tests: two different ID types with the same `Guid` value are not equal to each other, typed ID round-trips through value converter
- [ ] All existing tests continue to pass

---

### US-005: Extract `ListServerMembersQuery` — remove direct DB access from controller
**Description:** As a developer, I want the `ListServerMembers` controller action to go through MediatR so it's consistent with every other read path.

**Acceptance Criteria:**
- [ ] `ListServerMembersQuery` and handler introduced in `Queries/`
- [ ] `ServerController.ListServerMembers` becomes a one-liner `mediator.Send(...)`
- [ ] No `FennecDbContext` injected directly in `ServerController`
- [ ] Unit test on `ListServerMembersQuery`: returns members for the given server, empty list when no members
- [ ] All existing tests continue to pass

---

### US-006: `InviteCode` value object — move generation out of the handler
**Description:** As a developer, I want invite code generation to live on a value object so the format rules and randomness are not private statics on a command handler.

**Acceptance Criteria:**
- [ ] `record InviteCode` introduced with `static InviteCode Generate()` factory using the existing alphabet and 8-char length
- [ ] `InviteCode` stores the code string internally; implicit or explicit conversion to `string` available
- [ ] `CreateServerInviteCommandHandler` uses `InviteCode.Generate()` instead of inline `GenerateCode(8)`
- [ ] `ServerInvite.Code` accepts `InviteCode` (or stores `string` via `ToString()`)
- [ ] Unit tests: generated code is 8 chars, alphanumeric only; two calls produce different codes (probabilistic)
- [ ] All existing tests continue to pass

---

### US-007: `IServerMembershipChecker` domain service — deduplicate membership auth
**Description:** As a developer, I want membership authorization extracted into one service so the 5-handler duplication is eliminated.

**Acceptance Criteria:**
- [ ] `IServerMembershipChecker` interface with `Task<KnownUser> AssertIsMemberAsync(IAuthPrincipal principal, Guid serverId, CancellationToken ct)` (returns `KnownUser` so callers don't re-fetch)
- [ ] `ServerMembershipChecker` implementation extracted from the duplicated logic in `RenameChannelCommand`, `DeleteChannelCommand`, `UpdateChannelTypeCommand`, `CreateChannelCommand`, `SendMessageCommand`
- [ ] All 5 handlers replaced to call `membershipChecker.AssertIsMemberAsync(...)`
- [ ] Unit tests on `ServerMembershipChecker`: throws `HttpForbiddenException` when user not found, throws when not a member, returns `KnownUser` when member
- [ ] All existing handler tests continue to pass (update mocks to stub `IServerMembershipChecker`)

---

### US-008: `Server` as aggregate root — factory method for server creation
**Description:** As a developer, I want `Server` to expose a static factory that creates itself with its default members and channels so `CreateServerCommand` stops constructing children externally.

**Acceptance Criteria:**
- [ ] `Server.Create(string name, ServerVisibility visibility, KnownUser creator, string issuerUrl)` static factory introduced
- [ ] Factory internally constructs `ServerMember`, `ChannelGroup` (default), `Channel` (default), `KnownServer`, `UserJoinedKnownServer` and adds them to navigation collections
- [ ] `CreateServerCommandHandler` calls `Server.Create(...)` and does a single `dbContext.Add(server)` — no manual `AddRange` of children
- [ ] EF Core cascade configuration verified: `dbContext.Add(server)` cascades to all children
- [ ] Unit tests on `Server.Create`: server has the creator as a member, default group and channel exist, `KnownServer` and `UserJoinedKnownServer` are initialized
- [ ] All existing tests continue to pass

---

### US-009: `Server.AddChannel(...)` — channel creation through the aggregate
**Description:** As a developer, I want channel creation to go through the `Server` aggregate root so the membership check and channel construction are co-located.

**Acceptance Criteria:**
- [ ] `Server.AddChannel(string name, Guid channelGroupId, ChannelType type, KnownUser requestor)` method introduced; throws if `requestor` is not a member
- [ ] `CreateChannelCommandHandler` uses `server.AddChannel(...)` instead of constructing `Channel` directly
- [ ] Membership check in `CreateChannelCommand` replaced by the aggregate method (handler still fetches server with members loaded)
- [ ] Unit tests: non-member cannot add channel (throws), member can add channel, channel appears in `server.Channels`
- [ ] All existing tests continue to pass

---

### US-010: Typed `ChannelMessage.Details` — replace `JsonDocument` with sealed type hierarchy
**Description:** As a developer, I want `ChannelMessage.Details` to use a typed class so the implicit anonymous-object contract between `SendMessageCommand` and the read path is made explicit and compiler-verified.

**Acceptance Criteria:**
- [ ] `TextMessageContent` class (or record) introduced; `SendMessageCommand` uses it instead of `new { text = ... }` anonymous object
- [ ] `ChannelMessage.Details` stores a typed object; EF Core serializes via `OwnsOne` or a JSON value converter
- [ ] Existing `TextMessage` class in `ChannelMessage.cs` is reconciled (reuse or remove)
- [ ] Unit test: `SendMessageCommand` produces a `ChannelMessage` whose `Details` is a `TextMessageContent` with the correct text
- [ ] All existing tests continue to pass

---

## Functional Requirements

- FR-1: `ServerInvite.CanRedeem(Instant)` and `Redeem()` enforce expiry and use-count invariants
- FR-2: `KnownUser.MarkStale()` encapsulates the `IsDeleted = true` assignment
- FR-3: `FederationInstanceUrl` value object normalizes and validates on construction; EF maps to existing `text` column
- FR-4: Typed ID records (`ServerId`, `ChannelId`, `UserId`, `KnownUserId`, `MessageId`) with EF value converters; DB schema unchanged
- FR-5: `ListServerMembersQuery` replaces inline DB access in `ServerController`
- FR-6: `InviteCode` value object with `Generate()` factory; handler uses it
- FR-7: `IServerMembershipChecker.AssertIsMemberAsync` consolidates the 5-copy membership check
- FR-8: `Server.Create(...)` factory constructs server with all required children; single `dbContext.Add(server)` in the handler
- FR-9: `Server.AddChannel(...)` enforces membership before creating a channel
- FR-10: `ChannelMessage.Details` uses a typed `TextMessageContent` instead of `JsonDocument` / anonymous object

---

## Non-Goals

- No new API endpoints or DTOs
- No changes to SignalR hub behavior
- No federation protocol changes
- No client-side (Fennec.App) changes
- No database schema changes — all EF mappings must preserve existing column names and types
- No role-based permissions beyond the existing member/non-member check

---

## Technical Considerations

- Stories can be executed independently in any order; each must leave the solution in a green state
- EF Core value converters (`HasConversion`) are the mechanism for typed IDs and `FederationInstanceUrl`
- Tests use `NSubstitute` mocks and the existing `DbSetMockExtensions` helper
- Each story should produce at minimum one new test file or extend the directly relevant existing one
- `US-007` (membership service) touches 5 handlers — update their existing test mocks to stub `IServerMembershipChecker`; don't delete the behavioral coverage

---

## Success Metrics

- Zero direct DB access in controllers (all reads via MediatR queries)
- Zero duplicated membership-check blocks across handlers
- Compiler error if a `ChannelId` is passed where `ServerId` is expected
- All DDD violations in `DDD.md` resolved
- Test suite remains green after each story

---

## Open Questions

- Should `FederationInstanceUrl` live in `Fennec.Shared` (accessible to `Fennec.Client`) or stay in `Fennec.Api`? Federation clients also need to normalize URLs.
- Should typed IDs implement `IComparable` for sorting, or is equality enough for now?
- `US-008` / `US-009`: should `Server.AddChannel` also handle `ChannelGroup` creation, or keep that as a separate aggregate method?
