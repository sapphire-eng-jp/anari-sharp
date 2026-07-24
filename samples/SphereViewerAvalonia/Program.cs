// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using Avalonia;

namespace SphereViewerAvalonia;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
