// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>
/// Managed counterpart of ANARIStatusCallback. <paramref name="device"/> and
/// <paramref name="source"/> are raw native handles of the reporting device and object.
/// </summary>
public delegate void ANARIStatusCallback(
    IntPtr device,
    IntPtr source,
    ANARIDataType sourceType,
    ANARIStatusSeverity severity,
    ANARIStatusCode code,
    string message);

internal static unsafe class StatusCallbackThunk
{
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static void Invoke(
        void* userPtr,
        Device* device,
        Interop.Object* source,
        ANARIDataType sourceType,
        int severity,
        int code,
        sbyte* message)
    {
        try
        {
            var gcHandle = GCHandle.FromIntPtr((IntPtr)userPtr);
            if (gcHandle.Target is ANARIStatusCallback callback)
            {
                callback(
                    (IntPtr)device,
                    (IntPtr)source,
                    sourceType,
                    (ANARIStatusSeverity)severity,
                    (ANARIStatusCode)code,
                    Marshal.PtrToStringUTF8((IntPtr)message) ?? string.Empty);
            }
        }
        catch
        {
            // exceptions must never propagate across the native boundary
        }
    }
}

