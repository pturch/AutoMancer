// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Errors;

// Thrown when AppSession fails to launch, attach to, or find the target application's window.
public sealed class AppLaunchError : Exception
{
    public AppLaunchError(string message) : base(message) { }
    public AppLaunchError(string message, Exception inner) : base(message, inner) { }
}
