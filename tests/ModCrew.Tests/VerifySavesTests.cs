using ModCrew.Install;
using ModCrew.Packs;
using ModCrew.Saves;
using Xunit;

namespace ModCrew.Tests;

public sealed class VerifySavesTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "modcrew-vs-" + Guid.NewGuid().ToString("N")[..8]);
    readonly string game;
    readonly PackLibrary library;
    readonly InstallService service;
    static readonly IProgress<InstallProgress> NoProgress = new Progress<InstallProgress>();
    static Task<bool> Yes(string a, string b, string c) => Task.FromResult(true);

    public VerifySavesTests()
    {
        game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "Mods"));
        File.WriteAllText(Path.Combine(game, "game.dll"), "original v1");
        library = new PackLibrary(Path.Combine(root, "data", "packs"));
        service = new InstallService(library, Path.Combine(root, "data", "installs"), Path.Combine(root, "data", "cache"));
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    string MakePack()
    {
        var src = Path.Combine(root, "src");
        Directory.CreateDirectory(Path.Combine(src, "files"));
        File.WriteAllText(Path.Combine(src, "files", "mod.dll"), "mod");
        File.WriteAllText(Path.Combine(src, "files", "game.dll"), "patched");
        Json.Write(Path.Combine(src, PackLibrary.ManifestName), new PackManifest
        {
            Id = "p", Name = "Pack", Game = new GameRef { SteamAppId = 1 },
            Mods =
            {
                new ModEntry
                {
                    Name = "M",
                    Steps =
                    {
                        new Step { Action = "copy", From = "{pack}/files/mod.dll", To = "{game}/Mods/" },
                        new Step { Action = "copy", From = "{pack}/files/game.dll", To = "{game}/" },
                    },
                },
            },
        });
        return library.Import(src).Id;
    }

    [Fact]
    public async Task Verify_SeesMissingAndReplacedFiles()
    {
        var id = MakePack();
        await service.InstallAsync(id, game, NoProgress, Yes);
        Assert.Empty(service.Verify(id));

        File.Delete(Path.Combine(game, "Mods", "mod.dll"));
        File.WriteAllText(Path.Combine(game, "game.dll"), "steam update v2");
        Assert.Equal(new[] { @"Mods\mod.dll", "game.dll" }, service.Verify(id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Repair_KeepsGameUpdateAsNewOriginal()
    {
        var id = MakePack();
        await service.InstallAsync(id, game, NoProgress, Yes);

        // Steam updates the game: our patched file is replaced and our mod file is wiped.
        File.WriteAllText(Path.Combine(game, "game.dll"), "original v2");
        File.Delete(Path.Combine(game, "Mods", "mod.dll"));

        await service.RepairAsync(id, NoProgress, Yes);
        Assert.Empty(service.Verify(id));
        Assert.Equal("patched", File.ReadAllText(Path.Combine(game, "game.dll")));
        Assert.True(File.Exists(Path.Combine(game, "Mods", "mod.dll")));

        // Uninstalling now gives back the updated game file, not the old v1.
        service.Uninstall(id);
        Assert.Equal("original v2", File.ReadAllText(Path.Combine(game, "game.dll")));
        Assert.False(File.Exists(Path.Combine(game, "Mods", "mod.dll")));
    }

    [Fact]
    public void Saves_BackupRestoreAndPrune()
    {
        var saves = Path.Combine(root, "saves");
        Directory.CreateDirectory(Path.Combine(saves, "sub"));
        File.WriteAllText(Path.Combine(saves, "slot1.txt"), "day 10");
        File.WriteAllText(Path.Combine(saves, "sub", "x.txt"), "x");
        var backups = new SaveBackups(Path.Combine(root, "data", "saves"));

        var first = backups.Create(1, saves, "before install", auto: true)!;
        Assert.Equal(2, first.Files);

        File.WriteAllText(Path.Combine(saves, "slot1.txt"), "day 11, car on fire");
        File.WriteAllText(Path.Combine(saves, "new.txt"), "new");
        backups.Restore(1, first.Id, saves);

        Assert.Equal("day 10", File.ReadAllText(Path.Combine(saves, "slot1.txt")));
        Assert.False(File.Exists(Path.Combine(saves, "new.txt")));
        Assert.True(File.Exists(Path.Combine(saves, "sub", "x.txt")));
        // Restoring snapshotted the broken state first, so it can be undone.
        Assert.Equal(2, backups.List(1).Count);

        for (int i = 0; i < SaveBackups.KeepAuto + 5; i++) backups.Create(1, saves, "auto", auto: true);
        backups.Create(1, saves, "manual", auto: false);
        var list = backups.List(1);
        Assert.Equal(SaveBackups.KeepAuto, list.Count(b => b.Auto));
        Assert.Single(list, b => !b.Auto);
    }

    [Fact]
    public void Saves_NothingToBackUp_ReturnsNull()
    {
        var backups = new SaveBackups(Path.Combine(root, "data", "saves"));
        Assert.Null(backups.Create(1, Path.Combine(root, "nope"), "x", auto: true));
    }

    [Theory]
    [InlineData("{locallow}/Amistech/My Summer Car", true)]
    [InlineData("{game}/saves", true)]
    [InlineData("{game}", false)]
    [InlineData("{game}/", false)]
    [InlineData("{work}/x", false)]
    [InlineData("{pack}/x", false)]
    [InlineData("{documents}/../x", false)]
    [InlineData("C:/Saves", false)]
    public void Saves_TemplateMustBeASpecificSubfolder(string template, bool ok)
    {
        if (ok) SaveBackups.CheckTemplate(template);
        else Assert.Throws<InvalidDataException>(() => SaveBackups.CheckTemplate(template));
    }
}
