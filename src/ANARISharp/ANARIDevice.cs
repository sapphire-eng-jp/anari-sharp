// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>Wraps an ANARIDevice handle and its object factory functions (anariNew*).</summary>
public sealed unsafe class ANARIDevice : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_DEVICE;

    internal ANARIDevice(IntPtr handle) : base(null, handle) { }

    private Device* D => (Device*)Handle;

    // Object factories ////////////////////////////////////////////////////////

    public ANARICamera NewCamera(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARICamera(this, (IntPtr)ANARI.anariNewCamera(D, (sbyte*)n));
    }

    public ANARIGeometry NewGeometry(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARIGeometry(this, (IntPtr)ANARI.anariNewGeometry(D, (sbyte*)n));
    }

    public ANARILight NewLight(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARILight(this, (IntPtr)ANARI.anariNewLight(D, (sbyte*)n));
    }

    public ANARIMaterial NewMaterial(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARIMaterial(this, (IntPtr)ANARI.anariNewMaterial(D, (sbyte*)n));
    }

    public ANARISampler NewSampler(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARISampler(this, (IntPtr)ANARI.anariNewSampler(D, (sbyte*)n));
    }

    public ANARISpatialField NewSpatialField(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARISpatialField(this, (IntPtr)ANARI.anariNewSpatialField(D, (sbyte*)n));
    }

    public ANARIVolume NewVolume(string subtype)
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARIVolume(this, (IntPtr)ANARI.anariNewVolume(D, (sbyte*)n));
    }

    public ANARIInstance NewInstance(string subtype = "transform")
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARIInstance(this, (IntPtr)ANARI.anariNewInstance(D, (sbyte*)n));
    }

    public ANARIRenderer NewRenderer(string subtype = "default")
    {
        fixed (byte* n = NativeHelpers.ToUtf8(subtype))
            return new ANARIRenderer(this, (IntPtr)ANARI.anariNewRenderer(D, (sbyte*)n));
    }

    public ANARISurface NewSurface() =>
        new(this, (IntPtr)ANARI.anariNewSurface(D));

    public ANARIGroup NewGroup() =>
        new(this, (IntPtr)ANARI.anariNewGroup(D));

    public ANARIWorld NewWorld() =>
        new(this, (IntPtr)ANARI.anariNewWorld(D));

    public ANARIFrame NewFrame() =>
        new(this, (IntPtr)ANARI.anariNewFrame(D));

    /// <summary>Creates an object of an implementation-defined type (anariNewObject).</summary>
    public ANARIObject NewObject(string objectType, string subtype)
    {
        fixed (byte* t = NativeHelpers.ToUtf8(objectType))
        fixed (byte* s = NativeHelpers.ToUtf8(subtype))
            return new ANARIObject(this, (IntPtr)ANARI.anariNewObject(D, (sbyte*)t, (sbyte*)s));
    }

    // Arrays //////////////////////////////////////////////////////////////////

    /// <summary>Creates a device-managed 1D array (no application memory).</summary>
    public ANARIArray1D NewArray1D(ANARIDataType elementType, ulong numElements1)
    {
        var handle = (IntPtr)ANARI.anariNewArray1D(D, null, null, null, elementType, numElements1);
        return new ANARIArray1D(this, handle, elementType, numElements1);
    }

    /// <summary>Creates a 1D array and copies <paramref name="data"/> into it.</summary>
    public ANARIArray1D NewArray1D<T>(ReadOnlySpan<T> data) where T : unmanaged
    {
        var array = NewArray1D(ANARITypeFor<T>.Value, (ulong)data.Length);
        data.CopyTo(array.Map<T>());
        array.Unmap();
        return array;
    }

    /// <summary>Creates a 1D array of object handles and copies them into it.</summary>
    public ANARIArray1D NewArray1D(ReadOnlySpan<ANARIObject> objects)
    {
        if (objects.IsEmpty)
            throw new ArgumentException("Object array must not be empty.", nameof(objects));

        var array = NewArray1D(objects[0].DataType, (ulong)objects.Length);
        var dst = array.Map<IntPtr>();
        for (int i = 0; i < objects.Length; i++)
            dst[i] = objects[i].Handle;
        array.Unmap();
        return array;
    }

    /// <summary>Creates a 1D array over caller-owned memory (zero copy, no deleter).</summary>
    public ANARIArray1D NewArray1D(void* appMemory, ANARIDataType elementType, ulong numElements1)
    {
        var handle = (IntPtr)ANARI.anariNewArray1D(D, appMemory, null, null, elementType, numElements1);
        return new ANARIArray1D(this, handle, elementType, numElements1);
    }

    /// <summary>Creates a device-managed 2D array (no application memory).</summary>
    public ANARIArray2D NewArray2D(ANARIDataType elementType, ulong numElements1, ulong numElements2)
    {
        var handle = (IntPtr)ANARI.anariNewArray2D(D, null, null, null, elementType, numElements1, numElements2);
        return new ANARIArray2D(this, handle, elementType, numElements1, numElements2);
    }

    /// <summary>Creates a 2D array and copies <paramref name="data"/> into it.</summary>
    public ANARIArray2D NewArray2D<T>(ReadOnlySpan<T> data, ulong numElements1, ulong numElements2)
        where T : unmanaged
    {
        if ((ulong)data.Length < numElements1 * numElements2)
            throw new ArgumentException("Span is smaller than numElements1 * numElements2.", nameof(data));

        var array = NewArray2D(ANARITypeFor<T>.Value, numElements1, numElements2);
        data.CopyTo(array.Map<T>());
        array.Unmap();
        return array;
    }

    /// <summary>Creates a device-managed 3D array (no application memory).</summary>
    public ANARIArray3D NewArray3D(ANARIDataType elementType, ulong numElements1, ulong numElements2, ulong numElements3)
    {
        var handle = (IntPtr)ANARI.anariNewArray3D(D, null, null, null, elementType, numElements1, numElements2, numElements3);
        return new ANARIArray3D(this, handle, elementType, numElements1, numElements2, numElements3);
    }

    /// <summary>Creates a 3D array and copies <paramref name="data"/> into it.</summary>
    public ANARIArray3D NewArray3D<T>(ReadOnlySpan<T> data, ulong numElements1, ulong numElements2, ulong numElements3)
        where T : unmanaged
    {
        if ((ulong)data.Length < numElements1 * numElements2 * numElements3)
            throw new ArgumentException("Span is smaller than the requested element count.", nameof(data));

        var array = NewArray3D(ANARITypeFor<T>.Value, numElements1, numElements2, numElements3);
        data.CopyTo(array.Map<T>());
        array.Unmap();
        return array;
    }

    // Introspection ///////////////////////////////////////////////////////////

    public string[] GetObjectSubtypes(ANARIDataType objectType) =>
        NativeHelpers.ReadStringList(ANARI.anariGetObjectSubtypes(D, objectType));
}

