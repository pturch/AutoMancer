// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Diagnostics;
using Interop.UIAutomationClient;
using Moq;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class SetFocusActionTests
{
    [Fact]
    public async Task ExecuteAsync_NonUiaHandle_NoOp()
    {
        var element = new ElementHandle("1", "win32", new object());

        await SetFocusAction.ExecuteAsync(element);
    }

    [Fact]
    public async Task ExecuteAsync_UiaHandle_CallsSetFocus()
    {
        var native = new Mock<IUIAutomationElement>();
        // Zero window handle skips EnsureForeground's SetForegroundWindow call, isolating this test to SetFocus itself.
        native.SetupGet(e => e.CurrentNativeWindowHandle).Returns(IntPtr.Zero);
        var writer = new StringWriter();
        var element = new ElementHandle("1", "uia3", native.Object) { Logger = new EngineLogger(writer) };

        await SetFocusAction.ExecuteAsync(element);

        native.Verify(e => e.SetFocus(), Times.Once);
        Assert.Contains("Focused via SetFocus", writer.ToString());
    }
}
