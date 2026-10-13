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

    public Api(Bridge bridge, Form owner)
    {
        this.bridge = bridge;
        this.owner = owner;
        installs = new InstallService(library, AppPaths.Installs, AppPaths.Cache, protectedDir: AppPaths.Data);

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
                coverUrl = p.Cover == null ? null : $"https://{PacksHost}/{p.Id}/{p.Cover.Replace('\\', '/')}?v={Uri.EscapeDataString(p.Version)}",
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
