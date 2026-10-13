using System.IO.Compression;

namespace ModCrew.Packs;

/// <summary>
/// Packs the player has added. A pack is a folder with pack.json (+ cover, + files/ for
/// bundled mods). It travels between friends as a .modcrew file = that folder zipped.
/// </summary>
public sealed class PackLibrary
{
    public const string ManifestName = "pack.json";
    public const string Extension = ".modcrew";

    readonly string root;

    public PackLibrary(string root)
    {
        this.root = root;
        Directory.CreateDirectory(root);
    }

    public string DirOf(string id) => Path.Combine(root, id);

    public List<PackManifest> List()
    {
        var result = new List<PackManifest>();
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            if (Path.GetFileName(dir).StartsWith('.')) continue;
            var manifest = Json.TryRead<PackManifest>(Path.Combine(dir, ManifestName));
            if (manifest != null) result.Add(manifest);
        }
        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public PackManifest Get(string id) =>
        Json.TryRead<PackManifest>(Path.Combine(DirOf(id), ManifestName))
        ?? throw new InvalidOperationException($"Сборка «{id}» не найдена.");

    /// <summary>Adds a .modcrew file or a folder with pack.json; replaces an older copy with the same id.</summary>
    public PackManifest Import(string source)
    {
        var staging = Path.Combine(root, ".import-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (Directory.Exists(source))
            {
                if (!File.Exists(Path.Combine(source, ManifestName)))
                    throw new InvalidDataException("В папке нет pack.json.");
                CopyDir(source, staging);
            }
            else
            {
                // ExtractToDirectory refuses entries that point outside the target folder.
                ZipFile.ExtractToDirectory(source, staging);
            }

            var manifestPath = Path.Combine(staging, ManifestName);
            if (!File.Exists(manifestPath))
                throw new InvalidDataException("Это не сборка ModCrew: внутри нет pack.json.");
            var manifest = Json.Read<PackManifest>(manifestPath);
            manifest.Validate();

            var target = DirOf(manifest.Id);
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    /// <summary>Zips a pack into a .modcrew file to send to friends.</summary>
    public void Export(string id, string destination)
    {
        Get(id); // throws if missing
        if (File.Exists(destination)) File.Delete(destination);
        ZipFile.CreateFromDirectory(DirOf(id), destination, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    public void Remove(string id)
    {
        var dir = DirOf(id);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    // ---------------------------------------------------------------- authoring

    public const string FilesDir = "files";

    public bool Exists(string id) => File.Exists(Path.Combine(DirOf(id), ManifestName));

    /// <summary>Creates an empty pack with an id derived from the name; returns it.</summary>
    public PackManifest Create(string name, int steamAppId, string? gameName, string? author)
    {
        var baseId = Authoring.Slug(name);
        var id = baseId;
        for (int n = 2; Directory.Exists(DirOf(id)); n++) id = $"{baseId}-{n}";
        var manifest = new PackManifest
        {
            Id = id, Name = name.Trim(), Version = "1.0.0", Author = author,
            Game = new GameRef { SteamAppId = steamAppId, Name = gameName },
            Saves = Saves.SaveBackups.KnownSaves(steamAppId),
        };
        manifest.Validate();
        Directory.CreateDirectory(Path.Combine(DirOf(id), FilesDir));
        Json.Write(Path.Combine(DirOf(id), ManifestName), manifest);
        return manifest;
    }

    public void Save(string id, PackManifest manifest)
    {
        if (manifest.Id != id) throw new InvalidDataException("id сборки менять нельзя.");
        if (!Exists(id)) throw new InvalidOperationException($"Сборка «{id}» не найдена.");
        manifest.Validate();
        Json.Write(Path.Combine(DirOf(id), ManifestName), manifest);
    }

    /// <summary>Copies a file into the pack's files/ folder (renaming on clash); returns "files/name".</summary>
    public string AddFile(string id, string source)
    {
        var dir = Path.Combine(DirOf(id), FilesDir);
        Directory.CreateDirectory(dir);
        var name = Path.GetFileName(source);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (int n = 2; File.Exists(Path.Combine(dir, name)); n++) name = $"{stem}-{n}{ext}";
        File.Copy(source, Path.Combine(dir, name));
        return $"{FilesDir}/{name}";
    }

    /// <summary>Files inside the pack folder (except pack.json), as relative paths with forward slashes.</summary>
    public List<string> Files(string id)
    {
        var dir = DirOf(id);
        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
            .Where(f => f != ManifestName)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Full path of a file inside the pack; refuses anything that escapes the pack folder.</summary>
    public string FilePath(string id, string relative)
    {
        var dir = Path.GetFullPath(DirOf(id));
        var full = Path.GetFullPath(Path.Combine(dir, relative.Replace('/', '\\')));
        if (!full.StartsWith(dir + "\\", StringComparison.OrdinalIgnoreCase) || full.EndsWith("\\" + ManifestName, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Файл вне сборки.");
        return full;
    }

    public void DeleteFile(string id, string relative)
    {
        var full = FilePath(id, relative);
        if (File.Exists(full)) File.Delete(full);
    }

    /// <summary>Replaces the cover with a copy of an image; returns the new relative path.</summary>
    public string SetCover(string id, string image)
    {
        var dir = DirOf(id);
        foreach (var old in Directory.EnumerateFiles(dir, "cover.*")) File.Delete(old);
        var name = "cover" + Path.GetExtension(image).ToLowerInvariant();
        File.Copy(image, Path.Combine(dir, name));
        return name;
    }

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
    }
}
