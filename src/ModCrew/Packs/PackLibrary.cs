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

    static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, dir)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)));
    }
}
