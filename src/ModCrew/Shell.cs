using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ModCrew;

/// <summary>Windows integration: .modcrew file association and "open in the running window".</summary>
static class Shell
{
    static readonly string PipeName = "ModCrew.Open." + Environment.UserName;

    /// <summary>
    /// Makes double-clicking a .modcrew file open it in ModCrew (per user, no admin rights).
    /// Points at whichever ModCrew.exe ran last, so moving the exe just works after one start.
    /// </summary>
    public static void RegisterFileType()
    {
        try
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            var command = $"\"{exe}\" \"%1\"";
            using var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            using (var cmd = classes.OpenSubKey(@"ModCrew.Pack\shell\open\command"))
                if (cmd?.GetValue(null) as string == command) return;

            using (var ext = classes.CreateSubKey(".modcrew")) ext.SetValue(null, "ModCrew.Pack");
            using (var type = classes.CreateSubKey("ModCrew.Pack"))
            {
                type.SetValue(null, "Сборка ModCrew");
                using (var icon = type.CreateSubKey("DefaultIcon")) icon.SetValue(null, $"\"{exe}\",0");
                using (var cmd = type.CreateSubKey(@"shell\open\command")) cmd.SetValue(null, command);
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED: refresh Explorer icons
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Not fatal: the player can still use «Добавить сборку».
        }
    }

    /// <summary>Hands command-line files to an already running ModCrew. False if none is running.</summary>
    public static bool SendToRunning(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client);
            writer.WriteLine("activate");
            foreach (var a in args) writer.WriteLine(Path.GetFullPath(a));
            return true;
        }
        catch (Exception e) when (e is TimeoutException or IOException)
        {
            return false;
        }
    }

    /// <summary>Receives what <see cref="SendToRunning"/> sends; calls back on a pool thread.</summary>
    public static async Task ListenAsync(Action<List<string>> onOpen, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancel);
                using var reader = new StreamReader(server);
                var lines = new List<string>();
                while (await reader.ReadLineAsync(cancel) is { } line)
                    if (line != "activate") lines.Add(line);
                onOpen(lines);
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { await Task.Delay(200, CancellationToken.None); }
        }
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
}
