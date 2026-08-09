// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Testing.XUnit;

namespace AutoMancer.Engine.Tests.Integration;

// Rewrite of NotepadWorkflowTests' type-and-verify step on AutoMancerTest/Expect() — shorter than the hand-rolled Assert.Contains + GetEditorText version.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class NotepadExpectWorkflowTests(NotepadAppFixture fixture)
    : AutoMancerTest(fixture), IClassFixture<NotepadAppFixture>
{
    // Document's Name is a fixed accessibility label ("Text editor"), not its content, so this asserts on the title bar, which Notepad updates live as you type.
    [Fact]
    public async Task TypeContent_TitleBarShowsTypedText()
    {
        await App.ClickAsync(Locator.ByControlType("Document"));
        await App.TypeAsync(Locator.ByControlType("Document"), "Hello from AutoMancer.");

        await Expect(Locator.ByControlType("TitleBar")).ToHaveTextAsync("Hello from AutoMancer");
    }
}
