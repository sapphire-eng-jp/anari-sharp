// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>
/// Compile-time mapping from a C# value type to its <see cref="ANARIDataType"/>,
/// mirroring anari::ANARITypeFor&lt;T&gt; from anari_cpp/Traits.h.
/// </summary>
public static class ANARITypeFor<T> where T : unmanaged
{
    public static readonly ANARIDataType Value = Compute();

    private static ANARIDataType Compute()
    {
        var t = typeof(T);

        if (t == typeof(sbyte)) return ANARIDataType.ANARI_INT8;
        if (t == typeof(byte)) return ANARIDataType.ANARI_UINT8;
        if (t == typeof(short)) return ANARIDataType.ANARI_INT16;
        if (t == typeof(ushort)) return ANARIDataType.ANARI_UINT16;
        if (t == typeof(int)) return ANARIDataType.ANARI_INT32;
        if (t == typeof(uint)) return ANARIDataType.ANARI_UINT32;
        if (t == typeof(long)) return ANARIDataType.ANARI_INT64;
        if (t == typeof(ulong)) return ANARIDataType.ANARI_UINT64;
        if (t == typeof(Half)) return ANARIDataType.ANARI_FLOAT16;
        if (t == typeof(float)) return ANARIDataType.ANARI_FLOAT32;
        if (t == typeof(double)) return ANARIDataType.ANARI_FLOAT64;

        if (t == typeof(Vector2)) return ANARIDataType.ANARI_FLOAT32_VEC2;
        if (t == typeof(Vector3)) return ANARIDataType.ANARI_FLOAT32_VEC3;
        if (t == typeof(Vector4)) return ANARIDataType.ANARI_FLOAT32_VEC4;
        if (t == typeof(Quaternion)) return ANARIDataType.ANARI_FLOAT32_QUAT_IJKW;
        if (t == typeof(Matrix4x4)) return ANARIDataType.ANARI_FLOAT32_MAT4;

        if (t == typeof(IVec2)) return ANARIDataType.ANARI_INT32_VEC2;
        if (t == typeof(IVec3)) return ANARIDataType.ANARI_INT32_VEC3;
        if (t == typeof(IVec4)) return ANARIDataType.ANARI_INT32_VEC4;
        if (t == typeof(UVec2)) return ANARIDataType.ANARI_UINT32_VEC2;
        if (t == typeof(UVec3)) return ANARIDataType.ANARI_UINT32_VEC3;
        if (t == typeof(UVec4)) return ANARIDataType.ANARI_UINT32_VEC4;

        if (t == typeof(ANARIDataType)) return ANARIDataType.ANARI_DATA_TYPE;

        return ANARIDataType.ANARI_UNKNOWN;
    }
}

/// <summary>Utility queries over <see cref="ANARIDataType"/> values (see anari_cpp/Traits.h and type_utility.h).</summary>
public static class ANARIDataTypeUtil
{
    public static bool IsObject(ANARIDataType type) =>
        type >= ANARIDataType.ANARI_OBJECT && type <= ANARIDataType.ANARI_WORLD;

    public static bool IsArray(ANARIDataType type) =>
        type >= ANARIDataType.ANARI_ARRAY && type <= ANARIDataType.ANARI_ARRAY3D;

    /// <summary>Size in bytes of one element of the given data type.</summary>
    public static int SizeOf(ANARIDataType type)
    {
        int v = (int)type;

        // scalar/vector numeric block: 16 families of 4 (base, vec2, vec3, vec4)
        if (v >= (int)ANARIDataType.ANARI_INT8 && v <= (int)ANARIDataType.ANARI_FLOAT64_VEC4)
        {
            int family = (v - 1000) / 4; // int8,uint8,int16,...,ufixed64,float16,float32,float64
            int components = (v - 1000) % 4 + 1;
            int elementSize = family switch
            {
                0 or 1 or 8 or 9 => 1,
                2 or 3 or 10 or 11 or 16 => 2,
                4 or 5 or 12 or 13 or 17 => 4,
                _ => 8,
            };
            return elementSize * components;
        }

        return type switch
        {
            ANARIDataType.ANARI_UNKNOWN => 4,
            ANARIDataType.ANARI_DATA_TYPE => 4,
            ANARIDataType.ANARI_BOOL => 1,

            ANARIDataType.ANARI_UFIXED8_R_SRGB => 1,
            ANARIDataType.ANARI_UFIXED8_RA_SRGB => 2,
            ANARIDataType.ANARI_UFIXED8_RGB_SRGB => 3,
            ANARIDataType.ANARI_UFIXED8_RGBA_SRGB => 4,

            ANARIDataType.ANARI_INT32_BOX1 => 8,
            ANARIDataType.ANARI_INT32_BOX2 => 16,
            ANARIDataType.ANARI_INT32_BOX3 => 24,
            ANARIDataType.ANARI_INT32_BOX4 => 32,
            ANARIDataType.ANARI_FLOAT32_BOX1 => 8,
            ANARIDataType.ANARI_FLOAT32_BOX2 => 16,
            ANARIDataType.ANARI_FLOAT32_BOX3 => 24,
            ANARIDataType.ANARI_FLOAT32_BOX4 => 32,
            ANARIDataType.ANARI_FLOAT64_BOX1 => 16,
            ANARIDataType.ANARI_FLOAT64_BOX2 => 32,
            ANARIDataType.ANARI_FLOAT64_BOX3 => 48,
            ANARIDataType.ANARI_FLOAT64_BOX4 => 64,
            ANARIDataType.ANARI_UINT64_REGION1 => 16,
            ANARIDataType.ANARI_UINT64_REGION2 => 32,
            ANARIDataType.ANARI_UINT64_REGION3 => 48,
            ANARIDataType.ANARI_UINT64_REGION4 => 64,

            ANARIDataType.ANARI_FLOAT32_MAT2 => 16,
            ANARIDataType.ANARI_FLOAT32_MAT3 => 36,
            ANARIDataType.ANARI_FLOAT32_MAT4 => 64,
            ANARIDataType.ANARI_FLOAT32_MAT2x3 => 24,
            ANARIDataType.ANARI_FLOAT32_MAT3x4 => 48,
            ANARIDataType.ANARI_FLOAT32_QUAT_IJKW => 16,

            // strings, lists, function pointers and object handles are pointer sized
            _ => IntPtr.Size,
        };
    }
}

