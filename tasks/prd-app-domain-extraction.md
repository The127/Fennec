# PRD: App Domain Logic Extraction — Fennec.App

## Introduction

Fennec.App ViewModels contain implicit domain logic that has no separate home: message grouping rules, shortcode replacement, message length constraints, and an optimistic-update state machine are all embedded as imperative code or magic numbers inside `ServerViewModel`. This makes them impossible to test in isolation, duplicated across methods, and invisible to the ubiquitous language.

Following Evans' advice: extract a pure domain layer in `Fennec.App/Domain/` — plain C# classes with no Avalonia dependency — and let ViewModels become thin wrappers. Decomposition of ViewModel structure is out of scope (separate PRD).

---

## Goals

- Domain rules live in named, testable classes — not inside ViewModel methods
- Zero duplication of message grouping logic
- Message length constraints expressed as a single domain concept
- Optimistic update states explicit, not inferred from flag combinations
- `Fennec.App/Domain/` classes have no dependency on Avalonia or CommunityToolkit.Mvvm
- Every extracted concept is covered by fast, isolated unit tests

---

## User Stories

### US-001: Extract `MessageGrouper` — eliminate duplicated grouping logic
**Description:** As a developer, I want message grouping rules in one place so `LoadMessagesAsync` and `BuildMessageItem` stop duplicating the same "show author / show time separator" logic.

**Acceptance Criteria:**
- [ ] `Fennec.App/Domain/MessageGrouper.cs` introduced — pure static class or record, no Avalonia dependency
- [ ] `MessageGrouper.ShouldShowAuthor(previous, current)` returns `true` when author differs, OR time gap ≥ 5 min, OR a day boundary is crossed
- [ ] `MessageGrouper.ShouldShowTimeSeparator(previous, current)` returns `true` when a calendar-day boundary is crossed
- [ ] `LoadMessagesAsync` in `ServerViewModel` uses `MessageGrouper` — inline duplication removed
- [ ] `BuildMessageItem` in `ServerViewModel` uses `MessageGrouper` — inline duplication removed
- [ ] Unit tests: same author within 5 min → no header; same author after 5 min → header; different author → header; new day → separator; same day → no separator; first message (no previous) → header, no separator
- [ ] All existing tests pass

---

### US-002: Extract `ShortcodeReplacer` — promote internal static to domain class
**Description:** As a developer, I want shortcode replacement to live in a named domain class so it is testable, discoverable, and no longer hidden as `internal static` on a ViewModel.

**Acceptance Criteria:**
- [ ] `Fennec.App/Domain/ShortcodeReplacer.cs` introduced — pure static class, no Avalonia dependency
- [ ] `ShortcodeReplacer.Replace(string text)` replaces `:shortcode:` tokens with Unicode emoji, leaving unknown shortcodes untouched
- [ ] `ServerViewModel.ReplaceShortcodes` deleted; call site updated to `ShortcodeReplacer.Replace(...)`
- [ ] Unit tests: known shortcode replaced with correct Unicode; unknown shortcode left as-is; multiple shortcodes in one string; empty string
- [ ] All existing tests pass

---

### US-003: Extract `MessageLengthPolicy` — replace magic numbers with a domain concept
**Description:** As a developer, I want message length rules expressed as a named policy so `MaxMessageLength` and `CharCountVisibleThreshold` are not magic numbers on a ViewModel.

**Acceptance Criteria:**
- [ ] `Fennec.App/Domain/MessageLengthPolicy.cs` introduced — pure static class, no Avalonia dependency
- [ ] `MessageLengthPolicy.MaxLength` (10 000) and `MessageLengthPolicy.CounterVisibleThreshold` (9 000) are named constants
- [ ] `MessageLengthPolicy.CharsRemaining(string text)` returns `MaxLength - text.Length`
- [ ] `MessageLengthPolicy.ShouldShowCounter(string text)` returns `true` when `text.Length >= CounterVisibleThreshold`
- [ ] `MessageLengthPolicy.IsOverLimit(string text)` returns `true` when `text.Length > MaxLength`
- [ ] `ServerViewModel` computed properties (`MessageCharsRemaining`, `ShowCharCount`, `IsOverLimit`) delegate to `MessageLengthPolicy`
- [ ] Magic number literals removed from `ServerViewModel`
- [ ] Unit tests: empty string not over limit; exactly at max not over limit; one char over is over limit; threshold boundary for counter visibility; chars remaining calculation
- [ ] All existing tests pass

---

