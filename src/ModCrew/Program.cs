namespace ModCrew;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // One window per user: a second start (double-clicked .modcrew) hands its files to the first.
        using var mutex = new Mutex(true, "ModCrew.SingleInstance." + Environment.UserName, out bool first);
        if (!first && Shell.SendToRunning(args)) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args));
    }
}
