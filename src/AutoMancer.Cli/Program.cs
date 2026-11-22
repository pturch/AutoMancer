// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using AutoMancer.Cli;

// Write UTF-8 so non-ASCII text (element names, typed text, file paths) isn't turned into question marks; with no console attached there is nothing to configure.
try { Console.OutputEncoding = new System.Text.UTF8Encoding(false); }
catch (System.IO.IOException) { }

return await CliSession.InvokeAsync(CliApp.BuildRoot(), args);
