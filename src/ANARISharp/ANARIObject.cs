// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>
/// Base class for all ANARI object handles. Mirrors the parameter/lifetime
/// functions of anari_cpp (setParameter, commitParameters, release, ...).
/// </summary>
public unsafe class ANARIObject : IDisposable
{
    private readonly ANARIDevice? _device;

    // Native device handle captured at construction. Child objects retain it so
    // anariRelease stays valid even if the device wrapper is disposed first.
    private readonly IntPtr _deviceHandle;

    /// <summary>Raw native ANARIObject handle.</summary>
    public IntPtr Handle { get; private set; }

    /// <summary>The device this object belongs to.</summary>
    public ANARIDevice Device => _device ?? (ANARIDevice)this;

    /// <summary>The ANARI object type of this handle (e.g. ANARI_CAMERA).</summary>
    public virtual ANARIDataType DataType => ANARIDataType.ANARI_OBJECT;

    internal ANARIObject(ANARIDevice? device, IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create {GetType().Name} (native call returned NULL).");
        _device = device;
        _deviceHandle = device?.Handle ?? handle;
        Handle = handle;
        if (device != null)
            ANARI.anariRetain((Device*)_deviceHandle, (Interop.Object*)_deviceHandle);
    }

    internal Device* DevicePtr => (Device*)_deviceHandle;
    internal Interop.Object* ObjectPtr => (Interop.Object*)Handle;

    // Parameters //////////////////////////////////////////////////////////////

