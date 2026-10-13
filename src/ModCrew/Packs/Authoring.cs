using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ModCrew.Packs;

/// <summary>What is inside a mod file, for the "add mod" wizard.</summary>
public sealed class FileInfoResult
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    /// <summary>"zip" or "file".</summary>
    public string Kind { get; set; } = "file";
    /// <summary>Folders inside the archive ("A", "A/B"), sorted.</summary>
    public List<string> Dirs { get; set; } = new();
    public List<string> TopLevel { get; set; } = new();
    public int FileCount { get; set; }
    /// <summary>Suggested archive folder to take ("" = whole archive) and where to put it.</summary>
    public string SuggestFrom { get; set; } = "";
    public string SuggestTo { get; set; } = "{game}";
    public string? Sha256 { get; set; }
    public string? Url { get; set; }
    /// <summary>For files added to the pack: "{pack}/files/name".</summary>
    public string? PackPath { get; set; }
    /// <summary>Programs inside the archive (.exe/.msi), archive-relative.</summary>
    public List<string> Executables { get; set; } = new();
    /// <summary>"place" (put files into the game) or "run" (it is an installer).</summary>
    public string SuggestAction { get; set; } = "place";
    /// <summary>For "run" on an archive: which program inside it.</summary>
    public string? SuggestRun { get; set; }
}

public sealed record Problem(string Level, string Text, int? Mod = null, int? Step = null, string? File = null);

