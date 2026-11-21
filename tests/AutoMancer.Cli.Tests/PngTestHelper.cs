// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Buffers.Binary;
using System.IO.Compression;

namespace AutoMancer.Cli.Tests;

// A temporary directory that deletes itself on dispose, for tests that write image files.
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("automancer-cli-tests-").FullName;

    // Deletes the directory and everything in it.
    public void Dispose() => Directory.Delete(Path, recursive: true);
}

// Writes tiny solid-color PNG files without needing WPF, so tests can exercise image comparison.
internal static class PngTestHelper
{
    // Writes a width x height 8-bit RGBA PNG filled with one gray level, optionally making the first changedPixels pixels white; returns the file path.
    internal static string Write(string dir, string name, int width, int height, byte gray, int changedPixels = 0)
    {
        var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter type: none
            for (var x = 0; x < width; x++)
            {
                var v = y * width + x < changedPixels ? (byte)255 : gray;
                raw.Write([v, v, v, 255]);
            }
        }

        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw.ToArray());

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // color type: RGBA

        using var file = new MemoryStream();
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(file, "IHDR", header);
        Chunk(file, "IDAT", compressed.ToArray());
        Chunk(file, "IEND", []);
        var path = System.IO.Path.Combine(dir, name);
        File.WriteAllBytes(path, file.ToArray());
        return path;
    }

    // Appends one PNG chunk: length, type, data, then the CRC over type and data.
    private static void Chunk(Stream s, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        s.Write(length);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        s.Write(body);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        s.Write(crc);
    }

    // Standard PNG CRC-32 (polynomial 0xEDB88320) over the given bytes.
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? crc >> 1 ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
