// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>Base class for ANARIArray handles (anariMapArray/anariUnmapArray).</summary>
public unsafe class ANARIArray : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_ARRAY;

    /// <summary>The element data type this array was created with.</summary>
    public ANARIDataType ElementType { get; }

    /// <summary>Total number of elements across all dimensions.</summary>
    public ulong ElementCount { get; }

    internal ANARIArray(ANARIDevice device, IntPtr handle, ANARIDataType elementType, ulong elementCount)
        : base(device, handle)
    {
        ElementType = elementType;
        ElementCount = elementCount;
    }

    private Interop.Array* A => (Interop.Array*)Handle;

    /// <summary>Maps the array memory for writing (anariMapArray).</summary>
    public void* MapRaw() => ANARI.anariMapArray(DevicePtr, A);

    /// <summary>Maps the array memory as a typed span.</summary>
    public Span<T> Map<T>() where T : unmanaged =>
        new(ANARI.anariMapArray(DevicePtr, A), checked((int)ElementCount));

    /// <summary>Unmaps previously mapped array memory (anariUnmapArray).</summary>
    public void Unmap() => ANARI.anariUnmapArray(DevicePtr, A);
}

/// <summary>ANARIArray1D wrapper.</summary>
public sealed class ANARIArray1D : ANARIArray
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_ARRAY1D;

    public ulong Length { get; }

    internal ANARIArray1D(ANARIDevice device, IntPtr handle, ANARIDataType elementType, ulong numElements1)
        : base(device, handle, elementType, numElements1)
    {
        Length = numElements1;
    }
}

/// <summary>ANARIArray2D wrapper.</summary>
public sealed class ANARIArray2D : ANARIArray
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_ARRAY2D;

    public ulong Width { get; }
    public ulong Height { get; }

    internal ANARIArray2D(ANARIDevice device, IntPtr handle, ANARIDataType elementType,
        ulong numElements1, ulong numElements2)
        : base(device, handle, elementType, numElements1 * numElements2)
    {
        Width = numElements1;
        Height = numElements2;
    }
}

/// <summary>ANARIArray3D wrapper.</summary>
public sealed class ANARIArray3D : ANARIArray
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_ARRAY3D;

    public ulong Width { get; }
    public ulong Height { get; }
    public ulong Depth { get; }

    internal ANARIArray3D(ANARIDevice device, IntPtr handle, ANARIDataType elementType,
        ulong numElements1, ulong numElements2, ulong numElements3)
        : base(device, handle, elementType, numElements1 * numElements2 * numElements3)
    {
        Width = numElements1;
        Height = numElements2;
        Depth = numElements3;
    }
}

