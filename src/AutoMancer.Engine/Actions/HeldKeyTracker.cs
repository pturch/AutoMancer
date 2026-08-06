// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Actions;

// Tracks keys currently held via App.KeyDownAsync, so a crash or early Kill/Dispose can still release every key still down instead of leaving it stuck at the OS level, as funny as that might be. 
// Locked because App's async key methods can legitimately be called concurrently (e.g. holding W+A while moving the mouse).
internal sealed class HeldKeyTracker
{
    private readonly object _gate = new();
    private readonly HashSet<Key> _held = [];

    // Records key as held.
    public void Add(Key key) { lock (_gate) _held.Add(key); }

    // Forgets key — call after releasing it normally via KeyUpAsync.
    public void Remove(Key key) { lock (_gate) _held.Remove(key); }

    // Returns every key still recorded as held and forgets them all; returns empty on a second call.
    public IReadOnlyList<Key> DrainHeld()
    {
        lock (_gate)
        {
            var keys = _held.ToList();
            _held.Clear();
            return keys;
        }
    }
}
