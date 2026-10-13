using ModCrew.Install;
using ModCrew.Steam;
using Xunit;

namespace ModCrew.Tests;

public sealed class ParserTests
{
    [Fact]
    public void Vdf_ParsesLibraryFolders_NewAndOldFormat()
    {
        var root = VdfNode.Parse("""
            "libraryfolders"
            {
                // comment
                "0" { "path" "C:\\Program Files (x86)\\Steam" "apps" { "228980" "1" } }
                "1" "D:\\SteamLibrary"
            }
            """);
        var folders = root.Node("libraryfolders")!;
        Assert.Equal(@"C:\Program Files (x86)\Steam", folders.Node("0")!.Str("path"));
        Assert.Equal(@"D:\SteamLibrary", folders.Str("1"));
    }

    [Theory]
    [InlineData("", "", "k", "v", "k=v\r\n")]
    [InlineData("a=1\r\n[S]\r\nx=2\r\n", "", "b", "3", "a=1\r\nb=3\r\n[S]\r\nx=2\r\n")]
    [InlineData("[S]\nx=2\n\n[T]\ny=1\n", "s", "X", "9", "[S]\nx=9\n\n[T]\ny=1\n")]
    [InlineData("[S]\nx=2\n\n[T]\ny=1\n", "S", "z", "0", "[S]\nx=2\nz=0\n\n[T]\ny=1\n")]
    [InlineData("[S]\nx=2\n", "New", "k", "v", "[S]\nx=2\n\n[New]\nk=v\n")]
    [InlineData("; x=1\n[S]\n", "", "x", "2", "; x=1\nx=2\n[S]\n")]
    public void Ini_Set(string input, string section, string key, string value, string expected) =>
        Assert.Equal(expected, Ini.Set(input, section, key, value));
}
