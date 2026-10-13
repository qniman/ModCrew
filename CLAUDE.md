# ModCrew — notes for Claude

Mod organizer for qniman and friends: packs (mod lists + declarative install steps) installed with one
button, exact rollback. Friends are not PC-savvy: UI text must be simple, everything automatic.

## Working agreement
- Talk to the user in **Russian**. UI text is Russian (no i18n yet).
- **Agree the logic with the user before implementing** a new system; small fixes can go straight in.
- **No Claude attribution anywhere**: no `Co-Authored-By` trailers, nothing in PRs.
  Author `qniman <72304432+qniman@users.noreply.github.com>`; on a fresh clone set `git config user.name/user.email`.
- Extend `TESTING.md` (manual checklist) with every feature or fix; installer logic gets unit tests.
- Commits/comments in English.

## Layout
| | |
|---|---|
| `src/ModCrew/MainForm.cs` | Window, WebView2 setup; UI is embedded (`wwwroot/*` resources) and served on `app.modcrew` via WebResourceRequested; folder hosts `packs.modcrew`, `steam.modcrew` (Steam librarycache) |
| `Program.cs`, `Shell.cs` | Single instance (mutex + named pipe hands files to the running window), `.modcrew` association in HKCU (re-pointed to the running exe on every start) |
| `Saves/SaveBackups.cs` | Zip snapshots of a game's save folder (`pack.saves` or the known-games table), auto before install/update/repair/uninstall (keep 15), restore snapshots the current state first |
| `Install/InstallService.cs` (verify/repair) | Journal entries carry size/mtime/sha256 of what the install left; `Verify` lists changed files; `RepairAsync` turns files replaced by a game update into the new originals, then reinstalls |
| `Update/Updater.cs` | GitHub Releases (`qniman/ModCrew`): latest release API, newer `ModCrew.exe` asset (sha256 from asset `digest` or `.sha256`), swap by renaming the running exe to `.old`; dev builds (`\bin\`) never update |
| `Update/Setup.cs` | Per-user installer (exe named `*Setup*` → setup mode, `#setup` UI page), shortcuts via IShellLink, HKCU Uninstall entry, `--uninstall` (MessageBox flow) |
| `Settings.cs` | settings.json: game paths, `AutoUpdate`, `Appearance` (theme dark/light/system, accent preset, scale = WebView zoom, animations) |
| `ui/src/lib/theme.js`, `app.css` | Theme tokens: `--fg-rgb` overlays, accent via `color-mix`, `.dark-island` for blocks over game art, `.reduce-motion` |
| `build-final.ps1`, `.github/workflows/release.yml` | Local: refresh `..\App`, `..\Installer`. CI: tag `v*` → release with ModCrew-Setup.exe, ModCrew.exe, .sha256 |
| `Properties/PublishProfiles/Friends.pubxml` | `dotnet publish src/ModCrew -p:PublishProfile=Friends` → one self-contained `publish\ModCrew.exe` |
| `Bridge.cs` | UI↔host messages `{id, method, params}` → `{id, ok, result/error}`, events `{event, data}`, `Ask()` confirm in UI |
| `Api.cs` | All bridge methods (`games.*`, `packs.*`, `install.*`, `shell.openUrl`) |
| `Steam/` | Registry → `libraryfolders.vdf` → `appmanifest_*.acf`; cached images |
| `Packs/` | `PackManifest` (pack.json, flat `Step`), `PackLibrary` (import folder/.modcrew, export) |
| `Packs/Authoring.cs` | Editor helpers: `Slug`, `Inspect` (zip tree + where-to-put guess vs the game folder), `Check` (problems by mod/step) |
| `Install/Downloader.cs` | Shared https download with progress + sha256 (installer and «Мод по ссылке» probe; probes land in the download cache) |
| `ui/src/pages/PackEditor.svelte`, `ui/src/lib/editor/` | Editor: `steps.js` (field schema per action, step generation from the wizard, `_`-prefixed editor-only keys stripped on save), `StepCard`, `PathField`, `AddModDialog`, `NewPackDialog` |
| `Install/` | `PathResolver` (path vars + allowed roots), `Journal` (created/backup entries, rollback), `Installer` (step actions), `InstallService` (one pack per game, auto-rollback on failure), `Ini` |
| `ui/` | Svelte 5 (runes). `lib/api.js` bridge + `lib/mock.js` for browser dev, `lib/state.svelte.js` global state/actions, `pages/` |
| `tests/ModCrew.Tests` | xunit: install/uninstall round-trip, rollback, path sandbox, parsers |

## Rules that matter
- Steps are declarative on purpose (safety + exact rollback). No arbitrary scripts. `run` always asks the player.
- Every write outside `{work}` goes through `Journal` first. Writes limited to `{game}`, `{work}`, subfolders of user dirs; ModCrew's own data is off-limits.
- One installed pack per game; reinstall = uninstall old + install new.
- Bridge: sync handlers via `On`, async via `OnAsync` (don't merge them — a lambda returning null picked the Task overload).
- Csproj builds the UI (`npm run build`) and embeds `ui/dist/**` as `wwwroot/…` resources; `-p:SkipUi=true` to skip (then the exe has no UI).

## Where things are
- This repo lives in `C:\Users\qsi\Desktop\ModCrewFinal\Source` (moved from E:\Projects\ModCrew on 2026-10-13; the old copy is not used).
  `..\App` and `..\Installer` are build outputs (`build-final.ps1`). Releases: bump `<Version>` in the csproj, tag `vX.Y.Z`, push.
- Colours in components: use tokens (`var(--text)`, `rgb(var(--fg-rgb) / a)`, `color-mix(in srgb, var(--accent) N%, transparent)`), never dark-only literals — the light theme depends on it.

## Decisions
- Mods with their own installer (BeamMP…): wizard action «Запустить установщик» → extract + `run`. Not rolled back; friends are told on the pack page, in the run prompt and in the uninstall prompt.
- Drag & drop: JS sends `File` objects via `postMessageWithAdditionalObjects`, `Bridge.Files` has the real paths. `.modcrew` → import anywhere; other files → editor wizard (`dropTarget.handler`).
- Nexus mods: the author downloads them and embeds the file in the pack («Мод из файла»); no manual-download step.
- Files picked from disk are always embedded into `files/`. URLs only for direct https downloads (GitHub releases).
- Saving from the editor rewrites pack.json (comments are lost).

## Next (agreed direction, not built)
- Hosting packs (GitHub repo/Releases) + update notifications; for now packs are files sent to friends.
- 7z/rar archives, self-update of ModCrew.exe.
