// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>ANARICamera wrapper.</summary>
public sealed class ANARICamera : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_CAMERA;
    internal ANARICamera(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIGeometry wrapper.</summary>
public sealed class ANARIGeometry : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_GEOMETRY;
    internal ANARIGeometry(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIGroup wrapper.</summary>
public sealed class ANARIGroup : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_GROUP;
    internal ANARIGroup(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIInstance wrapper.</summary>
public sealed class ANARIInstance : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_INSTANCE;
    internal ANARIInstance(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARILight wrapper.</summary>
public sealed class ANARILight : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_LIGHT;
    internal ANARILight(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIMaterial wrapper.</summary>
public sealed class ANARIMaterial : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_MATERIAL;
    internal ANARIMaterial(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIRenderer wrapper.</summary>
public sealed class ANARIRenderer : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_RENDERER;
    internal ANARIRenderer(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARISampler wrapper.</summary>
public sealed class ANARISampler : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_SAMPLER;
    internal ANARISampler(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARISpatialField wrapper.</summary>
public sealed class ANARISpatialField : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_SPATIAL_FIELD;
    internal ANARISpatialField(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARISurface wrapper.</summary>
public sealed class ANARISurface : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_SURFACE;
    internal ANARISurface(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIVolume wrapper.</summary>
public sealed class ANARIVolume : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_VOLUME;
    internal ANARIVolume(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

/// <summary>ANARIWorld wrapper.</summary>
public sealed class ANARIWorld : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_WORLD;
    internal ANARIWorld(ANARIDevice device, IntPtr handle) : base(device, handle) { }
}

