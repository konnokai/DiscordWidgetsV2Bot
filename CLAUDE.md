# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A single-user Discord bot (C# / Discord.Net, .NET 10) that lets the application owner edit custom "Widgets V2" profile widget fields via `/widget` slash commands. It pushes field data to an **undocumented, experimental** Discord endpoint:

```
PATCH https://discord.com/api/v9/applications/{appId}/users/{userId}/identities/0/profile
Body: { "data": { "dynamic": [ { "type": 1|3, "name": "...", "value": ... } ] } }
```

This endpoint is not in official Discord docs and not wrapped by Discord.Net — it is called with a raw `HttpClient` and may change without notice. Sources: chloecinders.com/blog/discord-widgets, rohan.run/writing/discord-widgets. The full design rationale is in `discord-widgets-v2-shimmering-ocean.md`.

## Commands

```powershell
dotnet build          # build
dotnet run            # run locally (needs DISCORD__TOKEN / DISCORD__APPLICATIONID env vars or appsettings values)
docker compose up -d --build   # deploy (on a machine with Docker; this dev machine has none)
```

There are no tests. Real verification requires a live bot token: run, then `/widget set` → expect HTTP 2xx from `/widget refresh`.

## Architecture

Deliberately minimal (no DB, no OAuth server, no interfaces):

- `Program.cs` — Host wiring: `DiscordSocketClient` (GatewayIntents.None), `InteractionService`, global command registration on Ready. Fails fast with a clear message if token/appId are blank (note: `appsettings.json` ships empty strings, so guards must use `IsNullOrWhiteSpace`, not `?? throw`).
- `WidgetService.cs` — Owns the field definitions: the `StringField` (type 1) and `ImageField` (type 3, wrapped as `{url}`) enums are the **single source of truth** — each member's `[ChoiceDisplay]` carries the API field name, slash-command options are auto-generated from the enums, and the `StringFields`/`ImageFields` name arrays are derived via `ApiName()` (reflection over `ChoiceDisplay`). **To rename/add a field, edit only the enum**; the name must exactly match the Data Field name in the Developer Portal widget editor. Also persists `userId → field → value` to `data/widgets.json`; `PushAsync` builds/sends the PATCH payload and throws with the response body on non-2xx. Current config (19 fields): Widget Top = `top-title`, `top-sub-title-1`, `top-image`, `top-sub-icon-1`; Widget Bottom entries 1–4 = `bottom-name-N`, `bottom-description-N`, `bottom-image-N`; Mini Profile = `mini-profile-stat-text`, `mini-profile-stat-icon`, `mini-profile-contained-image`.
- `WidgetModule.cs` — `/widget` command group; field parameters are the enums above (Discord validates choices server-side). `clear` takes string fields, `clear-image` takes image fields — two commands because one parameter can't merge two enums.
- `TwitchService.cs` / `TwitchModule.cs` — optional Twitch live detection (`/twitch bind|unbind`). A `BackgroundService` validates `Twitch:ClientId`/`Twitch:ClientSecret` via client-credentials flow at startup — missing or invalid credentials just disable the feature (`IsEnabled` stays false, commands reply "未啟用") and must never break the rest of the bot. When enabled it polls Helix `/streams` every 30s per binding (`data/twitch.json`) and writes the per-binding target field chosen at bind time (default `bottom-description-3`) with `正在 Twitch 開台中!` / `上次直播時間: …`, pushing only on actual text change so the identity API is not hit every tick. Push failures are reported to the user via DM (once per transition — the stored value makes the next tick a no-op, so no retry/DM spam). Last-live time is seeded from the newest VOD at bind and updated on the live→offline transition.

Constraints that must not be "fixed":

- The bot is **user-install only**: `[IntegrationType(ApplicationIntegrationType.UserInstall)]` + `[CommandContextType(Guild, BotDm, PrivateChannel)]` on the module class. Do not add guild-install; the identity API doesn't need guild membership.
- All replies are ephemeral. Commands that push defer first (`PushAndReportAsync`) because the API call can exceed the 3s interaction window.
- The payload intentionally omits `username` — only `data.dynamic` is sent.
- Config keys are `Discord:Token` / `Discord:ApplicationId` (env vars `DISCORD__TOKEN` / `DISCORD__APPLICATIONID`); optional `Twitch:ClientId` / `Twitch:ClientSecret` (`TWITCH__CLIENTID` / `TWITCH__CLIENTSECRET`).

Since June 2026, only the application owner can add the widget to their profile, and enabling it requires manual Developer Portal + browser-console experiment steps — see the plan doc for the checklist. Nothing in this repo can automate those.
