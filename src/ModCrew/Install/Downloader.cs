using System.Diagnostics;
using System.Security.Cryptography;

namespace ModCrew.Install;

/// <summary>HTTPS downloads with progress, sha256 and a cache keyed by hash.</summary>
public static class Downloader
{
    static readonly HttpClient Http = CreateHttp();

    static HttpClient CreateHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ModCrew/0.1");
        return http;
    }

    public static void RequireHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Скачивать можно только по ссылке https://");
    }

    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="target"/> and returns its sha256 (lowercase hex).
    /// <paramref name="progress"/> gets (bytes done, total bytes or null) about ten times a second.
    /// </summary>
    public static async Task<string> DownloadAsync(string url, string target, Action<long, long?>? progress, CancellationToken cancel)
    {
        RequireHttps(url);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var partial = target + ".part";
        try
        {
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel))
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Сервер ответил {(int)response.StatusCode} {response.ReasonPhrase} на {url}");
                var length = response.Content.Headers.ContentLength;
                await using var input = await response.Content.ReadAsStreamAsync(cancel);
                await using var output = File.Create(partial);
                var buffer = new byte[81920];
                long done = 0;
                var lastReport = Stopwatch.StartNew();
                int read;
                while ((read = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancel);
                    done += read;
                    if (lastReport.ElapsedMilliseconds > 100)
                    {
                        progress?.Invoke(done, length);
                        lastReport.Restart();
                    }
                }
                progress?.Invoke(done, length ?? done);
            }
            var sha = Sha256Of(partial);
            File.Move(partial, target, overwrite: true);
            return sha;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    public static string Sha256Of(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>File name from the URL path, or the server's Content-Disposition is not known yet: "download.bin".</summary>
    public static string FileNameFromUrl(string url)
    {
        var name = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath)) : "";
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrEmpty(name) ? "download.bin" : name;
    }
}
