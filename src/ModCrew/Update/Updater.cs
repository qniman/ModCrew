using System.Reflection;
using System.Text.Json;
using ModCrew.Install;

namespace ModCrew.Update;

public sealed record UpdateInfo(string Version, string? Notes, string ExeUrl, long Size, string? Sha256, string PageUrl);

/// <summary>
/// Updates ModCrew.exe from GitHub Releases. A release carries ModCrew.exe (+ ModCrew-Setup.exe for new users).
/// The running exe cannot be overwritten but can be renamed, so the update is: download next to it,
/// rename the running one to .old, put the new one in place. It takes effect on the next start.
/// </summary>
public static class Updater
{
    public const string Repo = "qniman/ModCrew";
    public static string ReleasesPage => $"https://github.com/{Repo}/releases";
    public const string ExeAsset = "ModCrew.exe";
    public const string SetupAsset = "ModCrew-Setup.exe";

    public static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    public static Version Current { get; } = ParseVersion(
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0") ?? new Version(0, 0, 0);

    public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Math.Max(Current.Build, 0)}";

    /// <summary>Built from source (bin\Debug, bin\Release): never replaced by a release download.</summary>
    public static bool IsDevBuild =>
        ExePath.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
        ExePath.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);

    static Version? ParseVersion(string text)
    {
        var core = text.TrimStart('v', 'V').Split('+', '-')[0];
        return Version.TryParse(core, out var v) ? v : null;
    }

    /// <summary>The newest release if it is newer than this exe; null if up to date or there are no releases yet.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Downloader.Http.SendAsync(request, cancel);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // no releases published yet
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GitHub ответил {(int)response.StatusCode} {response.ReasonPhrase}");

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = ParseVersion(tag);
        if (version == null || version <= Current) return null;

        JsonElement? exe = null, sums = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name == ExeAsset) exe = asset;
            else if (name == ExeAsset + ".sha256") sums = asset;
        }
        if (exe == null) return null; // release without the app binary (e.g. still uploading)

        // GitHub publishes "digest": "sha256:…" for assets; older releases have a .sha256 file next to the exe.
        string? sha = null;
        if (exe.Value.TryGetProperty("digest", out var digest) && digest.GetString() is { } d && d.StartsWith("sha256:"))
            sha = d["sha256:".Length..].ToLowerInvariant();
        else if (sums != null)
        {
            var text = await Downloader.Http.GetStringAsync(sums.Value.GetProperty("browser_download_url").GetString(), cancel);
            sha = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        }

        return new UpdateInfo(
            $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}",
            root.TryGetProperty("body", out var body) ? body.GetString() : null,
            exe.Value.GetProperty("browser_download_url").GetString()!,
            exe.Value.GetProperty("size").GetInt64(),
            sha,
            root.GetProperty("html_url").GetString() ?? ReleasesPage);
    }

    /// <summary>Downloads the new exe and swaps it in. The running process keeps working; restart to use it.</summary>
    public static async Task ApplyAsync(UpdateInfo info, Action<long, long?>? progress, CancellationToken cancel = default)
    {
        if (IsDevBuild) throw new InvalidOperationException("Это сборка из исходников — она не обновляется из релизов.");
        var exe = ExePath;
        var fresh = exe + ".new";
        var old = exe + ".old";
        try
        {
            var sha = await Downloader.DownloadAsync(info.ExeUrl, fresh, progress, cancel);
            if (info.Sha256 != null && sha != info.Sha256)
                throw new InvalidDataException("Скачанное обновление повреждено (не совпала контрольная сумма). Попробуй ещё раз.");
            TryDelete(old);
            File.Move(exe, old);
            try
            {
                File.Move(fresh, exe);
            }
            catch
            {
                File.Move(old, exe); // put the working exe back
                throw;
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException(
                $"Нет прав менять файлы в папке {Path.GetDirectoryName(exe)}. Скачай установщик со страницы релизов.");
        }
        finally
        {
            TryDelete(fresh);
        }
        Setup.RecordVersion(info.Version);
    }

    /// <summary>Removes what the previous update left behind (the old exe can only go once it stopped running).</summary>
    public static void CleanUp()
    {
        TryDelete(ExePath + ".old");
        TryDelete(ExePath + ".new");
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
