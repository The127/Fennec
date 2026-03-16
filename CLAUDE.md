# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build Fennec.sln                           # Build entire solution
dotnet build Fennec.Api/Fennec.Api.csproj          # Build API only
dotnet build Fennec.App.Desktop/Fennec.App.Desktop.csproj  # Build desktop app only

dotnet run --project Fennec.Api                    # Run API server
dotnet run --project Fennec.App.Desktop            # Run desktop app

dotnet test                                        # Run all tests
dotnet test Fennec.Api.Tests                       # Run API tests only
dotnet test --filter "FullyQualifiedName~TestName" # Run single test
```

## Dev Setup

```bash
docker compose up -d                    # Start PostgreSQL (port 7891)
just generate-private-key               # Generate RSA key for JWT signing (private.pem)
dotnet dev-certs https --trust          # Trust dev HTTPS certs
```

PostgreSQL connection (dev): `Host=localhost;Port=7891;Database=fennec;Username=user;Password=password`

## Test Harness

The API and app instances run as systemd user services. Logs go to the journal.

```bash
# Service management
systemctl --user start fennec-test-api            # API server
systemctl --user start fennec-test-app@local       # Local app instance
systemctl --user start fennec-test-app-mac-mini    # Mac Mini app (syncs, builds, runs over SSH)
systemctl --user restart fennec-test-app@local
systemctl --user stop fennec-test-api

# Logs
journalctl --user -u fennec-test-api -f            # Follow API logs
journalctl --user -u fennec-test-app@local -f      # Follow local app logs
journalctl --user -u fennec-test-app-mac-mini -f   # Follow Mac Mini logs
journalctl --user -u 'fennec-test-*' --since '5m ago'  # All test logs, last 5 min
```

App instance env files live in `~/.config/fennec-test/<instance>.env` (e.g. `local.env`, `mac-mini.env`). Supported env vars: `FENNEC_PROFILE`, `FENNEC_AUTO_LOGIN`, `FENNEC_AUTO_LOGIN_PASSWORD`, `FENNEC_AUTO_JOIN_SERVER`, `FENNEC_AUTO_JOIN_CHANNEL`.

## Architecture

Fennec is a **federated real-time chat platform** — a Discord-like app where multiple instances can communicate.

### Projects

- **Fennec.Api** — ASP.NET Core 10.0 Web API backend (PostgreSQL via EF Core)
- **Fennec.App** — Avalonia 11 cross-platform desktop UI (MVVM, SQLite local cache)
- **Fennec.App.Desktop** — Desktop entry point
- **Fennec.App.Browser** — WebAssembly entry point
- **Fennec.Client** — HTTP + SignalR client library (shared between UI targets)
- **Fennec.Shared** — DTOs and JSON serialization context shared between API and Client
- **Fennec.Api.Tests** / **Fennec.App.Tests** — Unit tests

### Key Patterns

**CQRS via MediatR** — API uses `Commands/` for writes and `Queries/` for reads, mediated through MediatR. Each command/query is a self-contained handler class.

**Real-time messaging** — SignalR hubs (`Hubs/MessageHub`) push events to clients. Client-side `MessageHubClient` in Fennec.Client handles reconnection and event dispatch.

**Authentication** — Dual-token system: session tokens for local API auth, JWT public tokens for federation. `AuthenticationMiddleware` validates requests; `IAuthPrincipal` provides user context. RSA private key (`private.pem`) signs JWTs.

**Federation** — Server-to-server communication via `FederationClient/` and `FederationApi/` controllers. HTTP requests are signed with `FederationSigningHandler`. See [Federation Design](#federation-design) below for the full model.

- **HTTP browsing** (messages, channels, members): proxied through the home instance via federation HTTP endpoints. The client always talks to its home API; the home API forwards requests to the hosting instance.
- **Real-time features** (voice, signaling): the client opens a **direct SignalR connection to the hosting instance** using a federation JWT (cached in `TokenStore`). This is necessary because the hosting instance's hub needs a real `connectionId` for the user — without it, peer-targeted operations (SDP/ICE relay, speaking state) silently fail.
- **Identity on remote hubs**: `GetCallerIdentity()` reads userId/username from the JWT and the issuer as instanceUrl. When the user connects directly, `IsRemote(instanceUrl)` returns `false` on the hosting instance, so the user is treated as a local participant with full signaling support.
- `TokenStore` is a DI singleton shared between `ClientFactory` and services like `VoiceHubService` so federation JWTs are accessible app-wide.

**Desktop UI** — Avalonia MVVM with CommunityToolkit.MVVM. `ViewModels/` bind to `Views/` (XAML). Navigation via `Routes/` + `Routing/` layer. DI configured in `App.axaml.cs`.

**Voice** — WebRTC peer-to-peer calls via SIPSorcery + PortAudio for audio I/O. `VoiceStateService` manages call state on the API side. `VoiceHubService` handles hub routing: local channels use the home `MessageHubClient`, remote channels open a direct `HubConnection` to the hosting server.

### Data Flow

```
Desktop App (Avalonia)
  → ViewModels → Services → Fennec.Client (HTTP + SignalR)
    → API Controllers → MediatR Commands/Queries → EF Core → PostgreSQL
