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

Three files, deliberately minimal (no DB, no OAuth server, no interfaces):

- `Program.cs` — Host wiring: `DiscordSocketClient` (GatewayIntents.None), `InteractionService`, global command registration on Ready. Fails fast with a clear message if token/appId are blank (note: `appsettings.json` ships empty strings, so guards must use `IsNullOrWhiteSpace`, not `?? throw`).
- `WidgetService.cs` — Owns the field definitions (`StringFields` = type 1, `ImageFields` = type 3 wrapped as `{url}`), persists `userId → field → value` to `data/widgets.json`, and `PushAsync` builds/sends the PATCH payload. Non-2xx responses throw with the response body included.
- `WidgetModule.cs` — `/widget` command group. Field names are duplicated as `[Choice]` attributes here (Discord validates them server-side); **if you change a field name, update both `WidgetService` arrays and the `[Choice]` lists on `set`/`image`/`clear`.** Field names must exactly match the Data Field names configured in the Developer Portal widget editor.

Constraints that must not be "fixed":

- The bot is **user-install only**: `[IntegrationType(ApplicationIntegrationType.UserInstall)]` + `[CommandContextType(Guild, BotDm, PrivateChannel)]` on the module class. Do not add guild-install; the identity API doesn't need guild membership.
- All replies are ephemeral. Commands that push defer first (`PushAndReportAsync`) because the API call can exceed the 3s interaction window.
- The payload intentionally omits `username` — only `data.dynamic` is sent.
- Config keys are `Discord:Token` / `Discord:ApplicationId` (env vars `DISCORD__TOKEN` / `DISCORD__APPLICATIONID`).

Since June 2026, only the application owner can add the widget to their profile, and enabling it requires manual Developer Portal + browser-console experiment steps — see the plan doc for the checklist. Nothing in this repo can automate those.
