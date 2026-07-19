# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Design context

`Web`'s UI work is governed by `PRODUCT.md` (register: `product`; platform: `web`) — read it before any design/UX task in `src/Web`. Short version: single-operator control room for AndiBanterBot, built for the streamer to use live, mid-stream, at a glance. Personality is playful/characterful (matches the bot's own chat voice), explicitly not a generic SaaS admin template — the current stock Bootstrap scaffold is the anti-reference, not the target. Status indicators must never rely on color alone, and motion should stay calm rather than draw attention away from the actual stream. No `DESIGN.md` yet (nothing worth capturing in the stock template); the `/impeccable` skill can generate one once a real surface exists.

## What this is

AndiBanterBot is a Twitch chat bot for https://www.twitch.tv/littleandi77. The project is mid-refactor: the `pre-aspire` git tag marks the last commit before the codebase was split into small .NET Aspire-orchestrated services. The ongoing direction is to keep reducing third-party dependencies (TwitchLib was fully removed in favor of direct Twitch Helix/EventSub calls — see `git log --grep TwitchLib`) and to keep splitting responsibilities into separate services under Aspire. Treat anything under `src/Host.Old` on disk as leftover local build output, not source — it was deleted from git in #47 and isn't part of the solution.

A near-term goal is turning the Web project into a monitoring/status dashboard for the streamer (EventSub connection health, chat activity, PUBG match info, etc.), not just the OAuth login pages it has today.

## Commands

```bash
dotnet restore
dotnet build AndiBanterBot.sln

# Run everything the way it's meant to run: via the Aspire AppHost.
# Launches TwitchService, Web, and PubgService together, wires Aspire
# service discovery between them, and opens the Aspire dashboard.
dotnet run --project src/Host
```

There are no test projects in the solution (`dotnet test` is a no-op). The CI workflow (`.github/workflows/dotnet.yml`) pins the .NET 8 SDK while every project targets `net10.0` — it's stale; don't treat it as the source of truth for how to build.

### Running a single service standalone (without the AppHost)

Each service has its own `Properties/launchSettings.json` and can be run directly, e.g. `dotnet run --project src/TwitchService`. This is useful for iterating without the Aspire dashboard, but **Web's HttpClient to TwitchService uses Aspire service discovery** (`client.BaseAddress = new("https+http://twitch")` in `src/Web/Program.cs`), which only resolves when the AppHost injects `services__twitch__*` env vars. Running Web standalone requires setting that yourself, e.g.:

```bash
services__twitch__http__0=http://localhost:5041 dotnet run --project src/Web
```

(match the port to whatever launch profile you started TwitchService with).

## Architecture

### Project map

| Project | Role |
|---|---|
| `Host` | Aspire AppHost. `Program.cs` is the actual source of truth for what runs: it calls `AddProject` for `PubgService`, `TwitchService`, and `Web` only. It also loads `appsettings.prompts.json` as a config overlay, but see the AI section below — nothing currently consumes it. |
| `TwitchService` | The real, currently-active Twitch integration. Minimal-API service: OAuth token exchange/refresh (`TwitchTokenStore`, role-based for Bot vs Broadcaster), a hand-rolled EventSub websocket client with reconnect/keepalive supervision (`TwitchEventSubSupervisorService`, `TwitchWebSocketService`), and Helix calls for chat send (`TwitchChatService`) and user lookup (`TwitchUserApi`). Has **no ProjectReference to any other project** — fully self-contained, and doesn't reference `ServiceDefaults` either (see below). |
| `Web` | Blazor Server UI. Currently just the Twitch OAuth login/status pages (`/twitchlogin`, `/broadcaster/login`, callback handlers) that talk to `TwitchService` over HTTP. This is where the planned streamer-facing monitoring dashboard would grow. |
| `PubgService` | Standalone worker (generic `Host.CreateApplicationBuilder`, not a web host) that polls the PUBG API for new matches and stores them to Azure Blob Storage. AI commentary/chat-announcement of new matches is commented out in `PubgBackgroundService` — this integration is not fully wired up. |
| `Application` | Feature/business logic layer: `MessageProcessor`, `InstructionService`, `RewardService`, OpenAI client wrappers, and Twitch-facing interfaces (`IChatService`, `IClipService`) whose TwitchLib implementations were deleted. **Currently dead code at runtime** — `Host` never references it, and although `Web.csproj` still has a `ProjectReference` to it, nothing in `Web` actually uses any of its types. `AddApplication()`/`AddInfrastructure()` are defined but called from nowhere. This is "the AI parts are temporarily disabled" — reconnecting it to `TwitchService`'s chat events is a real follow-up, not yet done. |
| `Common` | One tiny shared piece: `IConfigurationOptions` + `AddConfigurationOptions<T>`, a convention for binding a config section straight to a singleton options object. |
| `ServiceDefaults` | Standard Aspire cross-cutting concerns (OpenTelemetry, health checks, service discovery, standard HTTP resilience). **Only `Web` references and calls this** (`AddServiceDefaults()`). `TwitchService` and `PubgService` don't opt in, so they get none of it — e.g. the automatic retry-on-502 behavior you'll see in logs when `TwitchService` is unreachable comes from `Web`'s outbound HTTP client, not from any resilience on `TwitchService`'s side. |

