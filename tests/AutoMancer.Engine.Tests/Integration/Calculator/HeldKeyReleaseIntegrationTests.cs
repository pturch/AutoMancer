// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine;
using AutoMancer.Engine.Actions;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Integration;

// Verifies App's held-key lifecycle against real OS key state via GetAsyncKeyState.
[Collection("Notepad")] // Uses Calculator, not Notepad, to avoid disturbing the shared Notepad fixture
[Trait("Category", "Integration")]
public sealed class HeldKeyReleaseIntegrationTests
{
    private const string CalculatorAumid = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";

    [Fact]
    public async Task Kill_ReleasesKeysStillHeldDown()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);

        await app.KeyDownAsync(Key.W);
        await Task.Delay(50);
        Assert.True(IsKeyDown(Key.W), "W should be held down after KeyDownAsync.");

        app.Kill();
        await Task.Delay(50);

        Assert.False(IsKeyDown(Key.W), "Kill() should have released the held key.");

        await app.WaitForExitAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task KeyUpAsync_ReleasesHeldKey()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);

        await app.KeyDownAsync(Key.W);
        await Task.Delay(50);
        Assert.True(IsKeyDown(Key.W), "W should be held down after KeyDownAsync.");

        await app.KeyUpAsync(Key.W);
        await Task.Delay(50);
        Assert.False(IsKeyDown(Key.W), "KeyUpAsync should have released the key.");

        app.Kill();
        await app.WaitForExitAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Kill_ReleasesMultipleKeysHeldDown()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);

        await app.KeyDownAsync(Key.W);
        await app.KeyDownAsync(Key.A);
        await Task.Delay(50);
        Assert.True(IsKeyDown(Key.W), "W should be held down after KeyDownAsync.");
        Assert.True(IsKeyDown(Key.A), "A should be held down after KeyDownAsync.");

        app.Kill();
        await Task.Delay(50);

        Assert.False(IsKeyDown(Key.W), "Kill() should have released W.");
        Assert.False(IsKeyDown(Key.A), "Kill() should have released A.");

        await app.WaitForExitAsync();
        await app.DisposeAsync();
    }

    [Fact]
    public async Task KeyUpAsync_ReleasesOnlyTheTargetedKey_WhenMultipleKeysAreHeld()
    {
        var app = await App.LaunchPackagedAsync(CalculatorAumid);

        await app.KeyDownAsync(Key.W);
        await app.KeyDownAsync(Key.A);
        await Task.Delay(50);
        Assert.True(IsKeyDown(Key.W), "W should be held down after KeyDownAsync.");
        Assert.True(IsKeyDown(Key.A), "A should be held down after KeyDownAsync.");

        await app.KeyUpAsync(Key.W);
        await Task.Delay(50);
        Assert.False(IsKeyDown(Key.W), "KeyUpAsync should have released W.");
        Assert.True(IsKeyDown(Key.A), "A should still be held down after only W was released.");

        await app.KeyUpAsync(Key.A);
        await Task.Delay(50);
        Assert.False(IsKeyDown(Key.A), "KeyUpAsync should have released A.");

        app.Kill();
        await app.WaitForExitAsync();
        await app.DisposeAsync();
    }

    private const short KeyDownBit = unchecked((short)0x8000); // high bit of GetAsyncKeyState's result means the key is currently down

    // Checks live OS key state (not the process's), since held keys survive Kill().
    private static bool IsKeyDown(Key key) => (NativeMethods.GetAsyncKeyState(key.ToVirtualKeyCode()) & KeyDownBit) != 0;
}
