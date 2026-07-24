// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;

namespace SphereViewerWpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(e.Args.Length > 0 ? e.Args[0] : "helide");
        window.Show();
    }
}
