namespace ModCrew;

public sealed class Settings
{
    /// <summary>Manual game folders by Steam app id (non-Steam copies, or when detection fails).</summary>
    public Dictionary<int, string> GamePaths { get; set; } = new();

    /// <summary>Download and install new versions from GitHub Releases on its own (otherwise only tell).</summary>
    public bool AutoUpdate { get; set; } = true;

    public Appearance Appearance { get; set; } = new();

    public static Settings Load() => Json.TryRead<Settings>(AppPaths.Settings) ?? new Settings();

    public void Save() => Json.Write(AppPaths.Settings, this);
}

public sealed class Appearance
{
    /// <summary>"dark", "light" or "system".</summary>
    public string Theme { get; set; } = "dark";
    /// <summary>Accent preset name, see ui/src/lib/theme.js.</summary>
    public string Accent { get; set; } = "violet";
    /// <summary>Interface zoom, 0.8–1.4.</summary>
    public double Scale { get; set; } = 1.0;
    public bool Animations { get; set; } = true;

    public void Clamp()
    {
        if (Theme is not ("dark" or "light" or "system")) Theme = "dark";
        Scale = Math.Clamp(double.IsFinite(Scale) ? Scale : 1.0, 0.8, 1.4);
    }

    /// <summary>Whether the dark theme is in effect (for "system": Windows app mode).</summary>
    public bool IsDark() => Theme switch
    {
        "light" => false,
        "system" => Microsoft.Win32.Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) is not 1,
        _ => true,
    };
}
