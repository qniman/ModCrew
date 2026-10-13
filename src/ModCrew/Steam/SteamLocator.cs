using Microsoft.Win32;

namespace ModCrew.Steam;

public sealed record SteamGame(int AppId, string Name, string InstallDir, string Library);

/// <summary>Finds Steam, its library folders and installed games (no Steam API needed).</summary>
public static class SteamLocator
{
    // Redistributables, runtimes and tools that show up as "apps" but are not games.
    static readonly HashSet<int> NotGames = new() { 228980, 1070560, 1391110, 1628350, 1493710, 2180100, 1826330 };

    public static string? SteamPath()
    {
        var path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                   ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrEmpty(path)) return null;
        path = Path.GetFullPath(path.Replace('/', '\\'));
        return Directory.Exists(path) ? path : null;
    }

    public static List<string> Libraries(string steamPath)
    {
        var result = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return result;
        try
        {
            var folders = VdfNode.Parse(File.ReadAllText(vdf)).Node("libraryfolders");
            if (folders == null) return result;
            foreach (var (_, value) in folders)
            {
                // New format: "0" { "path" "..." }, old format: "1" "D:\\SteamLibrary".
                var path = value is VdfNode n ? n.Str("path") : value as string;
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
                path = Path.GetFullPath(path);
                if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
            }
        }
        catch (FormatException) { }
        return result;
    }

    public static List<SteamGame> Games()
    {
        var steam = SteamPath();
        if (steam == null) return new();
        var games = new List<SteamGame>();
        foreach (var lib in Libraries(steam))
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                try
                {
                    var state = VdfNode.Parse(File.ReadAllText(acf)).Node("AppState");
                    if (state == null || !int.TryParse(state.Str("appid"), out var appId)) continue;
                    if (NotGames.Contains(appId)) continue;
                    var name = state.Str("name") ?? appId.ToString();
                    var installDir = state.Str("installdir");
                    if (string.IsNullOrEmpty(installDir)) continue;
                    var dir = Path.Combine(apps, "common", installDir);
                    if (!Directory.Exists(dir)) continue;
                    if (name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)) continue;
                    games.Add(new SteamGame(appId, name, dir, lib));
                }
                catch (Exception e) when (e is IOException or FormatException) { }
            }
        }
        return games.GroupBy(g => g.AppId).Select(g => g.First())
            .OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// A cached library image (library_600x900.jpg, library_hero.jpg, logo.png, header.jpg),
    /// as a path relative to appcache\librarycache, or null if Steam has not cached it.
    /// </summary>
    public static string? CachedImage(string steamPath, int appId, string file)
    {
        var cache = Path.Combine(steamPath, "appcache", "librarycache");
        var appDir = Path.Combine(cache, appId.ToString());
        if (Directory.Exists(appDir))
        {
            var direct = Path.Combine(appDir, file);
            if (File.Exists(direct)) return Path.GetRelativePath(cache, direct);
            var nested = Directory.EnumerateFiles(appDir, file, SearchOption.AllDirectories).FirstOrDefault();
            if (nested != null) return Path.GetRelativePath(cache, nested);
        }
        var flat = Path.Combine(cache, $"{appId}_{file}"); // pre-2024 Steam layout
        return File.Exists(flat) ? Path.GetRelativePath(cache, flat) : null;
    }
}
