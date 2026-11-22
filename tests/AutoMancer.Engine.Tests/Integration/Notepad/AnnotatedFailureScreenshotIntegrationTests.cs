// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Integration;

// Live check that a failed find under AnnotateFailureScreenshots leaves an annotated PNG in %TEMP%, and leaves none with the flag off.
[Collection("Notepad")]
[Trait("Category", "Integration")]
public sealed class AnnotatedFailureScreenshotIntegrationTests(NotepadFixture fixture) : IClassFixture<NotepadFixture>
{
    // A failed find with the flag on throws ElementNotFoundError whose ScreenshotPath is a real PNG.
    [Fact]
    public async Task FailedFind_WithFlagOn_SavesAnnotatedPng()
    {
        var app = fixture.App.WithOptions(new AppOptions { ImplicitWaitMs = 500, AnnotateFailureScreenshots = true });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => app.FindAsync(Locator.ByName("No such element")));

        Assert.NotNull(ex.ScreenshotPath);
        Assert.Contains(ex.ScreenshotPath, ex.Message);
        Assert.StartsWith(Path.GetTempPath(), ex.ScreenshotPath, StringComparison.OrdinalIgnoreCase);
        var bytes = await File.ReadAllBytesAsync(ex.ScreenshotPath);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]);
        File.Delete(ex.ScreenshotPath);
    }

    // A near-miss name draws the red box on the closest match's rect: red on its left edge's midpoint, not red at its center.
    [Fact]
    public async Task FailedFind_WithClosestMatch_DrawsRedBoxOnIt()
    {
        var app = fixture.App.WithOptions(new AppOptions { ImplicitWaitMs = 500, AnnotateFailureScreenshots = true });
        var target = (await app.FindAsync(Locator.ByName("File"))).BoundingRect;

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => app.FindAsync(Locator.ByName("Fle")));

        var window = ScreenshotAction.VisibleBounds(app.RootWindowHandle);
        // Disposed before the delete below, since a Bitmap holds its file open.
        using (var image = new System.Drawing.Bitmap(ex.ScreenshotPath!))
        {
            var left = (int)Math.Floor(target.X) - window.Left;
            var top = (int)Math.Floor(target.Y) - window.Top;
            var edge = image.GetPixel(left, top + (int)target.Height / 2);
            var center = image.GetPixel(left + (int)target.Width / 2, top + (int)target.Height / 2);
            Assert.Equal((255, 0, 0), (edge.R, edge.G, edge.B));
            Assert.NotEqual((255, 0, 0), (center.R, center.G, center.B));
        }
        File.Delete(ex.ScreenshotPath!);
    }

    // The default (flag off) writes nothing and leaves ScreenshotPath null.
    [Fact]
    public async Task FailedFind_WithFlagOff_SavesNothing()
    {
        var app = fixture.App.WithOptions(new AppOptions { ImplicitWaitMs = 500 });

        var ex = await Assert.ThrowsAsync<ElementNotFoundError>(() => app.FindAsync(Locator.ByName("No such element")));

        Assert.Null(ex.ScreenshotPath);
    }
}
