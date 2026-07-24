// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using ANARISharp;

namespace SphereViewerWpf;

/// <summary>Builds the demo scene shared by the sphere viewer samples.</summary>
internal static class SphereScene
{
    /// <summary>Builds a world containing randomly placed, colored spheres.</summary>
    public static ANARIWorld CreateWorld(ANARIDevice device, int sphereCount)
    {
        var rng = new Random(42);

        var positions = new Vector3[sphereCount];
        var radii = new float[sphereCount];
        var colors = new Vector4[sphereCount];
        for (int i = 0; i < sphereCount; i++)
        {
            positions[i] = new Vector3(
                rng.NextSingle() * 2f - 1f,
                rng.NextSingle() * 2f - 1f,
                rng.NextSingle() * 2f - 1f);
            radii[i] = 0.03f + rng.NextSingle() * 0.07f;
            colors[i] = new Vector4(
                0.2f + rng.NextSingle() * 0.8f,
                0.2f + rng.NextSingle() * 0.8f,
                0.2f + rng.NextSingle() * 0.8f,
                1.0f);
        }

        using var geometry = device.NewGeometry("sphere");
        geometry.SetParameterArray1D("vertex.position", (ReadOnlySpan<Vector3>)positions);
        geometry.SetParameterArray1D("vertex.radius", (ReadOnlySpan<float>)radii);
        geometry.SetParameterArray1D("vertex.color", (ReadOnlySpan<Vector4>)colors);
        geometry.CommitParameters();

        using var material = device.NewMaterial("matte");
        material.SetParameter("color", "color"); // bind the vertex color attribute
        material.CommitParameters();

        using var surface = device.NewSurface();
        surface.SetParameter("geometry", geometry);
        surface.SetParameter("material", material);
        surface.CommitParameters();

        using var light = device.NewLight("directional");
        light.SetParameter("direction", new Vector3(-1f, -1f, -0.5f));
        light.CommitParameters();

        var world = device.NewWorld();
        world.SetParameterArray1D("surface", new ANARIObject[] { surface });
        world.SetParameterArray1D("light", new ANARIObject[] { light });
        world.CommitParameters();
        return world;
    }
}
