// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Actions;

namespace AutoMancer.Engine.Tests.Actions;

public sealed class HeldKeyTrackerTests
{
    [Fact]
    public void DrainHeld_ReturnsAddedKeys_ThenEmptyOnNextCall()
    {
        var tracker = new HeldKeyTracker();
        tracker.Add(Key.W);
        tracker.Add(Key.A);

        var drained = tracker.DrainHeld();

        Assert.Equal(2, drained.Count);
        Assert.Contains(Key.W, drained);
        Assert.Contains(Key.A, drained);
        Assert.Empty(tracker.DrainHeld());
    }

    [Fact]
    public void Remove_ExcludesKeyFromNextDrain()
    {
        var tracker = new HeldKeyTracker();
        tracker.Add(Key.W);
        tracker.Add(Key.A);

        tracker.Remove(Key.W);

        Assert.Equal([Key.A], tracker.DrainHeld());
    }

    [Fact]
    public void Add_SameKeyTwice_DrainsOnce()
    {
        var tracker = new HeldKeyTracker();
        tracker.Add(Key.W);
        tracker.Add(Key.W);

        Assert.Equal([Key.W], tracker.DrainHeld());
    }

    // Regression guard: HeldKeyTracker backs concurrent App.KeyDownAsync/KeyUpAsync calls (e.g. holding W+A while moving the mouse); a bare HashSet corrupts under concurrent Add/Remove/DrainHeld.
    [Fact]
    public void ConcurrentAddAndRemove_DoesNotThrowOrCorruptState()
    {
        var tracker = new HeldKeyTracker();
        var keys = Enum.GetValues<Key>();

        Parallel.ForEach(keys, key =>
        {
            tracker.Add(key);
            tracker.Remove(key);
            tracker.Add(key);
        });

        var drained = tracker.DrainHeld();

        Assert.Equal(keys.Length, drained.Count);
        Assert.Equal(keys.ToHashSet(), drained.ToHashSet());
    }
}
