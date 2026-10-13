using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using ModCrew.Packs;

namespace ModCrew.Install;

public sealed record InstallProgress(int Step, int TotalSteps, string Mod, string Text, double? Fraction = null);

/// <summary>Asks the player something (title, text, ok-button label) and returns true for OK.</summary>
public delegate Task<bool> AskUser(string title, string text, string okLabel);

/// <summary>Runs a pack's steps against one game folder, journaling every change.</summary>
public sealed class Installer
{
    readonly PackManifest pack;
    readonly PathResolver paths;
    readonly Journal journal;
    readonly string downloadCache;
    readonly IProgress<InstallProgress> progress;
    readonly AskUser ask;
    readonly CancellationToken cancel;

    int stepNo, totalSteps;
    string modName = "";

    public Installer(PackManifest pack, PathResolver paths, Journal journal, string downloadCache,
        IProgress<InstallProgress> progress, AskUser ask, CancellationToken cancel = default)
    {
        this.pack = pack;
        this.paths = paths;
        this.journal = journal;
        this.downloadCache = downloadCache;
        this.progress = progress;
        this.ask = ask;
        this.cancel = cancel;
    }

    /// <summary>Runs all steps. Throws on the first failure; the caller rolls back via the journal.</summary>
    public async Task RunAsync(Action? afterStep = null)
    {
        totalSteps = pack.Mods.Sum(m => m.Steps.Count);
        stepNo = 0;
        foreach (var mod in pack.Mods)
        {
            modName = mod.Name;
            foreach (var step in mod.Steps)
            {
                cancel.ThrowIfCancellationRequested();
                stepNo++;
                Report(step.Title ?? Describe(step));
                try
                {
                    await RunStep(step);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new InstallException($"{mod.Name}, шаг {stepNo}: {e.Message}", e);
                }
                afterStep?.Invoke();
            }
        }
        Report("Готово");
    }

    void Report(string text, double? fraction = null) =>
        progress.Report(new InstallProgress(stepNo, totalSteps, modName, text, fraction));

    static string Describe(Step s) => s.Action switch
    {
        "download" => $"Скачиваю {s.SaveAs ?? FileNameFromUrl(s.Url ?? "")}",
        "extract" => $"Распаковываю {Path.GetFileName(s.From)}",
        "copy" => $"Копирую {Path.GetFileName(s.From?.TrimEnd('/', '\\'))}",
        "delete" => $"Удаляю {Path.GetFileName(s.Path?.TrimEnd('/', '\\'))}",
        "replaceText" or "setIni" => $"Настраиваю {Path.GetFileName(s.File)}",
        "run" => $"Запускаю {Path.GetFileName(s.Path)}",
        _ => s.Action,
    };

    Task RunStep(Step s)
    {
        switch (s.Action)
        {
            case "download": return Download(s);
            case "extract": Extract(s); break;
            case "copy": Copy(s); break;
            case "delete": Delete(s); break;
            case "replaceText": ReplaceText(s); break;
            case "setIni": SetIni(s); break;
            case "run": return Run(s);
            default: throw new InvalidDataException($"Неизвестное действие «{s.Action}».");
        }
        return Task.CompletedTask;
    }

    static string Need(string? value, string field) =>
        string.IsNullOrEmpty(value) ? throw new InvalidDataException($"Не заполнено поле «{field}».") : value;

    // ---------------------------------------------------------------- download

