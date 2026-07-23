// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

namespace ANARISharp;

/// <summary>Two-component signed integer vector (ANARI_INT32_VEC2).</summary>
public struct IVec2
{
    public int X, Y;
    public IVec2(int x, int y) { X = x; Y = y; }
}

/// <summary>Three-component signed integer vector (ANARI_INT32_VEC3).</summary>
public struct IVec3
{
    public int X, Y, Z;
    public IVec3(int x, int y, int z) { X = x; Y = y; Z = z; }
}

/// <summary>Four-component signed integer vector (ANARI_INT32_VEC4).</summary>
public struct IVec4
{
    public int X, Y, Z, W;
    public IVec4(int x, int y, int z, int w) { X = x; Y = y; Z = z; W = w; }
}

/// <summary>Two-component unsigned integer vector (ANARI_UINT32_VEC2).</summary>
public struct UVec2
{
    public uint X, Y;
    public UVec2(uint x, uint y) { X = x; Y = y; }
}

/// <summary>Three-component unsigned integer vector (ANARI_UINT32_VEC3).</summary>
public struct UVec3
{
    public uint X, Y, Z;
    public UVec3(uint x, uint y, uint z) { X = x; Y = y; Z = z; }
}

/// <summary>Four-component unsigned integer vector (ANARI_UINT32_VEC4).</summary>
public struct UVec4
{
    public uint X, Y, Z, W;
    public UVec4(uint x, uint y, uint z, uint w) { X = x; Y = y; Z = z; W = w; }
}

