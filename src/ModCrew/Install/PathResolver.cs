using System.Text.RegularExpressions;

namespace ModCrew.Install;

/// <summary>
/// Turns step paths like "{game}/Mods" into real paths and keeps every write inside folders
/// a mod may legitimately touch — a pack can never write to C:\Windows or the player's desktop.
/// </summary>
public sealed class PathResolver
{
    readonly Dictionary<string, string> vars = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> writeRoots = new();
    readonly List<string> readRoots = new();
    readonly string? protectedDir;

    public string Work => vars["work"];

    /// <param name="protectedDir">ModCrew's own data folder: off-limits except for {work} and {pack}.</param>
    public PathResolver(string gameDir, string packDir, string workDir, string? protectedDir = null)
    {
        this.protectedDir = protectedDir == null ? null : Full(protectedDir);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        vars["game"] = Full(gameDir);
        vars["pack"] = Full(packDir);
        vars["work"] = Full(workDir);
        vars["documents"] = Full(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        vars["appdata"] = Full(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        vars["localappdata"] = Full(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        vars["locallow"] = Full(Path.Combine(profile, "AppData", "LocalLow"));

        writeRoots.Add(vars["game"]);
        writeRoots.Add(vars["work"]);
        // Saves/configs of games live here. Only per-game subfolders, never the root itself.
        writeRoots.Add(vars["documents"]);
        writeRoots.Add(vars["appdata"]);
        writeRoots.Add(vars["localappdata"]);
        writeRoots.Add(vars["locallow"]);

        readRoots.AddRange(writeRoots);
        readRoots.Add(vars["pack"]);
    }

    static string Full(string p) => Path.GetFullPath(p).TrimEnd('\\');

    static readonly Regex VarPattern = new(@"\{([a-zA-Z]+)\}");

    public string Resolve(string template)
    {
        var s = VarPattern.Replace(template, m =>
            vars.TryGetValue(m.Groups[1].Value, out var v)
                ? v
                : throw new InvalidDataException($"Неизвестная переменная {m.Value} в пути «{template}»."));
        s = s.Replace('/', '\\');
        if (!Path.IsPathRooted(s)) s = Path.Combine(vars["work"], s);
        return Path.GetFullPath(s);
    }

    public bool IsInWork(string path) => IsUnder(path, vars["work"]);

    public string ForRead(string template) => Check(Resolve(template), readRoots, template, write: false);

    public string ForWrite(string template) => Check(Resolve(template), writeRoots, template, write: true);

    string Check(string path, List<string> roots, string template, bool write)
    {
        if (protectedDir != null && IsUnder(path, protectedDir) && !IsInWork(path)
            && (write || !IsUnder(path, vars["pack"])))
            throw new UnauthorizedAccessException($"Путь «{template}» ведёт в служебную папку ModCrew.");
        foreach (var root in roots)
        {
            if (!IsUnder(path, root)) continue;
            // {game}, {work} themselves are fine targets; the shared user folders are not.
            bool isRoot = path.Equals(root, StringComparison.OrdinalIgnoreCase);
            if (isRoot && write && root != vars["game"] && root != vars["work"]) break;
            return path;
        }
        throw new UnauthorizedAccessException(
            $"Путь «{template}» ведёт за пределы разрешённых папок ({path}).");
    }

    static bool IsUnder(string path, string root) =>
        path.Equals(root, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
}
