using System.IO.Compression;
using ModCrew.Packs;
using Xunit;

namespace ModCrew.Tests;

public sealed class AuthoringTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "modcrew-auth-" + Guid.NewGuid().ToString("N")[..8]);
    readonly string game;
    readonly PackLibrary library;

    public AuthoringTests()
    {
        game = Path.Combine(root, "game");
        Directory.CreateDirectory(Path.Combine(game, "Mods"));
        Directory.CreateDirectory(Path.Combine(game, "BepInEx"));
        library = new PackLibrary(Path.Combine(root, "packs"));
    }

    public void Dispose()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    string Zip(params string[] entries)
    {
        var path = Path.Combine(root, Guid.NewGuid().ToString("N")[..6] + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var e in entries)
        {
            var entry = zip.CreateEntry(e);
            if (!e.EndsWith('/')) using (var w = new StreamWriter(entry.Open())) w.Write("x");
        }
        return path;
    }

    [Theory]
    [InlineData("MSC с друзьями", "msc-s-druzyami")]
    [InlineData("  Valheim — удобства!! ", "valheim-udobstva")]
    [InlineData("???", "pack")]
    public void Slug(string name, string expected) => Assert.Equal(expected, Authoring.Slug(name));

    [Fact]
    public void Create_MakesUniqueIds()
    {
        Assert.Equal("my-pack", library.Create("My pack", 1, null, null).Id);
        Assert.Equal("my-pack-2", library.Create("My pack", 1, null, null).Id);
    }

    [Fact]
    public void Inspect_ArchiveLaidOutLikeGame_GoesToGameRoot()
    {
        var info = Authoring.Inspect(Zip("BepInEx/plugins/a.dll", "readme.txt"), game);
        Assert.Equal("zip", info.Kind);
        Assert.Equal(("", "{game}"), (info.SuggestFrom, info.SuggestTo));
        Assert.Equal(new[] { "BepInEx", "BepInEx/plugins" }, info.Dirs);
        Assert.Equal(2, info.FileCount);
    }

    [Fact]
    public void Inspect_WrapperFolder_IsSkipped()
    {
        var info = Authoring.Inspect(Zip("CoolMod-1.2/Mods/cool.dll"), game);
        Assert.Equal(("CoolMod-1.2", "{game}"), (info.SuggestFrom, info.SuggestTo));
    }

    [Fact]
    public void Inspect_UnknownLayout_GoesToMods_AndDllToo()
    {
        Assert.Equal("{game}/Mods", Authoring.Inspect(Zip("cool.dll"), game).SuggestTo);
        var dll = Path.Combine(root, "x.dll");
        File.WriteAllText(dll, "MZ");
        var info = Authoring.Inspect(dll, game);
        Assert.Equal("file", info.Kind);
        Assert.Equal("{game}/Mods", info.SuggestTo);
    }

    [Fact]
    public void Check_FindsMistakes()
    {
        var pack = library.Create("Check me", 1, null, null);
        var src = Path.Combine(root, "mod.zip");
        File.Copy(Zip("a.dll"), src);
        var used = library.AddFile(pack.Id, src);       // files/mod.zip
        library.AddFile(pack.Id, src);                  // files/mod-2.zip, unused
        pack.Mods.Add(new ModEntry
        {
            Name = "M",
            Steps =
            {
                new Step { Action = "extract", From = "{pack}/" + used, To = "{game}/Mods" },
                new Step { Action = "copy", From = "{pack}/files/missing.dll", To = "C:/Games/x" },
                new Step { Action = "download", Url = "http://example.com/a.zip" },
                new Step { Action = "copy", From = "a", To = "{gmae}/x" },
            },
        });

        var problems = Authoring.Check(library, pack.Id, pack);
        string[] texts = problems.Select(p => $"{p.Level}:{p.Step}:{p.Text}").ToArray();
        Assert.DoesNotContain(problems, p => p.Step == 0);
        Assert.Contains(problems, p => p.Step == 1 && p.Text.Contains("missing.dll"));
        Assert.Contains(problems, p => p.Step == 1 && p.Text.Contains("абсолютный"));
        Assert.Contains(problems, p => p.Step == 2 && p.Level == "error" && p.Text.Contains("https"));
        Assert.Contains(problems, p => p.Step == 2 && p.Level == "warning" && p.Text.Contains("sha256"));
        Assert.Contains(problems, p => p.Step == 3 && p.Text.Contains("{gmae}"));
        Assert.Contains(problems, p => p.File == "files/mod-2.zip");
        Assert.DoesNotContain(problems, p => p.File == "files/mod.zip");
    }

    [Fact]
    public void FilePath_RefusesEscapes()
    {
        var pack = library.Create("Esc", 1, null, null);
        Assert.Throws<UnauthorizedAccessException>(() => library.FilePath(pack.Id, "../other/pack.json"));
        Assert.Throws<UnauthorizedAccessException>(() => library.FilePath(pack.Id, "pack.json"));
    }
}
