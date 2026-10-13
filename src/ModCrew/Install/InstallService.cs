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
            progress.Report(new InstallProgress(0, 0, "", "Запоминаю установленные файлы…"));
            await Task.Run(() => Fingerprint(record.Journal), cancel);
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

    // ---------------------------------------------------------------- verify & repair

    static void Fingerprint(List<JournalEntry> entries)
    {
        foreach (var e in entries.Where(e => e.Kind is "file" or "backup"))
        {
            var info = new FileInfo(e.Path);
            if (!info.Exists)
            {
                e.Size = -1;
                e.Ticks = null;
                e.Sha256 = null;
                continue;
            }
            e.Size = info.Length;
            e.Ticks = info.LastWriteTimeUtc.Ticks;
            e.Sha256 = Downloader.Sha256Of(e.Path);
        }
    }

    /// <summary>True when the file at the entry's path is no longer what the install left there.</summary>
    static bool Changed(JournalEntry e)
    {
        var info = new FileInfo(e.Path);
        if (e.Size == null) return e.Kind == "file" && !info.Exists; // old record: only "is it there"
        if (e.Size == -1) return info.Exists;
        if (!info.Exists || info.Length != e.Size) return true;
        if (info.LastWriteTimeUtc.Ticks == e.Ticks) return false; // untouched: skip hashing
        return e.Sha256 != null && Downloader.Sha256Of(e.Path) != e.Sha256;
    }

    /// <summary>Files of an installed pack that went missing or were replaced (e.g. by a Steam update).</summary>
    public List<string> Verify(string packId)
    {
        var record = Get(packId) ?? throw new InvalidOperationException("Сборка не установлена.");
        var gameDir = record.GameDir.TrimEnd('\\') + "\\";
        return record.Journal.Where(e => e.Kind is "file" or "backup").Where(Changed)
            .Select(e => e.Path.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase) ? e.Path[gameDir.Length..] : e.Path)
            .ToList();
    }

    /// <summary>
    /// Reinstalls a pack whose files were changed from outside. Whatever replaced our files (a game update)
    /// becomes the new "original", so rolling back does not bring back outdated game files.
    /// </summary>
    public async Task RepairAsync(string packId, IProgress<InstallProgress> progress, AskUser ask, CancellationToken cancel = default)
    {
        var record = Get(packId) ?? throw new InvalidOperationException("Сборка не установлена.");
        var backupDir = Path.Combine(RecordDir(packId), "backup");
        Directory.CreateDirectory(backupDir);
        foreach (var e in record.Journal.Where(e => e.Kind is "file" or "backup").ToList())
        {
            if (!File.Exists(e.Path) || !Changed(e)) continue;
            // Keep the new file as the original to restore.
            var backup = e.Backup ?? Path.Combine(backupDir, "r" + Guid.NewGuid().ToString("N")[..10] + ".bak");
            File.Copy(e.Path, backup, overwrite: true);
            e.Kind = "backup";
            e.Backup = backup;
        }
        Json.Write(RecordPath(packId), record);
        await InstallAsync(packId, record.GameDir, progress, ask, cancel);
    }

    static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