### The `Application.*` namespace trap

Three separate, non-referencing projects all use namespaces starting with `Application`:
- The actual `Application` project → `Application.Common`, `Application.Features`, `Application.Infrastructure.*`.
- `PubgService`'s own files (`DependencyInjection.cs`, `PubgBackgroundService.cs`) are declared under `namespace Application;` / `namespace Application.Features;` despite `PubgService.csproj` having no reference to the `Application` project at all — it's just leftover naming.
- `TwitchService`'s files are under `namespace Application.Features.Twitch;`, also with zero reference to the `Application` project.

Don't assume a type is in the `Application` project just because of its namespace — check the file's actual folder/project.

### Twitch integration flow (the part that's live)

1. Bot and Broadcaster accounts each authorize separately via `/twitchlogin` and `/broadcaster/login` in `Web`, which redirect to Twitch OAuth and land back on `Callback.razor` / `PubSubCallback.razor`. Those pages POST the code to `TwitchService`'s `/auth/callback`, which exchanges it and stores the token.
2. `TwitchTokenStore` persists tokens to disk (`%LOCALAPPDATA%/AndiBanterBot/twitch-tokens.json` by default, path configurable via `Twitch:TokenStorePath`), keyed by role (`Bot`/`Broadcaster`), and auto-refreshes on demand via `TwitchAppAuthHandler`/`TwitchUserAuthHandler` (`DelegatingHandler`s attached to the `TwitchClientAppAccess`/`TwitchClientUserAccess` named `HttpClient`s).
3. `TwitchEventSubSupervisorService` (a `BackgroundService`) keeps the EventSub websocket connected once a token exists, handling reconnects and dead-connection detection via the keepalive timeout.
4. Chat receive: `channel.chat.message` EventSub notifications are parsed in `TwitchWebSocketService` and raised as a `ChatMessageReceived` event — nothing subscribes to it downstream yet (this is the reconnection point for the `Application` feature layer).
5. Chat auth model: chat send (`TwitchChatService.SendMessageAsync` → Helix `POST /helix/chat/messages`) authenticates via the app-access client (`TwitchClientAppAccess`/`TwitchAppAuthHandler`, `client_credentials`), relying on the broadcaster's one-time `channel:bot` grant and the bot's one-time `user:bot` grant rather than an ongoing bot user-token refresh. The `channel.chat.message` EventSub subscription (`TwitchWebSocketService.SubscribeToChannelChatMessages`) does **not** use the app-access client, despite also being covered by the same `channel:bot`/`user:bot` grants: Twitch rejects EventSub subscriptions created over WebSocket transport when authenticated with an app access token (`400 invalid transport and auth combination`) — the app-access model only applies to webhook-transport subscriptions and to plain Helix calls, not WebSocket-delivered ones, and this codebase's `TwitchWebSocketService` is WebSocket-only. So subscription creation still authenticates on the Bot's own user token. The stored Bot token is also used as a proxy for "the bot has completed OAuth at least once" (gating whether to attempt the subscription) and to resolve the bot's user id for chat send's `sender_id`. `channel.channel_points_custom_reward_redemption.add` is unaffected and still authenticates on the Broadcaster's user token.

### Config

Twitch client ID/secret live in each service's `appsettings*.json` under a `Twitch` (or legacy `TwitchLib`, in `Web`) section — `appsettings.Development.json` is gitignored per project, so changes made locally won't show up in `git status`/diffs. OAuth scopes are **not** config-driven: `TwitchAuthUrlBuilder` (`src/Web/Components/Services/TwitchAuthUrlBuilder.cs`) hardcodes `BotScopes`/`BroadcasterScopes` as static arrays rather than reading a config section — the plan is for scopes to eventually be derived from whichever features/modules are enabled (possibly user-selectable) rather than a static list, so this is a deliberate placeholder, not an oversight. The README's scopes list is stale on two counts: it predates the Helix chat migration (still lists IRC-era `chat:edit`/`chat:read`/`whispers:read` in a way that doesn't distinguish Bot vs Broadcaster) and it predates this hardcoding — treat `TwitchAuthUrlBuilder`'s arrays as the current source of truth instead. `Host`'s `appsettings.prompts.json` holds the OpenAI system prompts for the (currently dormant) AI layer.
