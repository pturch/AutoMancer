// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace ConsumerVsCodeTests;

// Groups every test class in this sample into one sequential collection — each launches its own isolated VS Code instance (see VsCodeLauncher), but they still shouldn't run concurrently against the desktop's single foreground window.
[CollectionDefinition("VsCode", DisableParallelization = true)]
public sealed class VsCodeCollection { }