    /// <summary>Sets a parameter whose ANARI type is inferred from <typeparamref name="T"/>.</summary>
    public void SetParameter<T>(string name, T value) where T : unmanaged
    {
        var type = ANARITypeFor<T>.Value;
        if (type == ANARIDataType.ANARI_UNKNOWN)
            throw new ArgumentException($"No ANARI data type mapping exists for '{typeof(T)}'.");
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, type, &value);
    }

    /// <summary>Sets an ANARI_STRING parameter.</summary>
    public void SetParameter(string name, string value)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
        fixed (byte* v = NativeHelpers.ToUtf8(value))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, ANARIDataType.ANARI_STRING, v);
    }

    /// <summary>Sets an ANARI_BOOL parameter (stored as uint32, as in anari_cpp).</summary>
    public void SetParameter(string name, bool value)
    {
        uint b = value ? 1u : 0u;
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, ANARIDataType.ANARI_BOOL, &b);
    }

    /// <summary>Sets an object-typed parameter (camera, geometry, array, ...).</summary>
    public void SetParameter(string name, ANARIObject value)
    {
        var handle = value.Handle;
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, value.DataType, &handle);
    }

    /// <summary>Sets a parameter with an explicit ANARI type from a raw pointer.</summary>
    public void SetParameter(string name, ANARIDataType type, void* value)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, type, value);
    }

    /// <summary>Sets a parameter with an explicit ANARI type (anari::setParameterAs).</summary>
    public void SetParameterAs<T>(string name, ANARIDataType type, T value) where T : unmanaged
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariSetParameter(DevicePtr, ObjectPtr, (sbyte*)n, type, &value);
    }

    /// <summary>Sets an object parameter then releases the wrapper's reference (anari::setAndReleaseParameter).</summary>
    public void SetAndReleaseParameter(string name, ANARIObject value)
    {
        SetParameter(name, value);
        value.Dispose();
    }

    // Directly mapped array parameters ////////////////////////////////////////

    /// <summary>Sets a 1D array parameter from a span; element type inferred from <typeparamref name="T"/>.</summary>
    public void SetParameterArray1D<T>(string name, ReadOnlySpan<T> data) where T : unmanaged
    {
        fixed (T* src = data)
            SetParameterArray1D(name, ANARITypeFor<T>.Value, src, sizeof(T), (ulong)data.Length);
    }

    /// <summary>Sets a 1D array parameter of object handles (e.g. world "surface").</summary>
    public void SetParameterArray1D(string name, ReadOnlySpan<ANARIObject> objects)
    {
        if (objects.IsEmpty)
            throw new ArgumentException("Object array must not be empty.", nameof(objects));

        var handles = new IntPtr[objects.Length];
        for (int i = 0; i < objects.Length; i++)
            handles[i] = objects[i].Handle;

        fixed (IntPtr* src = handles)
            SetParameterArray1D(name, objects[0].DataType, src, IntPtr.Size, (ulong)handles.Length);
    }

    /// <summary>Sets a 1D array parameter from raw memory with an explicit element type.</summary>
    public void SetParameterArray1D(string name, ANARIDataType type, void* data, int elementSize, ulong numElements1)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            ulong stride = 0;
            void* dst = ANARI.anariMapParameterArray1D(DevicePtr, ObjectPtr, (sbyte*)n, type, numElements1, &stride);
            if (dst == null)
                return;
            CopyStrided(dst, data, elementSize, stride, numElements1);
            ANARI.anariUnmapParameterArray(DevicePtr, ObjectPtr, (sbyte*)n);
        }
    }

    /// <summary>Sets a 2D array parameter from a span; element type inferred from <typeparamref name="T"/>.</summary>
    public void SetParameterArray2D<T>(string name, ReadOnlySpan<T> data, ulong numElements1, ulong numElements2)
        where T : unmanaged
    {
        if ((ulong)data.Length < numElements1 * numElements2)
            throw new ArgumentException("Span is smaller than numElements1 * numElements2.", nameof(data));

        fixed (T* src = data)
        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            ulong stride = 0;
            void* dst = ANARI.anariMapParameterArray2D(DevicePtr, ObjectPtr, (sbyte*)n,
                ANARITypeFor<T>.Value, numElements1, numElements2, &stride);
            if (dst == null)
                return;
            CopyStrided(dst, src, sizeof(T), stride, numElements1 * numElements2);
            ANARI.anariUnmapParameterArray(DevicePtr, ObjectPtr, (sbyte*)n);
        }
    }

    /// <summary>Sets a 3D array parameter from a span; element type inferred from <typeparamref name="T"/>.</summary>
    public void SetParameterArray3D<T>(string name, ReadOnlySpan<T> data,
        ulong numElements1, ulong numElements2, ulong numElements3) where T : unmanaged
    {
        if ((ulong)data.Length < numElements1 * numElements2 * numElements3)
            throw new ArgumentException("Span is smaller than the requested element count.", nameof(data));

        fixed (T* src = data)
        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            ulong stride = 0;
            void* dst = ANARI.anariMapParameterArray3D(DevicePtr, ObjectPtr, (sbyte*)n,
                ANARITypeFor<T>.Value, numElements1, numElements2, numElements3, &stride);
            if (dst == null)
                return;
            CopyStrided(dst, src, sizeof(T), stride, numElements1 * numElements2 * numElements3);
            ANARI.anariUnmapParameterArray(DevicePtr, ObjectPtr, (sbyte*)n);
        }
    }

    private static void CopyStrided(void* dst, void* src, int elementSize, ulong strideOut, ulong count)
    {
        if (strideOut == 0 || strideOut == (ulong)elementSize)
        {
            Buffer.MemoryCopy(src, dst, (ulong)elementSize * count, (ulong)elementSize * count);
        }
        else
        {
            byte* s = (byte*)src;
            byte* d = (byte*)dst;
            for (ulong i = 0; i < count; i++)
                Buffer.MemoryCopy(s + i * (ulong)elementSize, d + i * strideOut, elementSize, elementSize);
        }
    }

    public void UnsetParameter(string name)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
            ANARI.anariUnsetParameter(DevicePtr, ObjectPtr, (sbyte*)n);
    }

    public void UnsetAllParameters() =>
        ANARI.anariUnsetAllParameters(DevicePtr, ObjectPtr);

    public void CommitParameters() =>
        ANARI.anariCommitParameters(DevicePtr, ObjectPtr);

    // Properties //////////////////////////////////////////////////////////////

    /// <summary>Queries an object property (anari::getProperty).</summary>
    public bool GetProperty<T>(string name, out T value, bool wait = true) where T : unmanaged
    {
        var type = ANARITypeFor<T>.Value;
        if (type == ANARIDataType.ANARI_UNKNOWN)
            throw new ArgumentException($"No ANARI data type mapping exists for '{typeof(T)}'.");

        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            T v = default;
            int ok = ANARI.anariGetProperty(DevicePtr, ObjectPtr, (sbyte*)n, type,
                &v, (ulong)sizeof(T), wait ? 1u : 0u);
            value = v;
            return ok != 0;
        }
    }

    /// <summary>Queries a bool property (transported as ANARI_BOOL/uint32, as in anari_cpp).</summary>
    public bool GetProperty(string name, out bool value, bool wait = true)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(name))
        {
            uint v = 0;
            int ok = ANARI.anariGetProperty(DevicePtr, ObjectPtr, (sbyte*)n,
                ANARIDataType.ANARI_BOOL, &v, sizeof(uint), wait ? 1u : 0u);
            value = v != 0;
            return ok != 0;
        }
    }

    // Lifetime ////////////////////////////////////////////////////////////////

    /// <summary>Increments the native reference count (anariRetain).</summary>
    public void Retain() => ANARI.anariRetain(DevicePtr, ObjectPtr);

    /// <summary>Releases the native handle (anariRelease). Idempotent.</summary>
    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
        {
            ANARI.anariRelease(DevicePtr, ObjectPtr);
            Handle = IntPtr.Zero;
            // Drop the device reference taken in the constructor.
            if (_device != null)
                ANARI.anariRelease((Device*)_deviceHandle, (Interop.Object*)_deviceHandle);
        }
        GC.SuppressFinalize(this);
    }
}

