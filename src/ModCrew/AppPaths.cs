namespace ModCrew;

/// <summary>Where ModCrew keeps its own data: %LOCALAPPDATA%\ModCrew.</summary>
static class AppPaths
{
    public static readonly string Data = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModCrew");

    /// <summary>Imported packs, one folder per pack id.</summary>
    public static string Packs => Path.Combine(Data, "packs");

    /// <summary>Install records + backups, one folder per pack id.</summary>
    public static string Installs => Path.Combine(Data, "installs");

    /// <summary>Downloads (by sha256) and temporary work folders.</summary>
    public static string Cache => Path.Combine(Data, "cache");

    public static string WebView => Path.Combine(Data, "webview");

    public static string Settings => Path.Combine(Data, "settings.json");
}
