using ModCrew.Packs;

namespace ModCrew.Install;

/// <summary>What is installed: saved as installs\&lt;packId&gt;\install.json next to the backups.</summary>
public sealed class InstallRecord
{
    public string PackId { get; set; } = "";
    public string PackName { get; set; } = "";
    public string Version { get; set; } = "";
    public int GameAppId { get; set; }
    public string GameDir { get; set; } = "";
    public DateTime InstalledAt { get; set; }
    /// <summary>False while installing: a record left like this means ModCrew crashed mid-install.</summary>
    public bool Complete { get; set; }
    public List<JournalEntry> Journal { get; set; } = new();
}

/// <summary>
/// Installs and removes packs. One pack per game: a pack is "the whole mod setup" for that game,
/// so installing another one first removes the current one (after asking).
/// </summary>
public sealed class InstallService
{
    readonly string installsDir;
    readonly string cacheDir;
    readonly string? protectedDir;
    readonly PackLibrary library;

    public InstallService(PackLibrary library, string installsDir, string cacheDir, string? protectedDir = null)
    {
        this.library = library;
        this.installsDir = installsDir;
        this.cacheDir = cacheDir;
        this.protectedDir = protectedDir;
        Directory.CreateDirectory(installsDir);
    }

    string RecordDir(string packId) => Path.Combine(installsDir, packId);
    string RecordPath(string packId) => Path.Combine(RecordDir(packId), "install.json");

    public List<InstallRecord> Installed()
    {
        var result = new List<InstallRecord>();
        foreach (var dir in Directory.EnumerateDirectories(installsDir))
        {
            var record = Json.TryRead<InstallRecord>(Path.Combine(dir, "install.json"));
            if (record != null) result.Add(record);
        }
        return result;
    }

    public InstallRecord? Get(string packId) => Json.TryRead<InstallRecord>(RecordPath(packId));

    public async Task InstallAsync(string packId, string gameDir, IProgress<InstallProgress> progress,
        AskUser ask, CancellationToken cancel = default)
    {
        var pack = library.Get(packId);
        if (!Directory.Exists(gameDir)) throw new DirectoryNotFoundException($"Папка игры не найдена: {gameDir}");

        var existing = Get(packId);
        if (existing != null)
        {
            progress.Report(new InstallProgress(0, 0, "", "Убираю предыдущую версию сборки…"));
            Uninstall(packId);
        }
        var other = Installed().FirstOrDefault(r => r.GameAppId == pack.Game.SteamAppId && r.PackId != packId);
        if (other != null)
        {
            if (!await ask("В игре уже стоит другая сборка",
                    $"Сейчас установлена «{other.PackName}». Чтобы поставить «{pack.Name}», её нужно удалить. Удалить?",
                    "Удалить и продолжить"))
                throw new OperationCanceledException("Установка отменена.");
            progress.Report(new InstallProgress(0, 0, "", $"Удаляю «{other.PackName}»…"));
            Uninstall(other.PackId);
        }

        var recordDir = RecordDir(packId);
        if (Directory.Exists(recordDir)) Directory.Delete(recordDir, recursive: true);
        var journal = new Journal(Path.Combine(recordDir, "backup"));
        var record = new InstallRecord
        {
            PackId = pack.Id, PackName = pack.Name, Version = pack.Version,
            GameAppId = pack.Game.SteamAppId, GameDir = gameDir, InstalledAt = DateTime.Now,
            Journal = journal.Entries,
        };
        void Save() => Json.Write(RecordPath(packId), record);
        Save();

        var work = Path.Combine(cacheDir, "work", Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(work);
        try
        {
            var paths = new PathResolver(gameDir, library.DirOf(packId), work, protectedDir);
            var installer = new Installer(pack, paths, journal, Path.Combine(cacheDir, "downloads"), progress, ask, cancel);
            await Task.Run(() => installer.RunAsync(afterStep: Save), cancel);
            record.Complete = true;
            Save();
        }
        catch
        {
            progress.Report(new InstallProgress(0, 0, "", "Ошибка — возвращаю всё как было…"));
            journal.Rollback();
            Directory.Delete(recordDir, recursive: true);
            throw;
        }
        finally
        {
            TryDeleteDir(work);
        }
    }

    /// <summary>Restores the game folder to how it was before the pack. Returns files that could not be restored.</summary>
    public List<string> Uninstall(string packId)
    {
        var record = Get(packId);
        if (record == null) return new();
        var dir = RecordDir(packId);
        var errors = new Journal(Path.Combine(dir, "backup"), record.Journal).Rollback();
        if (errors.Count > 0)
            throw new IOException("Не всё удалось вернуть (возможно, игра запущена):\n" + string.Join("\n", errors.Take(5)));
        Directory.Delete(dir, recursive: true);
        return errors;
    }

    static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
