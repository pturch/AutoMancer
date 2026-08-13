// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class InputDeliveryErrorTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var ex = new InputDeliveryError(requestedCount: 3, deliveredCount: 2);

        Assert.Equal(3, ex.RequestedCount);
        Assert.Equal(2, ex.DeliveredCount);
    }

    // Message states only the observed count mismatch — no Win32 code is carried at all, since SendInput's own docs say GetLastError doesn't reliably reflect a UIPI-blocked send; check the Windows Event Log artifact instead.
    [Fact]
    public void Message_StatesCountMismatchOnly()
    {
        var ex = new InputDeliveryError(requestedCount: 3, deliveredCount: 0);

        Assert.Contains("0 of 3", ex.Message);
        Assert.DoesNotContain("UIPI", ex.Message);
    }
}
