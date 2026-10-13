using System.Diagnostics;
using System.Text.Json;
using ModCrew.Install;
using ModCrew.Packs;
using ModCrew.Steam;
using ModCrew.Saves;
using ModCrew.Update;

namespace ModCrew;

/// <summary>Everything the UI can call. Method names are "area.verb".</summary>
sealed class Api
{
    public const string AppHost = "app.modcrew";
    public const string PacksHost = "packs.modcrew";
    public const string SteamHost = "steam.modcrew";

    readonly Bridge bridge;
    readonly MainForm owner;
    readonly Settings settings = Settings.Load();
    readonly PackLibrary library = new(AppPaths.Packs);
    readonly InstallService installs;
    readonly SaveBackups saves = new(Path.Combine(AppPaths.Data, "saves"));
    readonly string? steamPath = SteamLocator.SteamPath();
    List<SteamGame>? steamGames;
    bool busy;

    public string? SteamPath => steamPath;

    /// <summary>Result of opening files from Explorer (command line, second start, drag &amp; drop).</summary>
    sealed record Opened(List<string> Packs, List<string> Errors, List<string> Other);

    Opened? pendingOpen;

    UpdateInfo? update;
    bool updateApplied;

    public Api(Bridge bridge, MainForm owner, string[] startFiles, bool setupMode)
    {
        this.bridge = bridge;
        this.owner = owner;
        installs = new InstallService(library, AppPaths.Installs, AppPaths.Cache, protectedDir: AppPaths.Data);
        if (startFiles.Length > 0) pendingOpen = Import(startFiles);

        // Settings page
        bridge.On("settings.get", _ => SettingsView());
        bridge.On("settings.set", p =>
        {
            if (p.TryGetProperty("autoUpdate", out var auto)) settings.AutoUpdate = auto.GetBoolean();
            if (p.TryGetProperty("appearance", out var a))
            {
                settings.Appearance = Json.Parse<Appearance>(a);
                settings.Appearance.Clamp();
                owner.ApplyAppearance(settings.Appearance);
            }
            settings.Save();
            return SettingsView();
        });
        bridge.On("app.openDataDir", _ => OpenFolder(AppPaths.Data));

        // Updates from GitHub Releases
        bridge.OnAsync("update.check", async _ => await CheckUpdate());
        bridge.OnAsync("update.apply", async _ => await ApplyUpdate());
        bridge.On("update.restart", _ =>
        {
            if (busy) throw new InvalidOperationException("Дождись конца установки сборки.");
            owner.BeginInvoke(owner.Restart);
            return (object?)null;
        });

        // ModCrew-Setup.exe
        bridge.On("setup.info", _ => new
        {
            version = Updater.CurrentText,
            installedVersion = Setup.InstalledVersion(),
            dir = Setup.InstallDir,
            desktop = Setup.InstalledVersion() == null || Setup.HasDesktopShortcut,
        });
        bridge.On("setup.install", p =>
        {
            if (!setupMode) throw new InvalidOperationException("Not in setup mode");
            Setup.Install(p.GetProperty("desktop").GetBoolean());
            return (object?)null;
        });
        bridge.On("setup.finish", p =>
        {
            if (p.GetProperty("launch").GetBoolean())
                Process.Start(new ProcessStartInfo(Setup.InstalledExe) { UseShellExecute = true, WorkingDirectory = Setup.InstallDir });
            owner.BeginInvoke(owner.Close);
            return (object?)null;
        });

        // Files the app was started with; the UI asks once it is ready.
        bridge.On("app.takeOpened", _ =>
        {
            var o = pendingOpen;
            pendingOpen = null;
            return o;
        });
        // Files dropped on the window: packs are imported, other files go back to the UI (the editor adds them as mods).
        bridge.On("drop.files", _ => Import(bridge.Files));

        bridge.On("app.info", _ => new
        {
            version = Updater.CurrentText,
            dataDir = AppPaths.Data,
            steamFound = steamPath != null,
            mode = setupMode ? "setup" : "app",
        });

        bridge.On("games.list", p => GamesList(p.ValueKind == JsonValueKind.Object && p.TryGetProperty("refresh", out var r) && r.GetBoolean()));
        bridge.On("games.setPath", p => PickGamePath(Int(p, "appId")));
        bridge.On("games.open", p => OpenFolder(GameDir(Int(p, "appId"))));

        bridge.On("packs.list", _ => PacksList());
        bridge.On("packs.importFile", _ => ImportFile());
        bridge.On("packs.importFolder", _ => ImportFolder());
        bridge.On("packs.export", p => Export(Str(p, "id")));
        bridge.On("packs.remove", p => RemovePack(Str(p, "id")));
        bridge.On("packs.openFolder", p => OpenFolder(library.DirOf(Str(p, "id"))));

        // Authoring (pack editor)
        bridge.On("packs.create", p => Create(Str(p, "name"), Int(p, "appId")));
        bridge.On("packs.save", p => { var id = Str(p, "id"); library.Save(id, Json.Parse<PackManifest>(p.GetProperty("pack"))); return (object?)null; });
        bridge.On("packs.check", p => { var id = Str(p, "id"); return Authoring.Check(library, id, Json.Parse<PackManifest>(p.GetProperty("pack")), Path.Combine(AppPaths.Cache, "downloads")); });
        bridge.On("packs.files", p => library.Files(Str(p, "id")));
        bridge.On("packs.addFile", p => AddFile(Str(p, "id"), Int(p, "appId")));
        bridge.On("packs.addFileFrom", p => AddFileFrom(Str(p, "id"), Str(p, "path"), Int(p, "appId")));
        bridge.On("packs.stepOutputs", p => Authoring.StepOutputs(library, Str(p, "id"), Json.Parse<PackManifest>(p.GetProperty("pack")),
            Int(p, "mod"), Int(p, "step"), Path.Combine(AppPaths.Cache, "downloads")));
        bridge.On("packs.inspectFile", p => InspectPackFile(Str(p, "id"), Str(p, "path"), Int(p, "appId")));
        bridge.On("packs.deleteFile", p => { library.DeleteFile(Str(p, "id"), Str(p, "path")); return (object?)null; });
        bridge.On("packs.setCover", p => SetCover(Str(p, "id")));
        bridge.OnAsync("packs.probeUrl", p => ProbeUrl(Str(p, "url"), Int(p, "appId")));
        bridge.On("paths.pick", p => PickPath(Int(p, "appId"), Str(p, "kind")));

        bridge.OnAsync("install.run", p => Install(Str(p, "id")));
        bridge.On("install.uninstall", p => Uninstall(Str(p, "id")));
        bridge.OnAsync("install.verify", async p =>
        {
            var id = Str(p, "id");
            var changed = await Task.Run(() => installs.Verify(id));
            return new { changed = changed.Take(20).ToList(), count = changed.Count };
        });
        bridge.OnAsync("install.repair", p => Install(Str(p, "id"), repair: true));
        bridge.On("game.launch", p =>
        {
            // Steam starts the game the normal way (overlay, cloud saves, multiplayer).
            Process.Start(new ProcessStartInfo($"steam://rungameid/{Int(p, "appId")}") { UseShellExecute = true });
            return (object?)null;
        });

        // Save backups
        bridge.On("saves.list", p => SavesList(Str(p, "id")));
        bridge.On("saves.backup", p => { var (pack, dir) = SavesOf(Str(p, "id")); return saves.Create(pack.Game.SteamAppId, dir, "вручную", auto: false) ?? throw new InvalidOperationException("Сохранений пока нет — сначала поиграй."); });
        bridge.On("saves.restore", p => RestoreSave(Str(p, "id"), Str(p, "backup")));
        bridge.On("saves.delete", p => { var (pack, _) = SavesOf(Str(p, "id")); saves.Delete(pack.Game.SteamAppId, Str(p, "backup")); return (object?)null; });
        bridge.On("saves.open", p => OpenFolder(SavesOf(Str(p, "id")).dir));

        bridge.On("shell.openUrl", p =>
        {
            var url = Str(p, "url");
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Можно открывать только веб-ссылки.");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return (object?)null;
        });
    }