### US-004: Extract `OutgoingMessageState` — make the optimistic-update state machine explicit
**Description:** As a developer, I want the pending/delivered/failed states of an outgoing message to be an explicit domain concept so the state machine in `SendMessage()` is not inferred from flag combinations on `MessageItem`.

**Acceptance Criteria:**
- [ ] `Fennec.App/Domain/OutgoingMessageState.cs` introduced — discriminated union or enum (`Pending`, `Delivered`, `Failed`), no Avalonia dependency
- [ ] `MessageItem` gains a single `OutgoingMessageState? SendState` property replacing the pair `IsPending` / `IsSendFailed` (both can be derived from `SendState`)
- [ ] `IsPending` and `IsSendFailed` kept as computed properties delegating to `SendState` so existing View bindings continue to work without change
- [ ] `SendMessage()` in `ServerViewModel` transitions through `Pending → Delivered | Failed` using `SendState` — no direct assignment of `IsPending`/`IsSendFailed`
- [ ] Unit tests on `OutgoingMessageState`: newly created message has `null` state (not outgoing); after optimistic add, state is `Pending`; on success, state is `Delivered`; on failure, state is `Failed`
- [ ] All existing tests pass

---

### US-005: Deduplicate `ServerRoute` construction in `MainAppViewModel`
**Description:** As a developer, I want `ServerRoute` creation to go through a single private factory method so the three identical inline constructions in `MainAppViewModel` are not a maintenance hazard.

**Acceptance Criteria:**
- [ ] Private `CreateServerRoute(SidebarServer server)` method introduced in `MainAppViewModel`
- [ ] All three call sites (`NavigateToServerAsync`, `LoadServersAndNavigateToServerAsync`, `IVoiceChannelNavigator.NavigateToVoiceChannelAsync`) use the factory method
- [ ] No behavioural change — parameters passed are identical to current inline construction
- [ ] Unit tests: navigating to a server produces a route with the correct `ServerId` and `InstanceUrl`; all three navigation paths reach the same server (message-based, direct, voice-return)
- [ ] All existing tests pass

---

## Functional Requirements

- FR-1: `MessageGrouper.ShouldShowAuthor` and `ShouldShowTimeSeparator` encode the grouping rules; `ServerViewModel` has no inline grouping logic
- FR-2: `ShortcodeReplacer.Replace` is the single implementation of shortcode-to-emoji substitution
- FR-3: `MessageLengthPolicy` owns `MaxLength`, `CounterVisibleThreshold`, and the three derived predicates
- FR-4: `OutgoingMessageState` is the authoritative state for an in-flight message; `IsPending`/`IsSendFailed` are derived
- FR-5: `MainAppViewModel.CreateServerRoute` is the single construction point for `ServerRoute`
- FR-6: All classes in `Fennec.App/Domain/` have zero references to Avalonia or CommunityToolkit.Mvvm namespaces

---

## Non-Goals

- No decomposition of `ServerViewModel` into further sub-ViewModels (separate PRD)
- No new `Fennec.Domain` project — all domain classes stay in `Fennec.App/Domain/`
- No changes to `Fennec.Api` or `Fennec.Client`
- No changes to SignalR or federation behavior
- No UI/UX changes — view bindings must continue to work without modification

---

## Technical Considerations

- `MessageGrouper` takes `Instant?` values directly — no string parsing inside the domain class; callers parse first (resolved: no primitive obsession)
- `OutgoingMessageState` — use a sealed class hierarchy or a simple enum; avoid adding an Avalonia `ObservableObject` base
- Stories are independent and can be executed in any order; each must leave the solution green
- Tests live in `Fennec.App.Tests/Domain/` mirroring the production folder

---

## Success Metrics

- `ServerViewModel` contains no magic number literals for message length
- `BuildMessageItem` and `LoadMessagesAsync` share zero duplicated grouping logic
- `ReplaceShortcodes` method deleted from `ServerViewModel`
- `new ServerRoute(...)` appears exactly once in `MainAppViewModel`
- All domain classes in `Fennec.App/Domain/` pass a dependency check: no Avalonia references

---

## Open Questions

- ~~`MessageGrouper` string vs Instant?~~ Resolved: `Instant?` — no primitive obsession; callers parse before calling the grouper.
- ~~Enum vs sealed hierarchy for `OutgoingMessageState`?~~ Resolved: sealed class hierarchy (`PendingState`, `DeliveredState`, `FailedState(string Reason)`) — state and error travel together, no future flag sprawl.
