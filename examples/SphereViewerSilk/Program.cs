// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using Silk.NET.Input.Glfw;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;

namespace SphereViewerSilk;

internal static class Program
{
    private static void Main(string[] args)
    {
        // Silk.NET discovers its windowing/input platforms via reflection, which
        // trimming and Native AOT strip out. Register the GLFW platforms explicitly
        // and skip the reflection-based discovery so the published binary works.
        Window.ShouldLoadFirstPartyPlatforms(false);
        GlfwWindowing.RegisterPlatform();
        GlfwInput.RegisterPlatform();

        using var viewer = new Viewer(args.Length > 0 ? args[0] : "helide");
        viewer.Run();
    }
}
