namespace ModCrew;

public sealed class Settings
{
    /// <summary>Manual game folders by Steam app id (non-Steam copies, or when detection fails).</summary>
    public Dictionary<int, string> GamePaths { get; set; } = new();

    public static Settings Load() => Json.TryRead<Settings>(AppPaths.Settings) ?? new Settings();

    public void Save() => Json.Write(AppPaths.Settings, this);
}
