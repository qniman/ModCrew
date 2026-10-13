namespace ModCrew.Install;

public sealed class JournalEntry
{
    /// <summary>"file" = we created it, "dir" = we created it, "backup" = it existed, original saved in Backup.</summary>
    public string Kind { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Backup { get; set; }
}

/// <summary>
/// Records every change an install makes outside its work folder so it can be undone exactly:
/// files and folders we created get deleted, files we changed or deleted get their original back.
/// </summary>
public sealed class Journal
{
    public List<JournalEntry> Entries { get; set; } = new();

    readonly string backupDir;
    readonly HashSet<string> touched = new(StringComparer.OrdinalIgnoreCase);

    public Journal(string backupDir, List<JournalEntry>? entries = null)
    {
        this.backupDir = backupDir;
        if (entries == null) return;
        Entries = entries;
        foreach (var e in entries) touched.Add(e.Path);
    }

    /// <summary>Call before creating, overwriting or deleting a file.</summary>
    public void BeforeChange(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!touched.Add(path)) return; // first state is already recorded
        if (File.Exists(path))
        {
            Directory.CreateDirectory(backupDir);
            var backup = System.IO.Path.Combine(backupDir, Entries.Count.ToString("D6") + ".bak");
            File.Copy(path, backup, overwrite: true);
            Entries.Add(new JournalEntry { Kind = "backup", Path = path, Backup = backup });
        }
        else
        {
            EnsureDir(System.IO.Path.GetDirectoryName(path)!);
            Entries.Add(new JournalEntry { Kind = "file", Path = path });
        }
    }

    /// <summary>Creates a folder (and missing parents), recording the ones that did not exist.</summary>
    public void EnsureDir(string dir)
    {
        dir = System.IO.Path.GetFullPath(dir);
        var missing = new Stack<string>();
        for (var d = dir; d != null && !Directory.Exists(d); d = System.IO.Path.GetDirectoryName(d))
            missing.Push(d);
        while (missing.Count > 0)
        {
            var d = missing.Pop();
            Directory.CreateDirectory(d);
            if (touched.Add(d)) Entries.Add(new JournalEntry { Kind = "dir", Path = d });
        }
    }

    /// <summary>Undoes everything, newest first. Keeps going on errors and returns them.</summary>
    public List<string> Rollback()
    {
        var errors = new List<string>();
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            var e = Entries[i];
            try
            {
                switch (e.Kind)
                {
                    case "file":
                        if (File.Exists(e.Path)) File.Delete(e.Path);
                        break;
                    case "dir":
                        if (Directory.Exists(e.Path) && !Directory.EnumerateFileSystemEntries(e.Path).Any())
                            Directory.Delete(e.Path);
                        break;
                    case "backup":
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(e.Path)!);
                        File.Copy(e.Backup!, e.Path, overwrite: true);
                        break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{e.Path}: {ex.Message}");
            }
        }
        return errors;
    }
}
