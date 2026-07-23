// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>Wraps an ANARILibrary handle (anariLoadLibrary/anariUnloadLibrary).</summary>
public sealed unsafe class ANARILibrary : IDisposable
{
    private IntPtr _handle;
    private GCHandle _callbackHandle;

    public IntPtr Handle => _handle;

    private Library* L => (Library*)_handle;

    private ANARILibrary(IntPtr handle, GCHandle callbackHandle)
    {
        _handle = handle;
        _callbackHandle = callbackHandle;
    }

    /// <summary>Loads an ANARI implementation library, e.g. "helide".</summary>
    public static ANARILibrary Load(string name, ANARIStatusCallback? statusCallback = null)
    {
        GCHandle callbackHandle = default;
        delegate* unmanaged[Cdecl]<void*, Device*, Interop.Object*, ANARIDataType, int, int, sbyte*, void> fp = null;
        void* userPtr = null;

        if (statusCallback != null)
        {
            callbackHandle = GCHandle.Alloc(statusCallback);
            fp = &StatusCallbackThunk.Invoke;
            userPtr = (void*)GCHandle.ToIntPtr(callbackHandle);
        }

        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            var library = ANARI.anariLoadLibrary((sbyte*)n, fp, userPtr);
            if (library == null)
            {
                if (callbackHandle.IsAllocated)
                    callbackHandle.Free();
                throw new InvalidOperationException($"Failed to load ANARI library '{name}'.");
            }
            return new ANARILibrary((IntPtr)library, callbackHandle);
        }
    }

    public void LoadModule(string name)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariLoadModule(L, (sbyte*)n);
    }

    public void UnloadModule(string name)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariUnloadModule(L, (sbyte*)n);
    }

    public string[] GetDeviceSubtypes() =>
        NativeHelpers.ReadStringList(ANARI.anariGetDeviceSubtypes(L));

    public string[] GetDeviceExtensions(string deviceSubtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(deviceSubtype))
            return NativeHelpers.ReadStringList(ANARI.anariGetDeviceExtensions(L, (sbyte*)n));
    }

    public ANARIDevice NewDevice(string type = "default")
    {
        fixed (byte* n = NativeHelpers.ToUtf8(type))
            return new ANARIDevice((IntPtr)ANARI.anariNewDevice(L, (sbyte*)n));
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            ANARI.anariUnloadLibrary(L);
            _handle = IntPtr.Zero;
        }
        if (_callbackHandle.IsAllocated)
            _callbackHandle.Free();
    }
}

