using System.IO.Compression;
using ModCrew.Install;
using ModCrew.Packs;
using Xunit;

namespace ModCrew.Tests;

public sealed class InstallTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "modcrew-test-" + Guid.NewGuid().ToString("N")[..8]);
    readonly string game;
    readonly PackLibrary library;
    readonly InstallService service;
    readonly List<string> asked = new();

    public InstallTests()
    {
        game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "Mods"));
        File.WriteAllText(Path.Combine(game, "game.ini"), "[Video]\r\nWidth=800\r\n");
        File.WriteAllText(Path.Combine(game, "Mods", "old.dll"), "old");
        library = new PackLibrary(Path.Combine(root, "data", "packs"));
        service = new InstallService(library, Path.Combine(root, "data", "installs"), Path.Combine(root, "data", "cache"),
            protectedDir: Path.Combine(root, "data"));
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    Task<bool> Ask(string title, string text, string ok)
    {
        asked.Add(title);
        return Task.FromResult(true);
    }

    static readonly IProgress<InstallProgress> NoProgress = new Progress<InstallProgress>();

    /// <summary>Builds a pack folder with files/mod.zip (A.dll, sub/B.txt) and imports it.</summary>
    string MakePack(string id, int appId, params Step[] steps)
    {
        var src = Path.Combine(root, "src-" + id);
        Directory.CreateDirectory(Path.Combine(src, "files"));
        var zipSrc = Path.Combine(root, "zipsrc-" + id);
        Directory.CreateDirectory(Path.Combine(zipSrc, "sub"));
        File.WriteAllText(Path.Combine(zipSrc, "A.dll"), "new A");
        File.WriteAllText(Path.Combine(zipSrc, "sub", "B.txt"), "B");
        ZipFile.CreateFromDirectory(zipSrc, Path.Combine(src, "files", "mod.zip"));
        File.WriteAllText(Path.Combine(src, "files", "old.dll"), "replaced");
        var manifest = new PackManifest
        {
            Id = id, Name = "Pack " + id, Game = new GameRef { SteamAppId = appId },
            Mods = { new ModEntry { Id = "m", Name = "Mod", Steps = steps.ToList() } },
        };
        Json.Write(Path.Combine(src, PackLibrary.ManifestName), manifest);
        return library.Import(src).Id;
    }

    string Snapshot() => string.Join("\n", Directory.EnumerateFileSystemEntries(game, "*", SearchOption.AllDirectories)
        .OrderBy(p => p).Select(p => Path.GetRelativePath(game, p) + (File.Exists(p) ? "=" + File.ReadAllText(p) : "/")));

    [Fact]
    public async Task InstallThenUninstall_RestoresGameFolderExactly()
    {
        var before = Snapshot();
        var id = MakePack("p1", 1,
            new Step { Action = "extract", From = "{pack}/files/mod.zip", To = "{game}/Mods/NewMod" },
            new Step { Action = "copy", From = "{pack}/files/old.dll", To = "{game}/Mods/" },
            new Step { Action = "setIni", File = "{game}/game.ini", Section = "Video", Key = "Width", Value = "1920" },
            new Step { Action = "setIni", File = "{game}/game.ini", Section = "Audio", Key = "Volume", Value = "5" },
            new Step { Action = "delete", Path = "{game}/Mods/NewMod/sub/B.txt" });

        await service.InstallAsync(id, game, NoProgress, Ask);

        Assert.Equal("new A", File.ReadAllText(Path.Combine(game, "Mods", "NewMod", "A.dll")));
        Assert.False(File.Exists(Path.Combine(game, "Mods", "NewMod", "sub", "B.txt")));
        Assert.Equal("replaced", File.ReadAllText(Path.Combine(game, "Mods", "old.dll")));
        var ini = File.ReadAllText(Path.Combine(game, "game.ini"));
        Assert.Contains("Width=1920", ini);
        Assert.Contains("[Audio]\r\nVolume=5", ini);
        Assert.True(service.Get(id)!.Complete);

        service.Uninstall(id);
        Assert.Equal(before, Snapshot());
        Assert.Null(service.Get(id));
    }

    [Fact]
    public async Task FailedStep_RollsBackEverything()
    {
        var before = Snapshot();
        var id = MakePack("p2", 1,
            new Step { Action = "extract", From = "{pack}/files/mod.zip", To = "{game}/Mods/NewMod" },
            new Step { Action = "replaceText", File = "{game}/game.ini", Find = "NOT THERE", Replace = "x" });

        var e = await Assert.ThrowsAsync<InstallException>(() => service.InstallAsync(id, game, NoProgress, Ask));
        Assert.Contains("шаг 2", e.Message);
        Assert.Equal(before, Snapshot());
        Assert.Null(service.Get(id));
    }

    [Theory]
    [InlineData("{documents}/../outside.txt")]
    [InlineData("C:/Windows/evil.txt")]
    [InlineData("{documents}")]
    [InlineData("{localappdata}/../evil.txt")]
    public async Task WritesOutsideAllowedFolders_AreRefused(string target)
    {
        var id = MakePack("p3", 1, new Step { Action = "copy", From = "{pack}/files/old.dll", To = target });
        var e = await Assert.ThrowsAsync<InstallException>(() => service.InstallAsync(id, game, NoProgress, Ask));
        Assert.IsType<UnauthorizedAccessException>(e.InnerException);
    }

    [Fact]
    public async Task PackCannotTouchModCrewData()
    {
        var id = MakePack("p4", 1, new Step { Action = "delete", Path = "{pack}/pack.json" });
        var e = await Assert.ThrowsAsync<InstallException>(() => service.InstallAsync(id, game, NoProgress, Ask));
        Assert.IsType<UnauthorizedAccessException>(e.InnerException);
    }

    [Fact]
    public async Task Reinstall_ReplacesPreviousVersion_AndOtherPackForSameGameIsRemovedAfterAsking()
    {
        var before = Snapshot();
        var a = MakePack("pa", 7, new Step { Action = "copy", From = "{pack}/files/old.dll", To = "{game}/a.dll" });
        var b = MakePack("pb", 7, new Step { Action = "copy", From = "{pack}/files/old.dll", To = "{game}/b.dll" });

        await service.InstallAsync(a, game, NoProgress, Ask);
        await service.InstallAsync(a, game, NoProgress, Ask); // update in place, no question
        Assert.Empty(asked);

        await service.InstallAsync(b, game, NoProgress, Ask);
        Assert.Single(asked);
        Assert.False(File.Exists(Path.Combine(game, "a.dll")));
        Assert.True(File.Exists(Path.Combine(game, "b.dll")));
        Assert.Null(service.Get(a));

        service.Uninstall(b);
        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void Import_RejectsBadManifest()
    {
        var src = Path.Combine(root, "bad");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "pack.json"), """{ "id": "Bad Id!", "name": "x", "game": { "steamAppId": 1 } }""");
        Assert.Throws<InvalidDataException>(() => library.Import(src));
        Assert.Empty(library.List());
    }

    [Fact]
    public void ExportThenImport_RoundTrips()
    {
        var id = MakePack("rt", 1);
        var file = Path.Combine(root, "rt.modcrew");
        library.Export(id, file);
        library.Remove(id);
        Assert.Equal("rt", library.Import(file).Id);
        Assert.True(File.Exists(Path.Combine(library.DirOf("rt"), "files", "mod.zip")));
    }
}
