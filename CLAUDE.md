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

**Federation** — Server-to-server communication via `FederationClient/` and `FederationApi/` controllers. HTTP requests are signed with `FederationSigningHandler`. Users register on a "home instance" and can join servers on other instances.

**Desktop UI** — Avalonia MVVM with CommunityToolkit.MVVM. `ViewModels/` bind to `Views/` (XAML). Navigation via `Routes/` + `Routing/` layer. DI configured in `App.axaml.cs`.

**Voice** — WebRTC peer-to-peer calls via SIPSorcery + PortAudio for audio I/O. `VoiceStateService` manages call state on the API side.

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
