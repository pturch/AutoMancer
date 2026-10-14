// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
using AutoMancer.Engine.Providers;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class ElementInputHelpersTests
{
    // Reproduces a COMException observed against a live split-button MenuItem: get_CurrentNativeWindowHandle timed out mid-click.
    [Fact]
    public void EnsureForeground_CurrentNativeWindowHandleThrows_LogsInsteadOfThrowing()
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupGet(e => e.CurrentNativeWindowHandle).Throws(new COMException("Operation timed out.", unchecked((int)0x80131505)));
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        ElementInputHelpers.EnsureForeground(element);

        Assert.Contains("CurrentNativeWindowHandle failed", writer.ToString());
    }

    private const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

    // Id of a process that has already exited — stands in for an app killed out from under a held element.
    private static int ExitedProcessId()
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        process.WaitForExit(2000);
        return process.Id;
    }

    private static ElementHandle OwnedBy(int processId) => new("1", "uia3", new object()) { ProcessId = processId };

    [Fact]
    public void IsStaleFailure_ElementNotAvailableHResult_IsStale() =>
        Assert.True(ElementInputHelpers.IsStaleFailure(new COMException("gone", UIA_E_ELEMENTNOTAVAILABLE), OwnedBy(Environment.ProcessId)));

    [Fact]
    public void IsStaleFailure_Uia2ElementNotAvailableException_IsStale() =>
        Assert.True(ElementInputHelpers.IsStaleFailure(new System.Windows.Automation.ElementNotAvailableException(), OwnedBy(Environment.ProcessId)));

    // UIA reports E_UNEXPECTED for ~30ms after an element's process exits, before switching to UIA_E_ELEMENTNOTAVAILABLE.
    [Fact]
    public void IsStaleFailure_UnexpectedFailureAfterProcessExit_IsStale() =>
        Assert.True(ElementInputHelpers.IsStaleFailure(new COMException("unexpected", E_UNEXPECTED), OwnedBy(ExitedProcessId())));

    // E_UNEXPECTED is generic, so with the owning process still alive it stays an ordinary failure.
    [Fact]
    public void IsStaleFailure_UnexpectedFailureWhileProcessAlive_IsNotStale() =>
        Assert.False(ElementInputHelpers.IsStaleFailure(new COMException("unexpected", E_UNEXPECTED), OwnedBy(Environment.ProcessId)));

    [Fact]
    public void IsStaleFailure_UnexpectedFailureWithUnknownProcess_IsNotStale() =>
        Assert.False(ElementInputHelpers.IsStaleFailure(new COMException("unexpected", E_UNEXPECTED), OwnedBy(0)));

    [Fact]
    public void IsStaleFailure_OtherComFailure_IsNotStale() =>
        Assert.False(ElementInputHelpers.IsStaleFailure(new COMException("Operation timed out.", unchecked((int)0x80131505)), OwnedBy(ExitedProcessId())));

    [Fact]
    public void EnsureForeground_UnexpectedFailureAfterProcessExit_ThrowsStaleElementError()
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupGet(e => e.CurrentNativeWindowHandle).Throws(new COMException("unexpected", E_UNEXPECTED));
        var element = new ElementHandle("1", "uia3", native.Object) { ProcessId = ExitedProcessId() };

        var ex = Assert.Throws<StaleElementError>(() => ElementInputHelpers.EnsureForeground(element));

        Assert.Same(element, ex.Element);
    }

    // Control: the same E_UNEXPECTED from a live process keeps the original log-and-continue tolerance.
    [Fact]
    public void EnsureForeground_UnexpectedFailureWhileProcessAlive_LogsInsteadOfThrowing()
    {
        var native = new Mock<IUIAutomationElement>();
        native.SetupGet(e => e.CurrentNativeWindowHandle).Throws(new COMException("unexpected", E_UNEXPECTED));
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { ProcessId = Environment.ProcessId, Logger = new EngineLogger(writer) };

        ElementInputHelpers.EnsureForeground(element);

        Assert.Contains("CurrentNativeWindowHandle failed", writer.ToString());
    }

    private static ElementHandle Handle(bool? isEnabled = null, bool? isOffscreen = null, Rect rect = default) =>
        new ElementHandle("1", "test", new object()) { Name = "Save", AutomationId = "SaveButton", IsEnabled = isEnabled, IsOffscreen = isOffscreen, BoundingRect = rect == default ? new Rect(0, 0, 10, 10) : rect };

    // A UIA element whose one batched read reports these live values, while its handle's find-time values say enabled, visible, at (0,0)-(10,10).
    private static (ElementHandle Element, Mock<IUIAutomationElement> Native) LiveElement(bool isOffscreen, tagRECT bounds)
    {
        var live = new Mock<IUIAutomationElement>();
        live.SetupGet(e => e.CachedIsEnabled).Returns(1);
        live.SetupGet(e => e.CachedIsOffscreen).Returns(isOffscreen ? 1 : 0);
        live.SetupGet(e => e.CachedBoundingRectangle).Returns(bounds);
        var native = new Mock<IUIAutomationElement>();
        native.Setup(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>())).Returns(live.Object);
        var element = new ElementHandle("1", "uia3", native.Object) { Name = "Save", AutomationId = "SaveButton", IsEnabled = true, IsOffscreen = false, BoundingRect = new Rect(0, 0, 10, 10) };
        return (element, native);
    }

    // Win32-resolved elements leave IsEnabled/IsOffscreen null when they can't be determined — that must not be treated as "known bad."
    [Fact]
    public void EnsureInteractable_UnknownEnabledOffscreenState_Passes()
        => ElementInputHelpers.EnsureInteractable(Handle(isEnabled: null, isOffscreen: null));

    [Fact]
    public void EnsureInteractable_Disabled_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.EnsureInteractable(Handle(isEnabled: false)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsEnabled=false", ex.Message);
    }

    [Fact]
    public void EnsureInteractable_Offscreen_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.EnsureInteractable(Handle(isEnabled: true, isOffscreen: true)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsOffscreen=true", ex.Message);
    }

    // Found visible, since scrolled out of view: the check judges the live read, made in one batched call.
    [Fact]
    public void EnsureInteractable_Uia3_JudgesOneBatchedLiveRead()
    {
        var (element, native) = LiveElement(isOffscreen: true, new tagRECT { left = 0, top = 0, right = 10, bottom = 10 });

        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.EnsureInteractable(element));

        Assert.Contains("IsOffscreen=true", ex.Message);
        // BuildUpdatedCache is the only call on the element itself; the values come from the snapshot it returns.
        Assert.Single(native.Invocations);
        native.Verify(e => e.BuildUpdatedCache(It.IsAny<IUIAutomationCacheRequest>()), Times.Once);
    }

    [Fact]
    public void GetCenter_ReturnsBoundsCenter()
    {
        var (x, y) = ElementInputHelpers.GetCenter(Handle(rect: new Rect(10, 20, 30, 40)));

        Assert.Equal(25, x);
        Assert.Equal(40, y);
    }

    // Found at one position, since moved: synthesized input aims at where the element is now.
    [Fact]
    public void GetCenter_Uia3_AimsAtLiveBounds()
    {
        var (element, native) = LiveElement(isOffscreen: false, new tagRECT { left = 100, top = 200, right = 150, bottom = 220 });

        var (x, y) = ElementInputHelpers.GetCenter(element);

        Assert.Equal(125, x);
        Assert.Equal(210, y);
        Assert.Single(native.Invocations);
    }

    // GetCenter validates its own read rather than relying on EnsureInteractable having run, so it never aims at (0,0) or a stale position.
    [Fact]
    public void GetCenter_EmptyBounds_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.GetCenter(Handle(rect: new Rect(5, 5, 0, 0))));

        Assert.Contains("empty on-screen bounds", ex.Message);
    }

    // Independent of the enabled/visible check: a disabled element still has a point to aim at (what a forced click will rely on).
    [Fact]
    public void GetCenter_DisabledElement_StillReturnsCenter()
    {
        var (x, y) = ElementInputHelpers.GetCenter(Handle(isEnabled: false, rect: new Rect(0, 0, 10, 10)));

        Assert.Equal(5, x);
        Assert.Equal(5, y);
    }

    // Creates a window of className with the given style and rect; returns its hwnd, or IntPtr.Zero on failure.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string? windowName, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    // Destroys a window this process created.
    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr windowHandle);

    // Moves and resizes windowHandle to the given rect in screen coordinates.
    [DllImport("user32.dll")]
    private static extern bool MoveWindow(IntPtr windowHandle, int x, int y, int width, int height, bool repaint);

    // A real but never-shown top-level window in this process, so Win32 live reads have a genuine hwnd to read without touching the desktop.
    private static IntPtr CreateHiddenWindow(int x, int y, int width, int height)
    {
        const int WS_POPUP = unchecked((int)0x80000000);
        var windowHandle = CreateWindowEx(0, "STATIC", null, WS_POPUP, x, y, width, height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, windowHandle);
        return windowHandle;
    }

    // Found at one position, since moved: a Win32 handle aims at the window's current rect, not the one read at find time.
    [Fact]
    public void GetCenter_Win32_AimsAtWindowsCurrentRect()
    {
        var windowHandle = CreateHiddenWindow(100, 100, 200, 100);
        try
        {
            var element = new ElementHandle($"hwnd:{windowHandle}", "win32", windowHandle) { BoundingRect = new Rect(100, 100, 200, 100) };
            MoveWindow(windowHandle, 300, 400, 200, 100, false);

            var (x, y) = ElementInputHelpers.GetCenter(element);

            Assert.Equal(400, x);
            Assert.Equal(450, y);
        }
        finally
        {
            DestroyWindow(windowHandle);
        }
    }

    // This test thread owns the window, so SetFocus needs no input-queue join and never touches the user's foreground window.
    [Fact]
    public void EnsureKeyboardFocus_Win32_FocusesWindowAndConfirmsIt()
    {
        var windowHandle = CreateHiddenWindow(100, 100, 200, 100);
        try
        {
            var element = new ElementHandle($"hwnd:{windowHandle}", "win32", windowHandle);

            ElementInputHelpers.EnsureKeyboardFocus(element);

            Assert.True(NativeMethods.HasFocusWithin(windowHandle));
        }
        finally
        {
            DestroyWindow(windowHandle);
        }
    }

    [Fact]
    public void EnsureKeyboardFocus_Win32_DestroyedWindow_ThrowsStaleElementError()
    {
        var windowHandle = CreateHiddenWindow(100, 100, 200, 100);
        DestroyWindow(windowHandle);
        var element = new ElementHandle($"hwnd:{windowHandle}", "win32", windowHandle);

        Assert.Same(element, Assert.Throws<StaleElementError>(() => ElementInputHelpers.EnsureKeyboardFocus(element)).Element);
    }

    // Find time said visible, but the window isn't shown: the live user32 read wins.
    [Fact]
    public void EnsureInteractable_Win32_HiddenWindow_ThrowsElementNotInteractableError()
    {
        var windowHandle = CreateHiddenWindow(100, 100, 200, 100);
        try
        {
            var element = new ElementHandle($"hwnd:{windowHandle}", "win32", windowHandle) { Name = "Hidden", IsEnabled = true, BoundingRect = new Rect(100, 100, 200, 100) };

            var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.EnsureInteractable(element));

            Assert.Contains("IsOffscreen=true", ex.Message);
        }
        finally
        {
            DestroyWindow(windowHandle);
        }
    }
}
