// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Text.RegularExpressions;

namespace AutoMancer.Engine.Core;

// A single step in a tree-path expression; null Name and null Index mean "match any element of this type."
public sealed record PathSegment(string ControlType, string? Name, int? Index);

// Parses AutoMancer tree-path strings like "Window > Pane[2] > Button[\"OK\"]" into typed PathSegment records.
public static class AutoMancerPathParser
{
    // Regex: type name (letters or *), followed by an optional bracket containing either a numeric index or a double-quoted name.
    private static readonly Regex SegmentRegex = new(
        @"^(?<type>[A-Za-z]+|\*)(?:\[(?:(?<index>\d+)|""(?<name>[^""]*)"")\])?$",
        RegexOptions.Compiled);

    // Splits a path string on ">" and parses each trimmed segment; throws FormatException on malformed input.
    public static IReadOnlyList<PathSegment> Parse(string path)
    {
        string[] parts = path.Split('>');
        var segments = new PathSegment[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            Match match = SegmentRegex.Match(part);
            if (!match.Success)
                throw new FormatException($"Invalid path segment: '{part}'");

            string controlType = match.Groups["type"].Value;
            string? name = match.Groups["name"].Success ? match.Groups["name"].Value : null;
            int? index = match.Groups["index"].Success ? int.Parse(match.Groups["index"].Value) : null;

            segments[i] = new PathSegment(controlType, name, index);
        }

        return segments;
    }
}
