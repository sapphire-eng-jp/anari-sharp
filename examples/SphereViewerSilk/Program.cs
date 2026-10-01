// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

namespace SphereViewerSilk;

internal static class Program
{
    private static void Main(string[] args)
    {
        using var viewer = new Viewer(args.Length > 0 ? args[0] : "helide");
        viewer.Run();
    }
}
