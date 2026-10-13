using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModCrew;

static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException($"Empty JSON: {path}");

    public static T? TryRead<T>(string path) where T : class
    {
        try { return File.Exists(path) ? Read<T>(path) : null; }
        catch (Exception) { return null; }
    }

    /// <summary>Writes via a temp file so a crash never leaves half a file behind.</summary>
    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }

    public static T Parse<T>(JsonElement e) =>
        e.Deserialize<T>(Options) ?? throw new InvalidDataException("Empty parameters");
}
