using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace ModCrew;

/// <summary>
/// JSON-RPC-ish channel to the UI over WebView2 messages.
/// UI → host: {id, method, params}; host → UI: {id, ok, result | error}.
/// Host → UI events: {event, data}.
/// </summary>
sealed class Bridge
{
    readonly CoreWebView2 web;
    readonly Control ui;
    readonly Dictionary<string, Func<JsonElement, Task<object?>>> handlers = new();
    readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> questions = new();

    public Bridge(CoreWebView2 web, Control ui)
    {
        this.web = web;
        this.ui = ui;
        web.WebMessageReceived += OnMessage;
        On("ui.answer", p =>
        {
            var token = p.GetProperty("token").GetString()!;
            if (questions.TryRemove(token, out var tcs)) tcs.TrySetResult(p.GetProperty("ok").GetBoolean());
            return (object?)null;
        });
    }

    public void OnAsync(string method, Func<JsonElement, Task<object?>> handler) => handlers[method] = handler;

    public void On(string method, Func<JsonElement, object?> handler) =>
        handlers[method] = p => Task.FromResult(handler(p));

    async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            id = root.GetProperty("id").GetString();
            var method = root.GetProperty("method").GetString() ?? "";
            var args = root.TryGetProperty("params", out var p) ? p.Clone() : default;
            if (!handlers.TryGetValue(method, out var handler))
                throw new InvalidOperationException($"Unknown method {method}");
            var result = await handler(args);
            Post(new { id, ok = true, result });
        }
        catch (Exception ex)
        {
            if (id != null) Post(new { id, ok = false, error = ex.Message, cancelled = ex is OperationCanceledException });
        }
    }

    public void Emit(string name, object? data) => Post(new { @event = name, data });

    /// <summary>Shows a confirm dialog in the UI; safe to call from any thread.</summary>
    public Task<bool> Ask(string title, string text, string okLabel)
    {
        var token = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        questions[token] = tcs;
        Emit("ask", new { token, title, text, okLabel });
        return tcs.Task;
    }

    void Post(object message)
    {
        var json = JsonSerializer.Serialize(message, Json.Options);
        if (ui.InvokeRequired) ui.BeginInvoke(() => web.PostWebMessageAsJson(json));
        else web.PostWebMessageAsJson(json);
    }
}
