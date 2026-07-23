// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

// Harness that roots the entire ANARISharp assembly via TrimmerRootAssembly so
// that every code path, including unused APIs, is compiled by the AOT toolchain.
// Verification: run `dotnet publish -c Release -r win-x64` and confirm no
// IL2xxx / IL3050 warnings are emitted (outside a VS developer prompt,
// vswhere.exe must be on PATH for the native link step).
Console.WriteLine("AOT root test");
