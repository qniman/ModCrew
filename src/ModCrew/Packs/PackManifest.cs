using System.Text.RegularExpressions;

namespace ModCrew.Packs;

/// <summary>pack.json — a modpack for one game: a list of mods, each with install steps.</summary>
public sealed class PackManifest
{
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string? Author { get; set; }
    public string? Description { get; set; }
    /// <summary>Image path relative to the pack folder.</summary>
    public string? Cover { get; set; }
    public GameRef Game { get; set; } = new();
    public List<ModEntry> Mods { get; set; } = new();

    static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9._-]{0,63}$");

    public void Validate()
    {
        if (Format > CurrentFormat)
            throw new InvalidDataException($"Сборка сделана в более новой версии ModCrew (формат {Format}). Обнови приложение.");
        if (!IdPattern.IsMatch(Id))
            throw new InvalidDataException($"Неверный id сборки «{Id}»: только a-z, 0-9, точка, дефис, подчёркивание.");
        if (!Name.Any(char.IsLetterOrDigit)) throw new InvalidDataException("У сборки нет названия.");
        if (Game.SteamAppId <= 0) throw new InvalidDataException("В сборке не указана игра (game.steamAppId).");
        if (Cover != null && (Path.IsPathRooted(Cover) || Cover.Contains("..")))
            throw new InvalidDataException("cover должен быть путём внутри сборки.");
        for (int i = 0; i < Mods.Count; i++)
        {
            var mod = Mods[i];
            if (string.IsNullOrWhiteSpace(mod.Name)) throw new InvalidDataException($"У мода №{i + 1} нет названия.");
            foreach (var step in mod.Steps)
                if (!Step.Actions.Contains(step.Action))
                    throw new InvalidDataException($"Мод «{mod.Name}»: неизвестное действие «{step.Action}».");
        }
    }
}

public sealed class GameRef
{
    public int SteamAppId { get; set; }
    public string? Name { get; set; }
}

public sealed class ModEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Description { get; set; }
    /// <summary>Mod homepage (Nexus, GitHub…), shown as a link.</summary>
    public string? Url { get; set; }
    public List<Step> Steps { get; set; } = new();
}

/// <summary>
/// One install action. Flat on purpose: easy to write by hand and to edit in the UI.
/// Paths may use {game} {pack} {work} {documents} {appdata} {localappdata} {locallow};
/// a relative path means {work}.
/// </summary>
public sealed class Step
{
    public static readonly HashSet<string> Actions = new()
    {
        "download",    // url, sha256?, saveAs?
        "extract",     // from (zip), to?
        "copy",        // from (file or folder), to, overwrite? (default true)
        "delete",      // path
        "replaceText", // file, find, replace
        "setIni",      // file, section?, key, value
        "run",         // path, args? — asks the player first, cannot be rolled back
    };

    public string Action { get; set; } = "";
    public string? Title { get; set; }

    public string? Url { get; set; }
    public string? Sha256 { get; set; }
    public string? SaveAs { get; set; }

    public string? From { get; set; }
    public string? To { get; set; }
    public bool? Overwrite { get; set; }

    public string? Path { get; set; }
    public string? Args { get; set; }

    public string? File { get; set; }
    public string? Find { get; set; }
    public string? Replace { get; set; }
    public string? Section { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
}
