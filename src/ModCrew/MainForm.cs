using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ModCrew;

sealed class MainForm : Form
{
    static readonly Color Background = Color.FromArgb(12, 13, 18);

    readonly WebView2 view;
    readonly string[] startFiles;
    readonly CancellationTokenSource closing = new();
    CoreWebView2Environment? env;
    Bridge? bridge;
    Api? api;

    public MainForm(string[] args)
    {
        startFiles = args;
        Text = "ModCrew";
        BackColor = Background;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(980, 640);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

        view = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Background };
        Controls.Add(view);
        Load += async (_, _) => await InitAsync();
        FormClosed += (_, _) => closing.Cancel();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Dark title bar matching the UI (Windows 10 1809+ / 11; ignored elsewhere).
        int on = 1;
        DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
        int caption = ColorTranslator.ToWin32(Background);
        DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
    }

    async Task InitAsync()
    {
        try
        {
            env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebView);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(this,
                "Для работы нужен Microsoft Edge WebView2 Runtime. Сейчас откроется страница загрузки.",
                "ModCrew", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Process.Start(new ProcessStartInfo("https://go.microsoft.com/fwlink/p/?LinkId=2124703") { UseShellExecute = true });
            Close();
            return;
        }
        await view.EnsureCoreWebView2Async(env);
        var web = view.CoreWebView2;

        var devUrl = Environment.GetEnvironmentVariable("MODCREW_DEV_URL");
#if DEBUG
        var dev = true;
#else
        var dev = devUrl != null;
#endif
        web.Settings.AreDevToolsEnabled = dev;
        web.Settings.AreDefaultContextMenusEnabled = dev;
        web.Settings.IsStatusBarEnabled = false;
        web.Settings.IsZoomControlEnabled = false;
        web.Settings.AreBrowserAcceleratorKeysEnabled = dev;

        ServeUi(web);
        Directory.CreateDirectory(AppPaths.Packs);
        web.SetVirtualHostNameToFolderMapping(Api.PacksHost, AppPaths.Packs, CoreWebView2HostResourceAccessKind.Allow);

        bridge = new Bridge(web, this);
        api = new Api(bridge, this, startFiles);
        if (api.SteamPath != null)
            web.SetVirtualHostNameToFolderMapping(Api.SteamHost,
                Path.Combine(api.SteamPath, "appcache", "librarycache"), CoreWebView2HostResourceAccessKind.Allow);

        var home = devUrl ?? $"https://{Api.AppHost}/index.html";
        var homeOrigin = new Uri(home).GetLeftPart(UriPartial.Authority);

        // Links never navigate the app window: external ones open in the real browser.
        web.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            OpenExternal(e.Uri);
        };
        web.NavigationStarting += (_, e) =>
        {
            if (e.Uri.StartsWith(homeOrigin, StringComparison.OrdinalIgnoreCase)) return;
            e.Cancel = true;
            OpenExternal(e.Uri);
        };

        web.Navigate(home);

        Shell.RegisterFileType();
        _ = Shell.ListenAsync(files => BeginInvoke(() =>
        {
            BringToFront();
            api.OpenFiles(files);
        }), closing.Token);
    }

    new void BringToFront()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        // Windows only lets the foreground app steal focus; a quick TopMost flip gets past that.
        TopMost = true;
        TopMost = false;
        Activate();
    }

    /// <summary>The UI is embedded in the exe (one file to send to friends) and served from memory.</summary>
    void ServeUi(CoreWebView2 web)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("wwwroot/"))
            .ToDictionary(n => n["wwwroot/".Length..].Replace('\\', '/'), n => n, StringComparer.OrdinalIgnoreCase);

        web.AddWebResourceRequestedFilter($"https://{Api.AppHost}/*", CoreWebView2WebResourceContext.All);
        web.WebResourceRequested += (_, e) =>
        {
            var path = Uri.UnescapeDataString(new Uri(e.Request.Uri).AbsolutePath).TrimStart('/');
            if (path.Length == 0) path = "index.html";
            if (!resources.TryGetValue(path, out var name))
            {
                e.Response = env!.CreateWebResourceResponse(null, 404, "Not Found", "");
                return;
            }
            var data = new MemoryStream();
            using (var stream = assembly.GetManifestResourceStream(name)!) stream.CopyTo(data);
            data.Position = 0;
            e.Response = env!.CreateWebResourceResponse(data, 200, "OK", $"Content-Type: {Mime(path)}");
        };
    }

    static string Mime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".ico" => "image/x-icon",
        ".json" => "application/json",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream",
    };

    static void OpenExternal(string uri)
    {
        if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
