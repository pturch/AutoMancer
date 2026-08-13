// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Testing.XUnit;

// Process-wide Expect policy, set once (e.g. in a ModuleInitializer) instead of per test class.
public static class AutoMancerTestOptions
{
    // When true, every failed locator-based Expect assertion saves a screenshot to ScreenshotDirectory and folds the path into the failure message.
    public static bool CaptureScreenshotsOnFailure { get; set; } = false;

    // Directory failure screenshots are written to when CaptureScreenshotsOnFailure is true.
    public static string ScreenshotDirectory { get; set; } = Path.GetTempPath();

    // Directory an AppFixture's file logger, if any, should write its log file under.
    public static string LogDirectory { get; set; } = Path.GetTempPath();
}
