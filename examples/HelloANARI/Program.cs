// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using ANARISharp;
using ANARISharp.Interop;

// C# port of the ANARI SDK "first frame" tutorial: renders a single triangle
// with the requested library (helide by default) and writes the result as PPM.

const uint width = 800;
const uint height = 600;

string libraryName = args.Length > 0 ? args[0] : "helide";

using var library = ANARILibrary.Load(libraryName,
    (device, source, sourceType, severity, code, message) =>
        Console.Error.WriteLine($"[{severity}] {message}"));

Console.WriteLine($"Loaded library '{libraryName}' (devices: {string.Join(", ", library.GetDeviceSubtypes())})");

using var device = library.NewDevice();
device.CommitParameters();

// Geometry: one triangle with per-vertex colors.
ReadOnlySpan<Vector3> positions = stackalloc Vector3[]
{
    new(-1.0f, -1.0f, 3.0f),
    new( 1.0f, -1.0f, 3.0f),
    new( 0.0f,  1.0f, 3.0f),
};
ReadOnlySpan<Vector4> colors = stackalloc Vector4[]
{
    new(1.0f, 0.0f, 0.0f, 1.0f),
    new(0.0f, 1.0f, 0.0f, 1.0f),
    new(0.0f, 0.0f, 1.0f, 1.0f),
};

using var geometry = device.NewGeometry("triangle");
geometry.SetParameterArray1D("vertex.position", positions);
geometry.SetParameterArray1D("vertex.color", colors);
geometry.CommitParameters();

using var material = device.NewMaterial("matte");
material.SetParameter("color", "color"); // bind the vertex color attribute
material.CommitParameters();

using var surface = device.NewSurface();
surface.SetParameter("geometry", geometry);
surface.SetParameter("material", material);
surface.CommitParameters();

using var world = device.NewWorld();
world.SetParameterArray1D("surface", new ANARIObject[] { surface });
world.CommitParameters();

using var camera = device.NewCamera("perspective");
camera.SetParameter("aspect", (float)width / height);
camera.SetParameter("position", Vector3.Zero);
camera.SetParameter("direction", new Vector3(0f, 0f, 1f));
camera.SetParameter("up", new Vector3(0f, 1f, 0f));
camera.CommitParameters();

using var renderer = device.NewRenderer("default");
renderer.SetParameter("background", new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
renderer.CommitParameters();

using var frame = device.NewFrame();
frame.SetParameter("size", new UVec2(width, height));
frame.SetParameter("channel.color", ANARIDataType.ANARI_UFIXED8_RGBA_SRGB);
frame.SetParameter("world", world);
frame.SetParameter("camera", camera);
frame.SetParameter("renderer", renderer);
frame.CommitParameters();

frame.Render();
frame.Wait();

var mapped = frame.Map<byte>("channel.color");
Console.WriteLine($"Rendered {mapped.Width}x{mapped.Height} ({mapped.PixelType})");
WritePpm("hello_anari.ppm", mapped.Width, mapped.Height, mapped.Data);
frame.Unmap("channel.color");

Console.WriteLine($"Wrote {Path.GetFullPath("hello_anari.ppm")}");

static void WritePpm(string path, uint w, uint h, ReadOnlySpan<byte> rgba)
{
    using var f = new FileStream(path, FileMode.Create, FileAccess.Write);
    f.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n"));

    // PPM stores top-down RGB; the frame buffer is bottom-up RGBA.
    var row = new byte[w * 3];
    for (long y = h - 1; y >= 0; y--)
    {
        var src = rgba.Slice(checked((int)(y * w * 4)), (int)(w * 4));
        for (int x = 0; x < w; x++)
        {
            row[x * 3 + 0] = src[x * 4 + 0];
            row[x * 3 + 1] = src[x * 4 + 1];
            row[x * 3 + 2] = src[x * 4 + 2];
        }
        f.Write(row);
    }
}