public static class Authoring
{
    static readonly Dictionary<char, string> Translit = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "c",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya",
    };

    /// <summary>"MSC с друзьями" → "msc-s-druzyami".</summary>
    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (Translit.TryGetValue(c, out var t)) sb.Append(t);
            else if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else sb.Append('-');
        }
        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        return slug.Length == 0 ? "pack" : slug;
    }

    public static FileInfoResult Inspect(string path, string? gameDir)
    {
        var info = new FileInfoResult { Name = Path.GetFileName(path), Size = new FileInfo(path).Length };
        if (!IsZip(path))
        {
            info.SuggestTo = gameDir != null && Directory.Exists(Path.Combine(gameDir, "Mods")) ? "{game}/Mods" : "{game}";
            if (IsProgram(info.Name)) info.SuggestAction = "run";
            return info;
        }

        info.Kind = "zip";
        var dirs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var top = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in ZipEntries(path))
        {
            var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            bool isDir = name.EndsWith('/');
            if (!isDir)
            {
                info.FileCount++;
                if (IsProgram(name)) info.Executables.Add(name);
            }
            top.Add(parts[0]);
            int dirDepth = isDir ? parts.Length : parts.Length - 1;
            for (int i = 1; i <= dirDepth; i++) dirs.Add(string.Join('/', parts.Take(i)));
        }
        info.Dirs = dirs.ToList();
        info.TopLevel = top.ToList();
        bool gameLike;
        (info.SuggestFrom, info.SuggestTo, gameLike) = Suggest(info, gameDir);

        // An archive that does not look like the game folder but carries a setup program is an installer.
        info.SuggestRun = info.Executables
            .OrderByDescending(e => InstallerName.IsMatch(Path.GetFileName(e)))
            .ThenBy(e => e.Count(c => c == '/')).ThenBy(e => e.Length)
            .FirstOrDefault();
        if (!gameLike && info.SuggestRun != null &&
            (InstallerName.IsMatch(Path.GetFileName(info.SuggestRun)) || info.FileCount <= 3))
            info.SuggestAction = "run";
        return info;
    }

    static readonly Regex InstallerName = new("setup|install", RegexOptions.IgnoreCase);

    public static bool IsProgram(string name) =>
        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);

    /// <summary>Entry names with forward slashes; folders end with "/".</summary>
    public static IEnumerable<string> ZipEntries(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (name.Length > 0) yield return name;
        }
    }

    public static bool IsZip(string path)
    {
        using var f = File.OpenRead(path);
        Span<byte> magic = stackalloc byte[4];
        return f.Read(magic) == 4 && magic[0] == 'P' && magic[1] == 'K' && magic[2] == 3 && magic[3] == 4;
    }

    /// <summary>
    /// Guesses how the archive maps onto the game folder: if its top-level names (or those of its single
    /// wrapper folder) already exist in the game, it is laid out like the game — unpack into {game}.
    /// </summary>
    static (string from, string to, bool gameLike) Suggest(FileInfoResult info, string? gameDir)
    {
        if (gameDir == null || !Directory.Exists(gameDir)) return ("", "{game}", false);
        var existing = new HashSet<string>(
            Directory.EnumerateFileSystemEntries(gameDir).Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);

        if (info.TopLevel.Any(existing.Contains)) return ("", "{game}", true);

        var topDirs = info.TopLevel.Where(t => info.Dirs.Contains(t)).ToList();
        if (info.TopLevel.Count == 1 && topDirs.Count == 1)
        {
            var wrapper = topDirs[0];
            var inner = info.Dirs.Where(d => d.StartsWith(wrapper + "/") && d.Count(c => c == '/') == 1)
                .Select(d => d[(wrapper.Length + 1)..]);
            if (inner.Any(existing.Contains)) return (wrapper, "{game}", true);
        }
        if (existing.Contains("Mods")) return ("", "{game}/Mods", false);
        return ("", "{game}", false);
    }

    /// <summary>
    /// Files that the steps before (mod, step) leave in the temporary folder — downloads and what was unpacked
    /// from archives we can see (files in the pack, or downloads already in the cache). Lets the editor offer
    /// "BeamMP_Installer.exe from the previous step" instead of making the author guess the path.
    /// </summary>
    public static List<string> StepOutputs(PackLibrary library, string id, PackManifest pack, int mod, int step, string downloadCache) =>
        StepOutputs(library, id, pack, mod, step, downloadCache, out _);

    /// <param name="complete">False when some archive's contents are unknown (download not in the cache yet).</param>
    public static List<string> StepOutputs(PackLibrary library, string id, PackManifest pack, int mod, int step, string downloadCache, out bool complete)
    {
        complete = true;
        var outputs = new List<string>();
        var archives = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // work path -> local file
        static string Norm(string p)
        {
            var s = p.Replace('\\', '/').Trim('/');
            return s.StartsWith("{work}/", StringComparison.OrdinalIgnoreCase) ? s[7..] : s;
        }

        for (int m = 0; m <= mod && m < pack.Mods.Count; m++)
        {
            var steps = pack.Mods[m].Steps;
            int last = m == mod ? Math.Min(step, steps.Count) : steps.Count;
            for (int s = 0; s < last; s++)
            {
                var st = steps[s];
                if (st.Action == "download" && !string.IsNullOrEmpty(st.Url))
                {
                    var name = Norm(st.SaveAs ?? Install.Downloader.FileNameFromUrl(st.Url));
                    outputs.Add(name);
                    var cached = string.IsNullOrEmpty(st.Sha256) ? null : Path.Combine(downloadCache, st.Sha256.Trim().ToLowerInvariant());
                    if (cached != null && File.Exists(cached)) archives[name] = cached;
                }
                else if (st.Action == "extract" && !string.IsNullOrEmpty(st.From))
                {
                    var to = Norm(st.To ?? Path.GetFileNameWithoutExtension(st.From));
                    if (to.StartsWith('{')) continue; // unpacked into the game, not into {work}
                    string? archive = null;
                    var from = st.From.Replace('\\', '/');
                    if (from.StartsWith("{pack}/", StringComparison.OrdinalIgnoreCase))
                    {
                        try { archive = library.FilePath(id, from[7..]); } catch (UnauthorizedAccessException) { }
                    }
                    else archives.TryGetValue(Norm(from), out archive);
                    if (archive == null || !File.Exists(archive) || !IsZip(archive))
                    {
                        complete = false;
                        outputs.Add(to);
                        continue;
                    }
                    foreach (var entry in ZipEntries(archive).Where(e => !e.EndsWith('/')).Take(500))
                        outputs.Add($"{to}/{entry}");
                }
            }
        }
        return outputs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    static readonly string[] KnownVars = { "game", "pack", "work", "documents", "appdata", "localappdata", "locallow" };
    static readonly Regex VarPattern = new(@"\{([a-zA-Z]+)\}");

    /// <summary>Finds mistakes before the pack is shared. Errors block nothing but will fail the install.</summary>
    public static List<Problem> Check(PackLibrary library, string id, PackManifest pack, string? downloadCache = null)
    {
        var problems = new List<Problem>();
        try { pack.Validate(); }
        catch (InvalidDataException e) { problems.Add(new Problem("error", e.Message)); }

        if (pack.Mods.Count == 0) problems.Add(new Problem("warning", "В сборке нет ни одного мода."));
        if (pack.Cover != null && !File.Exists(Path.Combine(library.DirOf(id), pack.Cover)))
            problems.Add(new Problem("error", $"Нет файла обложки {pack.Cover}."));

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (pack.Cover != null) used.Add(pack.Cover.Replace('\\', '/'));

        for (int m = 0; m < pack.Mods.Count; m++)
        {
            var mod = pack.Mods[m];
            if (string.IsNullOrWhiteSpace(mod.Name)) problems.Add(new Problem("error", "У мода нет названия.", m));
            if (mod.Steps.Count == 0) problems.Add(new Problem("warning", "У мода нет шагов — он ничего не установит.", m));
            for (int s = 0; s < mod.Steps.Count; s++)
            {
                var step = mod.Steps[s];
                void Err(string text) => problems.Add(new Problem("error", text, m, s));
                void Warn(string text) => problems.Add(new Problem("warning", text, m, s));

                foreach (var (field, value) in Required(step))
                    if (string.IsNullOrWhiteSpace(value)) Err($"Не заполнено поле «{field}».");

                foreach (var (field, value, write) in PathsOf(step))
                {
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    foreach (Match v in VarPattern.Matches(value))
                        if (!KnownVars.Contains(v.Groups[1].Value.ToLowerInvariant()))
                            Err($"Неизвестная переменная {v.Value} в поле «{field}».");
                    if (Path.IsPathRooted(value.Replace('/', '\\')))
                        Err($"Поле «{field}»: абсолютный путь не сработает у друзей — используй {{game}} и другие переменные.");
                    if (value.Contains(".."))
                        Err($"Поле «{field}»: «..» в путях запрещено.");
                    var lower = value.Replace('\\', '/').ToLowerInvariant();
                    if (write && lower.StartsWith("{pack}"))
                        Err($"Поле «{field}»: в папку сборки писать нельзя.");
                    if (lower.StartsWith("{pack}/"))
                    {
                        var rel = value.Replace('\\', '/')["{pack}/".Length..];
                        used.Add(rel.TrimEnd('/'));
                        var full = Path.Combine(library.DirOf(id), rel.Replace('/', '\\'));
                        if (!File.Exists(full) && !Directory.Exists(full)) Err($"В сборке нет файла {rel}.");
                    }
                    // A file read from the temporary folder must come from an earlier step.
                    bool fromWork = !write && (!lower.StartsWith('{') || lower.StartsWith("{work}/"));
                    if (fromWork && downloadCache != null)
                    {
                        var outputs = StepOutputs(library, id, pack, m, s, downloadCache, out bool complete);
                        var want = value.Replace('\\', '/').Trim('/');
                        if (want.StartsWith("{work}/", StringComparison.OrdinalIgnoreCase)) want = want[7..];
                        bool found = outputs.Any(o => o.Equals(want, StringComparison.OrdinalIgnoreCase)
                                                      || o.StartsWith(want + "/", StringComparison.OrdinalIgnoreCase)
                                                      || want.StartsWith(o + "/", StringComparison.OrdinalIgnoreCase) && !complete);
                        if (!found && (complete || outputs.Count == 0))
                            Err($"Поле «{field}»: «{want}» ищется во временной папке, но ни один шаг выше его не создаёт. Выбери файл через кнопку справа.");
                    }
                }

                if (step.Action == "download")
                {
                    if (!string.IsNullOrEmpty(step.Url) && !step.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        Err("Ссылка должна начинаться с https://");
                    if (string.IsNullOrWhiteSpace(step.Sha256))
                        Warn("Нет sha256: если файл по ссылке поменяется, у друзей установится другая версия.");
                }
                if (step.Action == "run") Warn("Запуск программы нельзя откатить — друзья увидят предупреждение.");
            }
        }

        // Files in files/ nobody refers to only make the .modcrew heavier.
        foreach (var file in library.Files(id))
        {
            if (!file.StartsWith(PackLibrary.FilesDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
            bool isUsed = used.Contains(file) || used.Any(u => file.StartsWith(u + "/", StringComparison.OrdinalIgnoreCase));
            if (!isUsed) problems.Add(new Problem("warning", $"Файл {file} не используется ни одним шагом.", File: file));
        }
        return problems;
    }

    static IEnumerable<(string, string?)> Required(Step s) => s.Action switch
    {
        "download" => new[] { ("ссылка", s.Url) },
        "extract" => new[] { ("архив", s.From) },
        "copy" => new[] { ("что", s.From), ("куда", s.To) },
        "delete" => new[] { ("что удалить", s.Path) },
        "replaceText" => new[] { ("файл", s.File), ("найти", s.Find) },
        "setIni" => new[] { ("файл", s.File), ("ключ", s.Key) },
        "run" => new[] { ("программа", s.Path) },
        _ => Array.Empty<(string, string?)>(),
    };

    static IEnumerable<(string field, string? value, bool write)> PathsOf(Step s) => s.Action switch
    {
        "download" => new[] { ("имя файла", s.SaveAs, true) },
        "extract" => new[] { ("архив", s.From, false), ("куда", s.To, true) },
        "copy" => new[] { ("что", s.From, false), ("куда", s.To, true) },
        "delete" => new[] { ("что удалить", s.Path, true) },
        "replaceText" or "setIni" => new[] { ("файл", s.File, true) },
        "run" => new[] { ("программа", s.Path, false) },
        _ => Array.Empty<(string, string?, bool)>(),
    };
}
