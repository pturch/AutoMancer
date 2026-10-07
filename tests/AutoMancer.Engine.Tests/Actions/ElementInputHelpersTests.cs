// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using AutoMancer.Engine.Errors;
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

    [Fact]
    public void GetCenter_EnabledOnscreenElement_ReturnsRectCenter()
    {
        var (x, y) = ElementInputHelpers.GetCenter(Handle(isEnabled: true, isOffscreen: false, rect: new Rect(10, 20, 30, 40)));

        Assert.Equal(25, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void GetCenter_UnknownEnabledOffscreenState_ReturnsRectCenter()
    {
        // Win32-resolved elements leave IsEnabled/IsOffscreen null when they can't be determined — that must not be treated as "known bad."
        var (x, y) = ElementInputHelpers.GetCenter(Handle(isEnabled: null, isOffscreen: null, rect: new Rect(0, 0, 10, 10)));

        Assert.Equal(5, x);
        Assert.Equal(5, y);
    }

    [Fact]
    public void GetCenter_Disabled_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.GetCenter(Handle(isEnabled: false)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsEnabled=false", ex.Message);
    }

    [Fact]
    public void GetCenter_Offscreen_ThrowsElementNotInteractableError()
    {
        var ex = Assert.Throws<ElementNotInteractableError>(() => ElementInputHelpers.GetCenter(Handle(isEnabled: true, isOffscreen: true)));

        Assert.Contains("Save", ex.Message);
        Assert.Contains("IsOffscreen=true", ex.Message);
    }
}
