// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using static AutoMancer.Testing.Assertions;

namespace AutoMancer.Engine.Tests.Integration;

// Live regressions for the actionability gate, app.Force, and keyboard focus on synthesized typing, against Notepad's File menu and Document.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class ActionabilityIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    private static readonly Locator Document = Locator.ByControlType("Document");
    private static readonly Locator Tabs = Locator.ByControlType("TabItem");
    private readonly App _app = fixture.App.WithOptions(new AppOptions { ProviderChain = ["uia3"] });

    // The case that motivated the gate: a held "New tab" item, clicked after its menu closed, used to open a tab no user could reach. Both handles are found while the menu is open, since no locator can reach the item once it's closed.
    [Fact]
    public async Task HeldMenuItem_AfterMenuCloses_RejectedByDefault_InvokedThroughForce()
    {
        var tabsBefore = (await _app.FindAllAsync(Tabs)).Count;
        await _app.ClickAsync(Locator.ByName("File"));
        var held = await _app.FindAsync(Locator.ByName("New tab"));
        var forced = await _app.Force.FindAsync(Locator.ByName("New tab"));
        NotepadFixture.SendEscape();
        // A closed menu's items drop out of search, so waiting for one to go waits for the menu to close.
        await _app.WaitUntilGoneAsync(Locator.ByName("New tab"));

        try
        {
            await Assert.ThrowsAsync<ElementNotInteractableError>(() => ClickAction.ExecuteAsync(held));

            // Exactly one new tab, so a tab the rejected click had opened too would fail this.
            await ClickAction.ExecuteAsync(forced);
            await Expect(_app, Tabs).ToHaveCountAsync(tabsBefore + 1);
        }
        finally
        {
            // Close the empty tab the forced click opened (no save prompt for an empty tab), leaving the shared window as it was.
            if ((await _app.FindAllAsync(Tabs)).Count > tabsBefore)
                await _app.PressKeyAsync(Key.W, new KeyModifiers(Control: true));
            await Expect(_app, Tabs).ToHaveCountAsync(tabsBefore);
        }
    }

    // Synthesized typing used to go to whatever control had focus; it now focuses the target first. The handle's operator is stripped so native SetValue can't run.
    [Fact]
    public async Task SynthesizedType_FocusElsewhere_LandsInTargetElement()
    {
        await _app.ClearAsync(Document);
        await _app.SetFocusAsync(Locator.ByName("File"));
        var focusedControlType = await Task.Run(() => new CUIAutomation8Class().GetFocusedElement()?.CurrentControlType ?? -1);
        Assert.NotEqual(UIA_ControlTypeIds.UIA_DocumentControlTypeId, focusedControlType);

        var document = await _app.FindAsync(Document);
        var synthesizedOnly = new ElementHandle(document.Id, document.ResolvedVia, document.NativeHandle)
        {
            Name = document.Name,
            IsEnabled = document.IsEnabled,
            IsOffscreen = document.IsOffscreen,
            BoundingRect = document.BoundingRect,
            ForegroundActivationTimeoutMs = document.ForegroundActivationTimeoutMs,
        };

        try
        {
            await TypeAction.ExecuteAsync(synthesizedOnly, "focus check");

            await Expect(_app, Document).ToHaveValueAsync("focus check");
        }
        finally
        {
            await _app.ClearAsync(Document);
        }
    }

    // Win32 handles used to get neither foregrounding nor focus, so keystrokes went wherever focus already was. The Open dialog's file-name box is a real Win32 Edit in Notepad's process, focused across processes.
    [Fact]
    public async Task Win32Type_FocusOnAnotherControl_LandsInTargetEdit()
    {
        await _app.HotkeyAsync(new KeyModifiers(Control: true), [Key.O]);
        var dialog = await App.FindDialogAsync(_app.ProcessId, "Open");
        Assert.NotNull(dialog);

        try
        {
            var win32 = dialog!.WithOptions(new AppOptions { ProviderChain = ["win32"] });
            var edit = await win32.FindAsync(Locator.ByClassName("Edit"));
            await win32.SetFocusAsync(Locator.ByName("Cancel"));
            Assert.False(NativeMethods.HasFocusWithin((IntPtr)edit.NativeHandle));

            await win32.TypeAsync(Locator.ByClassName("Edit"), "focus check");

            Assert.True(NativeMethods.HasFocusWithin((IntPtr)edit.NativeHandle));
            // Read through UIA's ValuePattern, since GetWindowText doesn't return another process's edit contents.
            Assert.Equal("focus check", await dialog.GetValueAsync(Locator.ByClassName("Edit")));
        }
        finally
        {
            // Never dispose the dialog's App — it would kill Notepad; WM_CLOSE cancels the dialog instead.
            await dialog!.CloseWindowAsync();
            await Expect(_app, () => Task.FromResult(NativeMethods.IsWindowVisible(dialog.RootWindowHandle))).ToSatisfyAsync(visible => !visible, "the Open dialog to close");
        }
    }
}
