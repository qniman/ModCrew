using System.Diagnostics;
using System.Text.Json;
using ModCrew.Install;
using ModCrew.Packs;
using ModCrew.Steam;

namespace ModCrew;

/// <summary>Everything the UI can call. Method names are "area.verb".</summary>
sealed class Api
{
    public const string AppHost = "app.modcrew";
    public const string PacksHost = "packs.modcrew";
    public const string SteamHost = "steam.modcrew";

    readonly Bridge bridge;
    readonly Form owner;
    readonly Settings settings = Settings.Load();
    readonly PackLibrary library = new(AppPaths.Packs);
    readonly InstallService installs;
    readonly string? steamPath = SteamLocator.SteamPath();
    List<SteamGame>? steamGames;
    bool busy;

    public string? SteamPath => steamPath;

    /// <summary>Result of opening files from Explorer (command line, second start, drag &amp; drop).</summary>
    sealed record Opened(List<string> Packs, List<string> Errors, List<string> Other);

    Opened? pendingOpen;

    public Api(Bridge bridge, Form owner, string[] startFiles)
    {
        this.bridge = bridge;
        this.owner = owner;
        installs = new InstallService(library, AppPaths.Installs, AppPaths.Cache, protectedDir: AppPaths.Data);
        if (startFiles.Length > 0) pendingOpen = Import(startFiles);

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
            version = Application.ProductVersion.Split('+')[0],
            dataDir = AppPaths.Data,
            steamFound = steamPath != null,
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

    async Task<object?> Install(string id)
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
            await installs.InstallAsync(id, dir, progress, bridge.Ask);
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
        installs.Uninstall(id);
        return null;
    }
}
