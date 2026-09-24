// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Core;
using AutoMancer.Engine.Providers;

namespace AutoMancer.Engine.Tests.Providers;

// Exercises the hand-declared IUIAutomationRegistrar COM interop directly — no live window/session needed, since registration is a session-wide UI Automation Core call, not tied to any app.
public sealed class UiaRegistrarInteropTests
{
    [Fact]
    public async Task RegisterCustomPropertyAsync_SameGuidTwice_ReturnsTheSameId()
    {
        var guid = Guid.NewGuid();

        var first = await UiaRegistrarInterop.RegisterCustomPropertyAsync(guid, "AutoMancer.Test.Property", UiaAutomationType.String);
        var second = await UiaRegistrarInterop.RegisterCustomPropertyAsync(guid, "AutoMancer.Test.Property", UiaAutomationType.String);

        Assert.True(first > 0);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task RegisterCustomPropertyAsync_DifferentGuids_ReturnDifferentIds()
    {
        var first = await UiaRegistrarInterop.RegisterCustomPropertyAsync(Guid.NewGuid(), "AutoMancer.Test.PropertyA", UiaAutomationType.Int);
        var second = await UiaRegistrarInterop.RegisterCustomPropertyAsync(Guid.NewGuid(), "AutoMancer.Test.PropertyB", UiaAutomationType.Int);

        Assert.NotEqual(first, second);
    }

    // End-to-end proof that a resolved id is a real, usable PropertyId: feeding it into Locator.ByProperty (2.1.3) produces an actual UIA condition, the same path FindAsync would take live — not just an opaque number.
    [Fact]
    public async Task RegisterCustomPropertyAsync_ResolvedId_FeedsIntoAWorkingByPropertyCondition()
    {
        var resolvedId = await UiaRegistrarInterop.RegisterCustomPropertyAsync(Guid.NewGuid(), "AutoMancer.Test.Status", UiaAutomationType.String);

        var condition = Uia3Provider.BuildCondition(Locator.ByProperty(resolvedId, "Ready"));

        Assert.NotNull(condition);
    }

    // The other tests only reuse the cached Registrar instance across separate sequential awaits; this proves it also survives genuinely concurrent calls from many thread-pool threads at once, without the apartment-threading exception a wrongly-cached STA object would throw.
    [Fact]
    public async Task RegisterCustomPropertyAsync_CalledConcurrently_DoesNotThrowAndStaysConsistent()
    {
        var sharedGuid = Guid.NewGuid();

        var calls = Enumerable.Range(0, 20)
            .Select(_ => UiaRegistrarInterop.RegisterCustomPropertyAsync(sharedGuid, "AutoMancer.Test.Concurrent", UiaAutomationType.Int));

        var ids = await Task.WhenAll(calls);

        Assert.All(ids, id => Assert.Equal(ids[0], id));
    }
}
