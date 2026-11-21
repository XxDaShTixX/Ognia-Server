# 🔥 Ognia-Server

<p align="center">
  <img src="/Assets/Brand/Logo/Ognia.png" alt="Ognia logo" width="200"/>
</p>

ASP.NET Core backend API for [Ognia-WPF](https://github.com/XxDaShTixX/Ognia-WPF). Handles user accounts, playlist syncing, and track metadata lookups while leaving all audio downloading and playback to the client.

## ✨ Features

- **Authentication:** Email/password registration and login with JWT + refresh tokens
- **Account management:** Forgot password, reset password, and change password flows
- **Guest migration:** Bulk-imports a local guest library on first account login
- **Playlists:** Create, rename, delete, and reorder playlists per user
- **Tracks:** Add tracks by URL, rename, and remove
- **Metadata resolution:** Resolves track title, duration, and thumbnail via `YoutubeExplode`
- **Extensible source pipeline:** Built around `ITrackSourceService` to support multiple media sources
- **Interactive API docs:** Built-in Swagger UI at `/swagger`

## 🏗️ Architecture

```
+----------------+   HTTPS/JSON   +------------------+
|  WPF (client)  | <------------> |  ASP.NET Core    |
|                |  JWT bearer    |  (Server)       |
+----------------+                +------------------+
											|
											v
									SQLite database
									(users, playlists,
									tracks, tokens)
```

The server owns user identities, playlists, and track metadata. It **never** downloads, stores, or streams audio bytes — when a user adds a track URL, `Ognia-Server` resolves and stores its metadata (`SourceName`, `SourceId`, title, duration, thumbnail), and `Ognia-WPF` downloads the audio directly to the user's machine.

## 🧰 Tech Stack

- **.NET 10** & **ASP.NET Core Web API**
- **Database:** Entity Framework Core + SQLite
- **Auth:** ASP.NET Core Identity + JWT Bearer
- **Metadata & Docs:** `YoutubeExplode`, Swashbuckle (Swagger)

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: `dotnet tool install --global dotnet-ef`

### Setup & Run

```bash
dotnet user-secrets set "Jwt:Key" "<64-char-random-hex>"
dotnet ef database update
dotnet run
```

Generate a random 64-character hex key in PowerShell:

```powershell
[Convert]::ToHexString((1..32 | ForEach-Object { Get-Random -Max 256 })).ToLower()
```

The API runs at `https://localhost:7235` by default (matching `Ognia-WPF`'s default configuration), with Swagger UI available at `https://localhost:7235/swagger`.

## 📁 Storage

- **Database:** `database.db` (SQLite, generated in the project root by `dotnet ef database update`)

## 🎵 Track Sources

Each track is stored as a `(SourceName, SourceId)` pair — e.g. `("youtube", "dQw4w9WgXcQ")`. All of these URLs normalize to the same track:

- `https://www.youtube.com/watch?v=dQw4w9WgXcQ`
- `https://youtu.be/dQw4w9WgXcQ`
- `https://music.youtube.com/watch?v=dQw4w9WgXcQ&t=42s`

### ➕ Adding a New Source

1. Implement `ITrackSourceService` in `Services/TrackSourceService/`.
2. Add a `TrackSource` enum member and its wire name in `TrackSourceExtensions`.
3. Register it in `Program.cs`.

> **Note:** `Program.cs` currently registers a single `ITrackSourceService` (`YouTubeTrackSourceService`) and the add-track endpoint resolves that single instance. To support multiple sources simultaneously, register them as a collection (`IEnumerable<ITrackSourceService>`) and dispatch by `CanHandle(url)` — the interface is already designed for this.

## 🤝 Contributing

Contributions are welcome — this started as a personal project, and it's better with more eyes on it.

### 🐛 Found a bug or want a feature?

Open an [issue](https://github.com/XxDaShTixX/Ognia-Server/issues). Both are equally welcome.

- **Bugs** — describe what you expected, what happened, and how to reproduce it. Stack traces or Swagger request payloads help.
- **Features** — describe the use case, not just the solution. "I want to X so that Y" is more useful than "add an endpoint called Z."

### 🛠️ Submitting code

1. **Fork** the repo (or, if you have access, branch directly).
2. **Create a branch named after yourself and the change:**
   ```bash
   git checkout -b YourName-ShortDescription
   # e.g. Dash-AddSoundCloudSource, Dash-FixRefreshTokenExpiry
   ```
3. **Make your change.** Keep commits focused; a single feature or fix per branch is ideal.
4. **Test it.** `dotnet build` must pass. Run the server, verify endpoints via `/swagger` (and against [Ognia-WPF](https://github.com/XxDaShTixX/Ognia-WPF) if changing DTOs or auth flows).
5. **Open a pull request** against `main`. Describe what changed, note any breaking DTO/route changes for the WPF client, and reference any related issue (`Closes #12`).

### 🤖 AI-assisted contributions

Using AI tools (Copilot, Claude, ChatGPT, Cursor, etc.) is fine. You don't need to disclose it. What matters is that you understand what you're submitting — you should be able to explain every line of your diff, debug it if it breaks, and stand behind it in review. "The model wrote it" is not a defense when something goes wrong.

### 🔍 What I'll look at in a PR

- **Does it work?** Clean build, no errors or warnings, no obvious regressions.
- **Does it fit the existing structure?** Clean separation of controllers, DTOs, services, and `ITrackSourceService` implementations.
- **Does it keep the client contract intact?** Any DTO or endpoint changes are clearly documented so `Ognia-WPF` stays in sync.
- **Is the diff small?** Large sweeping refactors are harder to review than focused changes.

### 🚫 What I won't merge

- Secrets in commits (JWT keys, connection strings, or anything in `appsettings.json` that looks like a real credential).
- Audio downloading/proxying through the server — audio bytes stay strictly client-side.
- New source integrations that violate a platform's terms of service.

## 🔗 Related

- **[Ognia-WPF](https://github.com/XxDaShTixX/Ognia-WPF)** — Windows desktop client for offline audio downloads, playback, themes, and guest mode.

## ⚖️ Legal

This is an educational project. It does **not** host, download, distribute, or own any copyrighted audio — it only stores user-created playlist records and public video metadata.

## 📜 License

MIT — see [LICENSE](LICENSE).