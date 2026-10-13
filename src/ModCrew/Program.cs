using System.Diagnostics;
using ModCrew.Update;

namespace ModCrew;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--uninstall"))
        {
            Setup.RunUninstall();
            return;
        }

        // ModCrew-Setup.exe: the same exe in installer mode, no single-instance rules.
        if (Setup.IsSetupExe)
        {
            Application.Run(new MainForm(Array.Empty<string>(), setupMode: true));
            return;
        }

        // Restart after an update: wait for the old process to let go of the single-instance lock.
        var after = Array.IndexOf(args, "--after");
        if (after >= 0 && after + 1 < args.Length && int.TryParse(args[after + 1], out var pid))
        {
            try { Process.GetProcessById(pid).WaitForExit(15000); } catch (ArgumentException) { }
            args = args.Where((_, i) => i != after && i != after + 1).ToArray();
        }

        // One window per user: a second start (double-clicked .modcrew) hands its files to the first.
        using var mutex = new Mutex(true, "ModCrew.SingleInstance." + Environment.UserName, out bool first);
        if (!first && Shell.SendToRunning(args)) return;

        Updater.CleanUp();
        Application.Run(new MainForm(args, setupMode: false));
    }
}