    object SettingsView() => new
    {
        autoUpdate = settings.AutoUpdate,
        appearance = settings.Appearance,
        version = Updater.CurrentText,
        devBuild = Updater.IsDevBuild,
        installed = Setup.IsInstalledCopy,
        exePath = Updater.ExePath,
        dataDir = AppPaths.Data,
        releasesPage = Updater.ReleasesPage,
        update = update == null ? null : new { update.Version, update.Notes, update.PageUrl, ready = updateApplied },
    };

    // ---------------------------------------------------------------- updates

    /// <summary>Checks GitHub a few seconds after start and then every 6 hours.</summary>
    public void StartUpdateChecks(CancellationToken cancel)
    {
        if (Updater.IsDevBuild) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancel);
            while (!cancel.IsCancellationRequested)
            {
                try
                {
                    await owner.Invoke(async () =>
                    {
                        if (updateApplied) return;
                        var found = await Updater.CheckAsync(cancel);
                        if (found == null) return;
                        update = found;
                        if (settings.AutoUpdate && !busy) await ApplyUpdate();
                        else bridge.Emit("update.available", new { found.Version, found.Notes, found.PageUrl });
                    });
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Offline or GitHub hiccup: try again next round, the player does not need to know.
                }
                await Task.Delay(TimeSpan.FromHours(6), cancel);
            }
        }, cancel);
    }

    async Task<object> CheckUpdate()
    {
        if (Updater.IsDevBuild) return new { status = "dev" };
        if (updateApplied) return new { status = "ready", version = update!.Version };
        update = await Updater.CheckAsync();
        return update == null
            ? new { status = "latest", version = Updater.CurrentText }
            : new { status = "available", version = update.Version };
    }

    async Task<object?> ApplyUpdate()
    {
        if (updateApplied) return new { update!.Version };
        if (busy) throw new InvalidOperationException("Сейчас идёт установка сборки — обновлю ModCrew после неё.");
        update ??= await Updater.CheckAsync() ?? throw new InvalidOperationException("Новых версий нет.");
        var info = update;
        busy = true;
        try
        {
            await Updater.ApplyAsync(info, (done, total) => bridge.Emit("update.progress", new { done, total }));
        }
        finally
        {
            busy = false;
        }
        updateApplied = true;
        bridge.Emit("update.ready", new { info.Version, info.Notes, info.PageUrl });
        return new { info.Version };
    }

    static string Str(JsonElement p, string name) => p.GetProperty(name).GetString() ?? "";
    static int Int(JsonElement p, string name) => p.GetProperty(name).GetInt32();

    // ---------------------------------------------------------------- games

    List<SteamGame> SteamGames(bool refresh = false)
    {
        if (steamGames == null || refresh) steamGames = SteamLocator.Games();
        return steamGames;
    }

    string? GameDir(int appId)
    {
        if (settings.GamePaths.TryGetValue(appId, out var custom) && Directory.Exists(custom)) return custom;
        return SteamGames().FirstOrDefault(g => g.AppId == appId)?.InstallDir;
    }

    string? SteamImage(int appId, string file)
    {
        if (steamPath == null) return null;
        var rel = SteamLocator.CachedImage(steamPath, appId, file);
        return rel == null ? null : $"https://{SteamHost}/{rel.Replace('\\', '/')}";
    }

    object GameImages(int appId) => new
    {
        poster = SteamImage(appId, "library_600x900.jpg"),
        hero = SteamImage(appId, "library_hero.jpg"),
        logo = SteamImage(appId, "logo.png"),
        header = SteamImage(appId, "header.jpg"),
    };

    object GamesList(bool refresh)
    {
        var installed = installs.Installed();
        var packs = library.List();
        return SteamGames(refresh).Select(g =>
        {
            var record = installed.FirstOrDefault(r => r.GameAppId == g.AppId);
            return new
            {
                appId = g.AppId,
                name = g.Name,
                dir = GameDir(g.AppId),
                custom = settings.GamePaths.ContainsKey(g.AppId),
                images = GameImages(g.AppId),
                packCount = packs.Count(p => p.Game.SteamAppId == g.AppId),
                installedPack = record == null ? null : new { id = record.PackId, name = record.PackName, version = record.Version },
            };
        }).ToList();
    }

    object? PickGamePath(int appId)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка, где лежит игра (там, где её .exe)",
            UseDescriptionForTitle = true,
            InitialDirectory = GameDir(appId) ?? "",
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        settings.GamePaths[appId] = dialog.SelectedPath;
        settings.Save();
        return dialog.SelectedPath;
    }

    static object? OpenFolder(string? dir)
    {
        if (dir == null || !Directory.Exists(dir)) throw new DirectoryNotFoundException("Папка не найдена.");
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        return null;
    }

    // ---------------------------------------------------------------- packs

    object PacksList()
    {
        var installed = installs.Installed();
        return library.List().Select(p =>
        {
            var record = installed.FirstOrDefault(r => r.PackId == p.Id);
            var game = SteamGames().FirstOrDefault(g => g.AppId == p.Game.SteamAppId);
            return new
            {
                pack = p,
                coverUrl = CoverUrl(p),
                game = new
                {
                    appId = p.Game.SteamAppId,
                    name = game?.Name ?? p.Game.Name ?? $"App {p.Game.SteamAppId}",
                    found = GameDir(p.Game.SteamAppId) != null,
                    savesDefault = SaveBackups.KnownSaves(p.Game.SteamAppId),
                    images = GameImages(p.Game.SteamAppId),
                },
                installed = record == null ? null : new { version = record.Version, complete = record.Complete, at = record.InstalledAt },
            };
        }).ToList();
    }

    string? CoverUrl(PackManifest p)
    {
        if (p.Cover == null) return null;
        var file = Path.Combine(library.DirOf(p.Id), p.Cover);
        // Cache-buster: the cover file can be replaced under the same name.
        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file).Ticks : 0;
        return $"https://{PacksHost}/{p.Id}/{p.Cover.Replace('\\', '/')}?v={stamp}";
    }

    // ---------------------------------------------------------------- authoring

    object Create(string name, int appId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Введи название сборки.");
        var game = SteamGames().FirstOrDefault(g => g.AppId == appId);
        return library.Create(name, appId, game?.Name, Environment.UserName).Id;
    }

    object? AddFile(string id, int appId)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Файл мода",
            Filter = "Моды и установщики (*.zip;*.dll;*.exe;*.msi)|*.zip;*.dll;*.exe;*.msi|Все файлы|*.*",
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? AddFileFrom(id, dialog.FileName, appId) : null;
    }

    object AddFileFrom(string id, string source, int appId)
    {
        if (!File.Exists(source)) throw new FileNotFoundException($"Файл не найден: {source}");
        var rel = library.AddFile(id, source);
        var info = Authoring.Inspect(library.FilePath(id, rel), GameDir(appId));
        info.PackPath = "{pack}/" + rel;
        return info;
    }

    /// <summary>Opens files from Explorer in the running window (second start / double click).</summary>
    public void OpenFiles(List<string> files)
    {
        if (files.Count == 0) return;
        bridge.Emit("opened", Import(files));
    }

    Opened Import(IEnumerable<string> paths)
    {
        var result = new Opened(new(), new(), new());
        foreach (var path in paths)
        {
            bool isPack = path.EndsWith(PackLibrary.Extension, StringComparison.OrdinalIgnoreCase)
                          || (Directory.Exists(path) && File.Exists(Path.Combine(path, PackLibrary.ManifestName)));
            if (!isPack)
            {
                if (File.Exists(path)) result.Other.Add(path);
                continue;
            }
            try
            {
                result.Packs.Add(library.Import(path).Id);
            }
            catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                result.Errors.Add($"{Path.GetFileName(path)}: {e.Message}");
            }
        }
        return result;
    }

    object InspectPackFile(string id, string path, int appId)
    {
        var rel = path.Replace('\\', '/');
        if (rel.StartsWith("{pack}/", StringComparison.OrdinalIgnoreCase)) rel = rel["{pack}/".Length..];
        var info = Authoring.Inspect(library.FilePath(id, rel), GameDir(appId));
        info.PackPath = "{pack}/" + rel;
        return info;
    }

    object? SetCover(string id)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Обложка сборки",
            Filter = "Картинки (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        var cover = library.SetCover(id, dialog.FileName);
        var pack = library.Get(id);
        pack.Cover = cover;
        return new { cover, url = CoverUrl(pack) };
    }

    /// <summary>Downloads a URL once so the editor can fill in sha256 and show what is inside.</summary>
    async Task<object?> ProbeUrl(string url, int appId)
    {
        Downloader.RequireHttps(url);
        var downloads = Path.Combine(AppPaths.Cache, "downloads");
        var temp = Path.Combine(downloads, "probe-" + Guid.NewGuid().ToString("N")[..8]);
        var sha = await Downloader.DownloadAsync(url, temp,
            (done, total) => bridge.Emit("probe.progress", new { done, total }), CancellationToken.None);
        // Keep it in the install cache: installing the pack afterwards will not download again.
        var cached = Path.Combine(downloads, sha);
        File.Move(temp, cached, overwrite: true);
        var info = Authoring.Inspect(cached, GameDir(appId));
        info.Name = Downloader.FileNameFromUrl(url);
        info.Sha256 = sha;
        info.Url = url;
        return info;
    }

    /// <summary>Folder/file picker that returns a portable path like "{game}/Mods".</summary>
    object? PickPath(int appId, string kind)
    {
        var game = GameDir(appId);
        string? picked;
        if (kind == "file")
        {
            using var dialog = new OpenFileDialog { Title = "Выбери файл", InitialDirectory = game ?? "" };
            picked = dialog.ShowDialog(owner) == DialogResult.OK ? dialog.FileName : null;
        }
        else
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Выбери папку (внутри папки игры)", UseDescriptionForTitle = true, InitialDirectory = game ?? "",
            };
            picked = dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : null;
        }
        if (picked == null) return null;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<(string var, string? dir)>
        {
            ("game", game),
            ("documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("appdata", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            ("localappdata", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            ("locallow", Path.Combine(profile, "AppData", "LocalLow")),
        };
        var full = Path.GetFullPath(picked).TrimEnd('\\');
        foreach (var (name, dir) in roots)
        {
            if (dir == null) continue;
            var root = Path.GetFullPath(dir).TrimEnd('\\');
            if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return $"{{{name}}}";
            if (full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                return $"{{{name}}}/" + full[(root.Length + 1)..].Replace('\\', '/');
        }
        throw new InvalidOperationException("Это место вне папки игры и папок сохранений — у друзей такого пути не будет.");
    }

    object? ImportFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Добавить сборку",
            Filter = "Сборка ModCrew (*.modcrew)|*.modcrew|Все файлы|*.*",
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? library.Import(dialog.FileName).Id : null;
    }

    object? ImportFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Папка сборки (с файлом pack.json)",
            UseDescriptionForTitle = true,
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? library.Import(dialog.SelectedPath).Id : null;
    }

    object? Export(string id)
    {
        var pack = library.Get(id);
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить сборку для друзей",
            Filter = "Сборка ModCrew (*.modcrew)|*.modcrew",
            FileName = $"{pack.Id}-{pack.Version}{PackLibrary.Extension}",
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        library.Export(id, dialog.FileName);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{dialog.FileName}\"") { UseShellExecute = true });
        return dialog.FileName;
    }

    object? RemovePack(string id)
    {
        if (installs.Get(id) != null) throw new InvalidOperationException("Сначала удали сборку из игры.");
        library.Remove(id);
        return null;
    }

    // ---------------------------------------------------------------- install

    async Task<object?> Install(string id, bool repair = false)
    {
        if (busy) throw new InvalidOperationException("Уже идёт установка.");
        busy = true;
        try
        {
            var pack = library.Get(id);
            var dir = GameDir(pack.Game.SteamAppId)
                      ?? throw new DirectoryNotFoundException($"Игра «{pack.Game.Name ?? pack.Game.SteamAppId.ToString()}» не найдена. Установи её в Steam или укажи папку вручную на вкладке «Игры».");
            var progress = new Progress<InstallProgress>(p => bridge.Emit("install.progress", new
            {
                packId = id, step = p.Step, total = p.TotalSteps, mod = p.Mod, text = p.Text, fraction = p.Fraction,
            }));
            ((IProgress<InstallProgress>)progress).Report(new InstallProgress(0, 0, "", "Сохраняю бэкап сохранений…"));
            await Task.Run(() => AutoBackup(pack, dir, repair ? $"перед починкой «{pack.Name}»" : $"перед установкой «{pack.Name}» {pack.Version}"));
            if (repair) await installs.RepairAsync(id, progress, bridge.Ask);
            else await installs.InstallAsync(id, dir, progress, bridge.Ask);
            return null;
        }
        finally
        {
            busy = false;
        }
    }

    object? Uninstall(string id)
    {
        if (busy) throw new InvalidOperationException("Сейчас идёт установка.");
        var pack = library.Get(id);
        AutoBackup(pack, GameDir(pack.Game.SteamAppId), $"перед удалением «{pack.Name}»");
        installs.Uninstall(id);
        return null;
    }

    // ---------------------------------------------------------------- saves

    /// <summary>Snapshot before something touches the game. Never blocks the install: a failed backup only warns.</summary>
    void AutoBackup(PackManifest pack, string? gameDir, string reason)
    {
        try
        {
            var dir = SaveBackups.SavesDir(pack, gameDir);
            if (dir != null) saves.Create(pack.Game.SteamAppId, dir, reason, auto: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            bridge.Emit("toast", new { text = $"Не получилось сделать бэкап сохранений: {e.Message}", kind = "error" });
        }
    }

    (PackManifest pack, string dir) SavesOf(string id)
    {
        var pack = library.Get(id);
        var dir = SaveBackups.SavesDir(pack, GameDir(pack.Game.SteamAppId))
                  ?? throw new InvalidOperationException("Для этой игры не указана папка сохранений — её можно задать в редакторе сборки.");
        return (pack, dir);
    }

    object SavesList(string id)
    {
        var pack = library.Get(id);
        string? dir;
        try { dir = SaveBackups.SavesDir(pack, GameDir(pack.Game.SteamAppId)); }
        catch (InvalidDataException) { dir = null; }
        catch (UnauthorizedAccessException) { dir = null; }
        return new
        {
            dir,
            exists = dir != null && Directory.Exists(dir),
            backups = dir == null ? new List<SaveBackup>() : saves.List(pack.Game.SteamAppId),
        };
    }

    object? RestoreSave(string id, string backup)
    {
        if (busy) throw new InvalidOperationException("Сейчас идёт установка.");
        var (pack, dir) = SavesOf(id);
        var gameDir = GameDir(pack.Game.SteamAppId);
        if (gameDir != null && GameRunning(gameDir))
            throw new InvalidOperationException("Сначала закрой игру — иначе она перезапишет сохранение при выходе.");
        saves.Restore(pack.Game.SteamAppId, backup, dir);
        return null;
    }

    /// <summary>Any process started from the game folder.</summary>
    static bool GameRunning(string gameDir)
    {
        var root = Path.GetFullPath(gameDir).TrimEnd('\\') + "\\";
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainModule?.FileName?.StartsWith(root, StringComparison.OrdinalIgnoreCase) == true) return true;
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            finally { p.Dispose(); }
        }
        return false;
    }
}
