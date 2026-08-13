// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown when SendInput accepts fewer events than requested — usually UIPI blocking input across a privilege boundary (e.g. an elevated target process).
public sealed class InputDeliveryError : Exception
{
    public int RequestedCount { get; }
    public int DeliveredCount { get; }

    // Thrown by NativeMethods.SendInputs when the OS delivers fewer events than were queued
    public InputDeliveryError(int requestedCount, int deliveredCount)
        : base($"Only {deliveredCount} of {requestedCount} input events were delivered by the OS.")
    {
        RequestedCount = requestedCount;
        DeliveredCount = deliveredCount;
    }
}
