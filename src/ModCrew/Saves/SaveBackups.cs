using System.IO.Compression;
using ModCrew.Install;
using ModCrew.Packs;

namespace ModCrew.Saves;

public sealed class SaveBackup
{
    public string Id { get; set; } = "";
    public DateTime At { get; set; }
    public string Reason { get; set; } = "";
    /// <summary>Made automatically (before an install); only these are pruned.</summary>
    public bool Auto { get; set; }
    public long Size { get; set; }
    public int Files { get; set; }
}

/// <summary>
/// Zip snapshots of a game's save folder in %LOCALAPPDATA%\ModCrew\saves\&lt;appId&gt;\,
/// taken before installs/updates/repairs and on request; restoring first snapshots what is there now.
/// </summary>
public sealed class SaveBackups
{
    public const int KeepAuto = 15;

    /// <summary>Save folders of games we know, for packs that do not say.</summary>
    static readonly Dictionary<int, string> Known = new()
    {
        [516750] = "{locallow}/Amistech/My Summer Car",                       // My Summer Car
        [489830] = "{documents}/My Games/Skyrim Special Edition/Saves",       // Skyrim SE
        [1222670] = "{documents}/Electronic Arts/The Sims 4/saves",           // The Sims 4
    };

    public static string? KnownSaves(int appId) => Known.TryGetValue(appId, out var p) ? p : null;

    readonly string root;

    public SaveBackups(string root) => this.root = root;

    string GameDir(int appId) => Path.Combine(root, appId.ToString());

    /// <summary>The save folder for a pack (its own setting, else the known one), resolved; null if unknown.</summary>
    public static string? SavesDir(PackManifest pack, string? gameDir)
    {
        var template = pack.Saves ?? KnownSaves(pack.Game.SteamAppId);
        if (string.IsNullOrWhiteSpace(template)) return null;
        if (gameDir == null && template.Contains("{game}", StringComparison.OrdinalIgnoreCase)) return null;
        CheckTemplate(template);
        var paths = new PathResolver(gameDir ?? AppPaths.Data, AppPaths.Packs, Path.Combine(AppPaths.Cache, "no-work"),
            protectedDir: AppPaths.Data);
        var dir = paths.ForWrite(template); // refuses anything outside game/user folders and ModCrew's own data
        if (gameDir != null && Path.GetFullPath(gameDir).TrimEnd('\\').Equals(dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Папка сохранений не может быть всей папкой игры.");
        return dir;
    }

    static readonly string[] AllowedRoots = { "{game}/", "{documents}/", "{appdata}/", "{localappdata}/", "{locallow}/" };

    /// <summary>A save folder is restored by replacing it whole, so it must be a specific subfolder.</summary>
    public static void CheckTemplate(string template)
    {
        var t = template.Replace('\\', '/');
        if (!AllowedRoots.Any(r => t.StartsWith(r, StringComparison.OrdinalIgnoreCase) && t.Length > r.Length) || t.Contains(".."))
            throw new InvalidDataException(
                "Папка сохранений должна быть подпапкой {game}, {documents}, {appdata}, {localappdata} или {locallow}.");
    }

    public List<SaveBackup> List(int appId)
    {
        var dir = GameDir(appId);
        if (!Directory.Exists(dir)) return new();
        return Directory.EnumerateFiles(dir, "*.json")
            .Select(f => Json.TryRead<SaveBackup>(f))
            .Where(b => b != null && File.Exists(Path.Combine(dir, b.Id + ".zip")))
            .Select(b => b!)
            .OrderByDescending(b => b.At).ToList();
    }

    /// <summary>Snapshots the folder; null when there is nothing to save yet.</summary>
    public SaveBackup? Create(int appId, string savesDir, string reason, bool auto)
    {
        if (!Directory.Exists(savesDir) || !Directory.EnumerateFileSystemEntries(savesDir).Any()) return null;
        var dir = GameDir(appId);
        Directory.CreateDirectory(dir);
        var at = DateTime.Now;
        var id = at.ToString("yyyyMMdd-HHmmss");
        for (int n = 2; File.Exists(Path.Combine(dir, id + ".zip")); n++) id = $"{at:yyyyMMdd-HHmmss}-{n}";
        var zip = Path.Combine(dir, id + ".zip");
        ZipFile.CreateFromDirectory(savesDir, zip, CompressionLevel.Optimal, includeBaseDirectory: false);
        var backup = new SaveBackup
        {
            Id = id, At = at, Reason = reason, Auto = auto, Size = new FileInfo(zip).Length,
            Files = Directory.EnumerateFiles(savesDir, "*", SearchOption.AllDirectories).Count(),
        };
        Json.Write(Path.Combine(dir, id + ".json"), backup);
        Prune(appId);
        return backup;
    }

    void Prune(int appId)
    {
        foreach (var old in List(appId).Where(b => b.Auto).Skip(KeepAuto)) Delete(appId, old.Id);
    }

    public void Delete(int appId, string id)
    {
        if (id.Contains('/') || id.Contains('\\') || id.Contains("..")) throw new ArgumentException("Bad backup id");
        foreach (var ext in new[] { ".zip", ".json" })
        {
            var f = Path.Combine(GameDir(appId), id + ext);
            if (File.Exists(f)) File.Delete(f);
        }
    }

    /// <summary>Puts a snapshot back. What is in the folder now is snapshotted first, so this can be undone too.</summary>
    public void Restore(int appId, string id, string savesDir)
    {
        if (id.Contains('/') || id.Contains('\\') || id.Contains("..")) throw new ArgumentException("Bad backup id");
        var zip = Path.Combine(GameDir(appId), id + ".zip");
        if (!File.Exists(zip)) throw new FileNotFoundException("Бэкап не найден.");
        Create(appId, savesDir, "перед восстановлением другого бэкапа", auto: true);

        // Unpack next to the folder first: if that fails, the current saves stay untouched.
        var staging = savesDir.TrimEnd('\\') + ".modcrew-restore";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        ZipFile.ExtractToDirectory(zip, staging);
        if (Directory.Exists(savesDir)) Directory.Delete(savesDir, recursive: true);
        Directory.Move(staging, savesDir);
    }
}
