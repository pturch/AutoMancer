// Copyright (c) AutoMancer Contributors. Licensed under the MIT License.
namespace AutoMancer.Engine.Errors;

public sealed class AppLaunchError : Exception
{
    public AppLaunchError(string message) : base(message) { }
    public AppLaunchError(string message, Exception inner) : base(message, inner) { }
}
