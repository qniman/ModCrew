using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32;

namespace ModCrew.Update;

/// <summary>
/// Per-user install without admin rights: %LOCALAPPDATA%\Programs\ModCrew\ModCrew.exe, Start menu and desktop
/// shortcuts, an entry in «Параметры → Приложения». ModCrew-Setup.exe is the same exe as ModCrew.exe —
/// it switches to installer mode because of its file name.
/// </summary>
public static class Setup
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ModCrew";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ModCrew");

    public static string InstalledExe => Path.Combine(InstallDir, "ModCrew.exe");

    static string StartMenuLink => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ModCrew.lnk");

    static string DesktopLink => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ModCrew.lnk");

    /// <summary>Started as ModCrew-Setup.exe (or anything with "setup" in the name).</summary>
    public static bool IsSetupExe =>
        Path.GetFileNameWithoutExtension(Updater.ExePath).Contains("setup", StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalledCopy =>
        string.Equals(Path.GetFullPath(Updater.ExePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase);

    public static string? InstalledVersion()
    {
        if (!File.Exists(InstalledExe)) return null;
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
        return key?.GetValue("DisplayVersion") as string ?? "?";
    }

    public static bool HasDesktopShortcut => File.Exists(DesktopLink);

    /// <summary>Copies this exe into the install folder and registers it. Works even while ModCrew runs.</summary>
    public static void Install(bool desktopShortcut)
    {
        Directory.CreateDirectory(InstallDir);
        var target = InstalledExe;
        string? old = null;
        if (File.Exists(target))
        {
            // A running ModCrew keeps its file locked for writing but allows renaming.
            old = target + ".old";
            TryDelete(old);
            if (File.Exists(old)) old = target + $".{Guid.NewGuid():N}.old";
            File.Move(target, old);
        }
        File.Copy(Updater.ExePath, target);
        if (old != null) TryDelete(old); // fails only if that version is still running; it cleans up on next start

        Shell.RegisterFileType(target);
        CreateShortcut(StartMenuLink, target);
        if (desktopShortcut) CreateShortcut(DesktopLink, target);
        else if (File.Exists(DesktopLink)) File.Delete(DesktopLink);

        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "ModCrew");
        key.SetValue("DisplayVersion", Updater.CurrentText);
        key.SetValue("Publisher", "qniman");
        key.SetValue("DisplayIcon", $"\"{target}\",0");
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("UninstallString", $"\"{target}\" --uninstall");
        key.SetValue("URLInfoAbout", $"https://github.com/{Updater.Repo}");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(target).Length / 1024), RegistryValueKind.DWord);
    }

    /// <summary>After a self-update of the installed copy, «Приложения» shows the new version.</summary>
    public static void RecordVersion(string version)
    {
        if (!IsInstalledCopy) return;
        using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
        key?.SetValue("DisplayVersion", version);
    }

    /// <summary>ModCrew.exe --uninstall (from «Приложения»). Plain dialogs: the WebView is not needed for this.</summary>
    public static void RunUninstall()
    {
        var others = Process.GetProcessesByName("ModCrew").Where(p => p.Id != Environment.ProcessId).ToList();
        if (others.Count > 0 &&
            MessageBox.Show("Сначала закрой ModCrew, потом нажми «Повторить».", "Удаление ModCrew",
                MessageBoxButtons.RetryCancel, MessageBoxIcon.Information) != DialogResult.Retry)
            return;

        if (MessageBox.Show(
                "Удалить ModCrew с компьютера?\n\nМоды, которые стоят в играх, останутся. Если хочешь убрать и их — " +
                "отмени, открой ModCrew и нажми «Удалить из игры» у нужных сборок.",
                "Удаление ModCrew", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        bool wipeData = MessageBox.Show(
            "Удалить и данные ModCrew (добавленные сборки, кэш загрузок, настройки)?\n\n«Нет» — оставить, пригодятся при переустановке.",
            "Удаление ModCrew", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        foreach (var link in new[] { StartMenuLink, DesktopLink })
            try { if (File.Exists(link)) File.Delete(link); } catch (IOException) { }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        Shell.UnregisterFileType();
        if (wipeData)
            try { Directory.Delete(AppPaths.Data, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        // This exe is still running: a hidden cmd removes the folder a moment after we exit.
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{InstallDir}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
        MessageBox.Show("ModCrew удалён.", "Удаление ModCrew", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ---------------------------------------------------------------- .lnk via the Shell COM object

    static void CreateShortcut(string path, string target)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(target);
        link.SetWorkingDirectory(Path.GetDirectoryName(target)!);
        link.SetIconLocation(target, 0);
        link.SetDescription("ModCrew — моды для своих");
        ((IPersistFile)link).Save(path, true);
        Marshal.FinalReleaseComObject(link);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder file, int max, IntPtr findData, int flags);
        void GetIDList(out IntPtr idl);
        void SetIDList(IntPtr idl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder name, int max);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder dir, int max);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder args, int max);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder path, int max, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, int reserved);
        void Resolve(IntPtr hwnd, int flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
}