```

### Shared Serialization

`SharedFennecJsonContext` in Fennec.Shared defines the JSON source-generated serialization context used by both API and Client. All DTOs live in `Fennec.Shared/Dtos/`.

### Database

- **API**: PostgreSQL with EF Core, snake_case naming convention (`EFCore.NamingConventions`), audit triggers via `EntityFrameworkCore.Triggered`, NodaTime for timestamps
- **Client**: SQLite local cache (`AppDbContext` in Fennec.App)
- Migrations in `Fennec.Api/Migrations/`

## Federation Design

### Core Concepts

Every user has a **home instance** — the instance they registered on. The home instance is privileged in that it must be informed of actions the user takes (e.g. joining a remote server, future: sending DMs). Users connect **directly** to remote instances for server activity (messages, voice) — those do not get federated back to the home instance.

Federation is currently **open**: any instance with valid RSA keys can federate. Whitelisting/blacklisting is not yet implemented.

### User/Server Membership Model

The DB uses a normalized pattern so local and remote users are represented uniformly:

- `User` — local user registered on this instance
- `KnownUser` — a remote user (from another instance), identified by `(RemoteId, InstanceUrl)`
- `ServerMember` — links either a `User` or `KnownUser` to a server (polymorphic FK via `KnownUserId`)
- `KnownServer` — a remote server (on another instance), identified by `(RemoteId, InstanceUrl)`
- `UserJoinedKnownServer` — tracks which local users have joined which remote servers (so the home instance knows the user's full server list)

### Join Flow (User on Instance A joins Server on Instance B)

1. User POSTs join request to **home instance A**
2. Instance A federates to **instance B**: creates `KnownUser` + `ServerMember` on B
3. Instance A also records `KnownServer` (for instance B's server) + `UserJoinedKnownServer` locally, so it knows the user's membership

> `KnownServer` and `UserJoinedKnownServer` tables exist in the schema but the home-instance-side of this flow is not yet implemented.

### Message Delivery

Server messages are **not federated**. A message posted to a server stays on the hosting instance's DB. Remote members connect directly to the hosting instance via SignalR/API to read and post messages. The home instance is not involved.

### Notification Federation

When a user on instance A is mentioned on instance B, instance B calls `POST /federation/v1/notification/push` on instance A (the user's home instance), which delivers it via SignalR to the client.

### Voice Federation

The instance hosting the channel is authoritative for voice state. When a remote user joins:
- Their client calls the hosting instance's federation voice endpoint
- The hosting instance manages `VoiceStateService` and broadcasts events
- Remote instances (where other participants are) receive `participant-joined/left` push notifications

### Server-to-Server Auth

Each instance signs outbound requests with RSA-SHA256 (`FederationSigningHandler`). Headers: `X-Instance`, `X-Timestamp`, `X-Signature`. Receiving instances verify the signature against the sender's public key fetched from `/.well-known/fennec/public-key`. Requests older than 30 seconds are rejected.

### Future: Direct Messages

DMs will route through the home instance: user sends to home instance → home instance federates to all participants' home instances.
