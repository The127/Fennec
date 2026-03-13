# Add Message Reactions

## Context
Users want to react to messages with emoji (like Discord/Slack). The emoji picker and database already exist. No reaction infrastructure exists yet — this is a new feature across all layers.

## Approach

### 1. Backend Model — `MessageReaction`
**New file:** `Fennec.Api/Models/MessageReaction.cs`
- `MessageReaction : EntityBase` with `ChannelMessageId`, `UserId`, `Emoji` (Unicode string)
- Unique constraint: `(ChannelMessageId, UserId, Emoji)` — one reaction per user per emoji per message
- Navigation props: `ChannelMessage`, `User`
- EF config class with composite unique index

### 2. Backend Commands
**New file:** `Fennec.Api/Commands/ToggleReactionCommand.cs`
- Single toggle command (add if not exists, remove if exists) — simpler UX
- Validates channel membership via `ChannelMessage → Channel → ServerMember`
- Pattern: follows `SendMessageCommandHandler` style

### 3. Backend Query Changes
**Modify:** `Fennec.Api/Queries/ListMessagesQuery.cs`
- Extend `ListMessagesResponse` to include reactions data
- Group reactions by emoji, return `List<ReactionGroup>` per message where `ReactionGroup = { Emoji, Count, UserIds[] }`
- Query with `.Include()` or a subquery

### 4. API Controller
**Modify:** `Fennec.Api/Controllers/UserApi/MessageController.cs`
- Add `PUT {messageId}/reactions` endpoint with body `{ emoji: string }` (toggle semantics)
- Route: `api/v1/servers/{serverId}/channels/{channelId}/messages/{messageId}/reactions`

### 5. Shared DTOs
**New files in** `Fennec.Shared/Dtos/Server/`:
- `ToggleReactionRequestDto` — `{ emoji: string }`
- `ReactionGroupDto` — `{ emoji, count, reacted }` (reacted = current user reacted)

**Modify:** `ListMessagesResponseItemDto`
- Add `Reactions` property: `List<ReactionGroupDto>`

**Modify:** `SharedFennecJsonContext.cs`
- Register new DTO types

### 6. Client HTTP Methods
**Modify:** `Fennec.Client/Clients/Server.cs`
- Add `ToggleReactionAsync(baseUrl, serverId, channelId, messageId, dto)` to interface + impl

### 7. Client ViewModel
**Modify:** `Fennec.App/ViewModels/ServerViewModel.cs`
- Add `ReactionGroup` class: `Emoji`, `Count`, `Reacted` (bool), `DisplayText` (e.g. "👍 3")
- Add `Reactions` collection to `MessageItem`
- Add `ToggleReaction(MessageItem, string emoji)` method → calls API, reloads messages
- Wire current user ID: pass `Guid currentUserId` through `ServerRoute` → `ServerViewModel` constructor (from `_session.UserId` in `MainAppViewModel`)

### 8. Client View (XAML)
**Modify:** `Fennec.App/Views/ServerView.axaml`
- Below message content Border, add reactions row:
  - `ItemsControl` with `WrapPanel` showing reaction pills
  - Each pill: `Button` with emoji + count, highlighted if current user reacted
  - "+" button to open emoji picker flyout for adding reactions (reuse existing `EmojiPickerView`)
  - Pill styling: rounded border, small font, `SecondaryColor50` bg, `PrimaryColor` bg when reacted

### 9. EF Migration
- `dotnet ef migrations add AddMessageReactions`

## Files to modify
- `Fennec.Api/Models/MessageReaction.cs` (new)
- `Fennec.Api/Commands/ToggleReactionCommand.cs` (new)
- `Fennec.Api/Queries/ListMessagesQuery.cs`
- `Fennec.Api/Controllers/UserApi/MessageController.cs`
- `Fennec.Shared/Dtos/Server/ToggleReactionRequestDto.cs` (new)
- `Fennec.Shared/Dtos/Server/ReactionGroupDto.cs` (new)
- `Fennec.Shared/Dtos/Server/ListMessagesResponseItemDto.cs`
- `Fennec.Shared/SharedFennecJsonContext.cs`
- `Fennec.Client/Clients/Server.cs`
- `Fennec.App/ViewModels/ServerViewModel.cs`
- `Fennec.App/Views/ServerView.axaml`
- `Fennec.App/Routes/ServerRoute.cs`
- `Fennec.App/ViewModels/MainAppViewModel.cs` (pass userId to ServerRoute)

## Verification
1. `dotnet build Fennec.App/Fennec.App.csproj`
2. `dotnet test`
3. Manual: send message → hover → click "+" → pick emoji → reaction pill appears → click pill to toggle off
