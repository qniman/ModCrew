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
| `src/ModCrew/MainForm.cs` | Window, WebView2 setup, virtual hosts `app.modcrew` (wwwroot), `packs.modcrew`, `steam.modcrew` (Steam librarycache) |
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
- Csproj builds the UI (`npm run build`) and links `ui/dist/**` into `wwwroot`; `-p:SkipUi=true` to skip.

## Decisions
- Nexus mods: the author downloads them and embeds the file in the pack («Мод из файла»); no manual-download step.
- Files picked from disk are always embedded into `files/`. URLs only for direct https downloads (GitHub releases).
- Saving from the editor rewrites pack.json (comments are lost).

## Next (agreed direction, not built)
- Hosting packs (GitHub repo/Releases) + update notifications; for now packs are files sent to friends.
- File association for `.modcrew` (double-click to import), drag & drop import, 7z/rar, app icon, self-update.
