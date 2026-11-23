// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Diagnostics;

namespace AutoMancer.Engine.Actions;

// Reads and writes the system clipboard's text; session-scoped like ScreenshotAction, not tied to any element.
public static class ClipboardAction
{
    // Returns the clipboard's current text, or an empty string when it holds none; logs only the length, since clipboards often hold sensitive text.
    public static async Task<string> GetTextAsync(IEngineLogger? logger = null, CancellationToken ct = default)
    {
        var text = await RunOnStaThreadAsync(System.Windows.Clipboard.GetText, ct);
        logger?.Info("Read clipboard text", new { length = text.Length });
        return text;
    }

    // Replaces the clipboard contents with text, logging only its length; WPF flushes it so it outlives the short-lived calling thread.
    public static async Task SetTextAsync(string text, IEngineLogger? logger = null, CancellationToken ct = default)
    {
        await RunOnStaThreadAsync(() =>
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }, ct);
        logger?.Info("Set clipboard text", new { length = text.Length });
    }

    // Runs work on a fresh STA thread because OLE clipboard access fails on the MTA thread-pool threads Task.Run uses.
    private static Task<T> RunOnStaThreadAsync<T>(Func<T> work, CancellationToken ct)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(work()); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(ct);
    }
}
