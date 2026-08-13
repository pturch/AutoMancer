// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Engine.Errors;

namespace AutoMancer.Engine.Tests.Errors;

public sealed class Win32ErrorCodesTests
{
    [Fact]
    public void Describe_KnownAutomationCode_IncludesCodeAndHint()
    {
        var description = Win32ErrorCodes.Describe(5);

        Assert.Contains("(Win32 error 5)", description);
        Assert.Contains("UIPI", description);
    }

    [Fact]
    public void Describe_UnknownCode_IncludesCodeWithoutHint()
    {
        // 87 == ERROR_INVALID_PARAMETER — a real Win32 code with no AutoMancer-specific hint registered.
        var description = Win32ErrorCodes.Describe(87);

        Assert.Contains("(Win32 error 87)", description);
        Assert.DoesNotContain("UIPI", description);
    }

    [Fact]
    public void Describe_AlwaysIncludesAnOsProvidedMessageBeforeTheCode()
    {
        var description = Win32ErrorCodes.Describe(5);

        Assert.True(description.IndexOf("(Win32 error 5)", StringComparison.Ordinal) > 0, "Expected OS message text before the code parenthetical.");
    }
}
