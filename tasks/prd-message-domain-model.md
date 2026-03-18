# PRD: Introduce Message as a Domain Value Object

## Introduction

The previous iteration extracted grouping logic, length policy, and send state into `Domain/`, but the result is a collection of disconnected static utilities rather than a coherent model. There is no `Message` entity — the closest thing is `MessageItem`, a ViewModel display class. The domain has no shared vocabulary, no invariants, and no home for the rules that govern a message.

This iteration introduces `Message` as a proper domain value object, fixes the hidden timezone dependency in `MessageGrouper`, relocates `ShortcodeReplacer` out of `Domain/`, and wires `SendState` transitions through the `MessageItem` display model while keeping `MessageItem` in `ViewModels/` where it belongs.

## Goals

- Introduce `Message` value object in `Domain/` that owns the raw facts of a message (id, author, content, timestamp)
- Fix `MessageGrouper`'s hidden `DateTimeZone` dependency — callers supply the zone
- Move `ShortcodeReplacer` to `Fennec.App/Formatting/` — it is text infrastructure, not domain logic
- `BuildMessageItem` in `ServerViewModel` constructs a `Message` first, then maps to `MessageItem` — domain logic stays in the domain
- All existing tests continue to pass; new tests cover `Message` invariants

## User Stories

### US-001: Introduce `Message` value object
**Description:** As a developer, I want a `Message` value object in `Domain/` so that the facts of a message have a named home with enforced invariants.

**Acceptance Criteria:**
- [ ] `Fennec.App/Domain/Message.cs` introduced — no Avalonia, no CommunityToolkit.Mvvm dependency
- [ ] `Message` is a `readonly record struct` with properties: `Guid Id`, `Guid AuthorId`, `string? AuthorInstanceUrl`, `string Content`, `Instant Timestamp`
- [ ] Static factory `Message.Create(Guid id, Guid authorId, string? authorInstanceUrl, string content, string isoTimestamp)` — parses the ISO timestamp and throws `ArgumentException` if unparseable
- [ ] `Message.AuthorIdentity` computed: `username@instanceUrl` when `AuthorInstanceUrl` is non-null, else just username — but username is not on `Message`; `AuthorIdentity` is NOT on `Message` (it belongs on the display layer)
- [ ] Unit tests in `Fennec.App.Tests/Domain/MessageTests.cs`: valid inputs produce correct properties; unparseable timestamp throws; content is stored as-is (no trimming in the domain)
- [ ] Tests pass
- [ ] Typecheck passes

### US-002: Fix `MessageGrouper` timezone dependency
**Description:** As a developer, I want `MessageGrouper` to accept a `DateTimeZone` parameter so the hidden system-default dependency is eliminated and tests can control the zone.

**Acceptance Criteria:**
- [ ] `MessageGrouper.ShouldShowAuthor(Instant? previous, Guid? previousAuthorId, Instant? current, Guid currentAuthorId, DateTimeZone zone)` — `zone` parameter added
- [ ] `MessageGrouper.ShouldShowTimeSeparator(Instant? previous, Instant? current, DateTimeZone zone)` — `zone` parameter added
- [ ] `GetSystemDefault()` call removed from `MessageGrouper` entirely
- [ ] Call sites in `ServerViewModel` pass `DateTimeZoneProviders.Tzdb.GetSystemDefault()` — zone resolution stays in the application layer, not the domain
- [ ] Existing `MessageGrouperTests` updated to supply an explicit zone (use `DateTimeZoneProviders.Tzdb["Europe/London"]` or `DateTimeZone.Utc`)
- [ ] Tests pass
- [ ] Typecheck passes

### US-003: Relocate `ShortcodeReplacer` to `Formatting/`
**Description:** As a developer, I want `ShortcodeReplacer` in `Fennec.App/Formatting/` so the `Domain/` namespace contains only domain logic.

**Acceptance Criteria:**
- [ ] `Fennec.App/Formatting/ShortcodeReplacer.cs` created — namespace changed to `Fennec.App.Formatting`
- [ ] `Fennec.App/Domain/ShortcodeReplacer.cs` deleted
- [ ] `ServerViewModel` using directive updated to `Fennec.App.Formatting`
- [ ] `ShortcodeReplacerTests` namespace/using updated — file moved to `Fennec.App.Tests/Formatting/ShortcodeReplacerTests.cs`
- [ ] Tests pass
- [ ] Typecheck passes

### US-004: Use `Message` in `BuildMessageItem`
**Description:** As a developer, I want `BuildMessageItem` to construct a `Message` value object first, then map it to `MessageItem`, so domain logic runs in the domain layer and `ServerViewModel` becomes a mapping concern.

**Acceptance Criteria:**
- [ ] `BuildMessageItem` constructs a `Message` via `Message.Create(...)` before building `MessageItem`
- [ ] Timestamp parsing (`InstantPattern.ExtendedIso.Parse`) removed from `BuildMessageItem` — `Message.Create` handles it; `BuildMessageItem` reads `message.Timestamp`
- [ ] `MessageGrouper` calls in `BuildMessageItem` use `message.Timestamp` from the `Message` value object
- [ ] `LoadMessagesAsync` similarly constructs `Message` objects before mapping — timestamp parsing loop removed from `ServerViewModel`
- [ ] No behavioural change observable from the UI
- [ ] Tests pass
- [ ] Typecheck passes

## Functional Requirements

- FR-1: `Message` value object is immutable (`readonly record struct`), lives in `Fennec.App.Domain`, has no UI framework dependency
- FR-2: `Message.Create` is the sole entry point — no public constructor that accepts a raw `Instant` (forces callers through validation)
- FR-3: `MessageGrouper` methods accept an explicit `DateTimeZone` — no global state, no system calls inside
- FR-4: `ShortcodeReplacer` namespace is `Fennec.App.Formatting` — `Domain/` contains no text-formatting utilities
- FR-5: `ServerViewModel.BuildMessageItem` and `LoadMessagesAsync` use `Message` as an intermediate step before constructing `MessageItem`

## Non-Goals

- No `Conversation` aggregate — do not model a collection of messages as a domain object
- No changes to `MessageLengthPolicy` or `OutgoingMessageState`
- No changes to API, SignalR, or persistence layer
- No changes to `MessageItem`'s properties or View bindings
- No event sourcing or domain events

## Technical Considerations

- `Message` as `readonly record struct` avoids heap allocation for transient mapping — it is constructed in `BuildMessageItem`, used immediately, not stored
- `DateTimeZone` injection via parameter (not DI service) is sufficient — `ServerViewModel` already has the call site, and a service would be over-engineering for one consumer
- `Message.Create` throwing on bad timestamps is intentional — the API should not be sending malformed ISO strings; a domain exception surfaces the contract violation early
- `Fennec.App/Formatting/` is a new directory — one file for now; it is not over-engineering because it correctly names the layer

## Success Metrics

- `Domain/` contains only `Message.cs`, `MessageGrouper.cs`, `MessageLengthPolicy.cs`, `OutgoingMessageState.cs` — no formatting utilities
- `MessageGrouper` tests pass without any `GetSystemDefault()` call in production code
- `BuildMessageItem` contains no `InstantPattern` parsing
- All existing tests continue to pass

## Open Questions

- Should `Message.Create` return `Result<Message>` instead of throwing, to allow graceful degradation when the API sends a bad timestamp? (Current proposal: throw — the API contract should be enforced, not silently swallowed)
- Is `readonly record struct` the right choice if `Message` ever needs to carry mutable send state? (Current answer: no — send state lives on `MessageItem`, not `Message`)