    async Task Download(Step s)
    {
        var url = Need(s.Url, "url");
        Downloader.RequireHttps(url);
        var target = paths.ForWrite(s.SaveAs ?? FileNameFromUrl(url));
        if (!paths.IsInWork(target))
            throw new InvalidDataException("download сохраняет только во временную папку; дальше используй copy/extract.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var expected = s.Sha256?.Trim().ToLowerInvariant();
        var cached = expected != null ? Path.Combine(downloadCache, expected) : null;
        if (cached != null && File.Exists(cached))
        {
            File.Copy(cached, target, overwrite: true);
            return;
        }

        var name = Path.GetFileName(target);
        var actual = await Downloader.DownloadAsync(url, target, (done, length) =>
        {
            if (length > 0)
                Report($"Скачиваю {name} — {done / 1048576.0:0.0} из {length / 1048576.0:0.0} МБ", (double)done / length.Value);
        }, cancel);
        if (expected != null && actual != expected)
        {
            File.Delete(target);
            throw new InvalidDataException("Скачанный файл не совпадает с ожидаемым (sha256). Возможно, автор обновил его по той же ссылке.");
        }
        if (expected != null)
        {
            Directory.CreateDirectory(downloadCache);
            File.Copy(target, cached!, overwrite: true);
        }
    }

    static string FileNameFromUrl(string url) => Downloader.FileNameFromUrl(url);

    // ---------------------------------------------------------------- files

    void Extract(Step s)
    {
        var zip = paths.ForRead(Need(s.From, "from"));
        var to = paths.ForWrite(s.To ?? Path.GetFileNameWithoutExtension(zip));
        if (!File.Exists(zip)) throw new FileNotFoundException($"Нет архива {s.From}.");
        if (paths.IsInWork(to))
        {
            Directory.CreateDirectory(to);
            ZipFile.ExtractToDirectory(zip, to, overwriteFiles: true);
            return;
        }
        // Into the game: unpack to {work} first, then copy file by file through the journal.
        var temp = Path.Combine(paths.Work, ".x-" + Guid.NewGuid().ToString("N")[..8]);
        ZipFile.ExtractToDirectory(zip, temp);
        CopyTree(temp, to, overwrite: true);
    }

    void Copy(Step s)
    {
        var from = paths.ForRead(Need(s.From, "from"));
        var toRaw = Need(s.To, "to");
        var to = paths.ForWrite(toRaw);
        bool overwrite = s.Overwrite ?? true;

        if (Directory.Exists(from))
        {
            CopyTree(from, to, overwrite);
            return;
        }
        if (!File.Exists(from)) throw new FileNotFoundException($"Нет файла {s.From}.");
        // "to" ending with a slash, or an existing folder, means "into this folder".
        if (toRaw.EndsWith('/') || toRaw.EndsWith('\\') || Directory.Exists(to))
            to = Path.Combine(to, Path.GetFileName(from));
        CopyFile(from, to, overwrite);
    }

    void CopyTree(string from, string to, bool overwrite)
    {
        Track(to, isDir: true);
        foreach (var dir in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Track(Path.Combine(to, Path.GetRelativePath(from, dir)), isDir: true);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            cancel.ThrowIfCancellationRequested();
            CopyFile(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite);
        }
    }

    void CopyFile(string from, string to, bool overwrite)
    {
        if (File.Exists(to) && !overwrite) return;
        Track(to, isDir: false);
        File.Copy(from, to, overwrite: true);
    }

    void Delete(Step s)
    {
        var path = paths.ForWrite(Need(s.Path, "path"));
        if (path.Equals(paths.Resolve("{game}"), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Нельзя удалить папку игры целиком.");
        if (Directory.Exists(path))
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                Track(file, isDir: false);
                File.Delete(file);
            }
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            Track(path, isDir: false);
            File.Delete(path);
        }
        // Already gone is fine: the goal is "this file must not be there".
    }

    void ReplaceText(Step s)
    {
        var file = paths.ForWrite(Need(s.File, "file"));
        var find = Need(s.Find, "find");
        if (!File.Exists(file)) throw new FileNotFoundException($"Нет файла {s.File}.");
        var (text, encoding) = ReadText(file);
        if (!text.Contains(find)) throw new InvalidDataException($"В {Path.GetFileName(file)} не найден текст «{find}».");
        Track(file, isDir: false);
        File.WriteAllText(file, text.Replace(find, s.Replace ?? ""), encoding);
    }

    void SetIni(Step s)
    {
        var file = paths.ForWrite(Need(s.File, "file"));
        var key = Need(s.Key, "key");
        var section = s.Section ?? "";
        var (text, encoding) = File.Exists(file) ? ReadText(file) : ("", new UTF8Encoding(false));
        Track(file, isDir: false);
        File.WriteAllText(file, Ini.Set(text, section, key, s.Value ?? ""), encoding);
    }

    static (string, Encoding) ReadText(string file)
    {
        using var reader = new StreamReader(file, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        return (text, reader.CurrentEncoding);
    }

    void Track(string path, bool isDir)
    {
        if (paths.IsInWork(path))
        {
            Directory.CreateDirectory(isDir ? path : Path.GetDirectoryName(path)!);
            return;
        }
        if (isDir) journal.EnsureDir(path);
        else journal.BeforeChange(path);
    }

    // ---------------------------------------------------------------- run

    async Task Run(Step s)
    {
        var exe = paths.ForRead(Need(s.Path, "path"));
        var name = Path.GetFileName(exe);
        if (!File.Exists(exe)) throw new FileNotFoundException($"Нет файла {s.Path}.");
        var ok = await ask(
            "Сейчас откроется установщик",
            $"«{modName}» ставится своим установщиком {name}.\n" +
            "Пройди его до конца (обычно «Далее» → «Установить» → «Готово») — ModCrew подождёт и продолжит сам.\n\n" +
            "Удалять эту программу потом нужно через «Параметры → Приложения»: кнопка «Удалить из игры» её не уберёт.",
            "Открыть установщик");
        if (!ok) throw new OperationCanceledException("Установка отменена.");

        var game = paths.Resolve("{game}");
        var args = s.Args == null ? "" : s.Args.Replace("{game}", game, StringComparison.OrdinalIgnoreCase);
        bool msi = exe.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo(msi ? "msiexec.exe" : exe)
        {
            Arguments = msi ? $"/i \"{exe}\" {args}".TrimEnd() : args,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            UseShellExecute = true,
        };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("Установщику не дали права администратора — без них он не может работать.");
        }
        using (process)
        {
            if (process == null) throw new InvalidOperationException($"{name} не запустился.");
            Report($"Жду, пока ты закончишь с установщиком {name}…");
            await process.WaitForExitAsync(cancel);
            // 1602 = the player pressed Cancel in an MSI installer.
            if (process.ExitCode == 1602) throw new OperationCanceledException($"Установку {name} отменили.");
            if (process.ExitCode != 0 && process.ExitCode != 3010) // 3010 = done, reboot suggested
                throw new InvalidOperationException(
                    $"Установщик {name} закрылся с ошибкой (код {process.ExitCode}). Если ты нажал «Отмена» — просто попробуй ещё раз.");
        }
    }
}

public sealed class InstallException(string message, Exception inner) : Exception(message, inner);
