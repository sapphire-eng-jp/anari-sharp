// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;

namespace ANARISharp;

internal static unsafe class NativeHelpers
{
    /// <summary>Encodes a string as NUL-terminated UTF-8 suitable for pinning.</summary>
    public static byte[] ToUtf8(string s)
    {
        var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
        return bytes;
    }

    /// <summary>Reads a NULL-terminated list of C strings (const char**).</summary>
    public static string[] ReadStringList(sbyte** list)
    {
        if (list == null)
            return Array.Empty<string>();

        var result = new List<string>();
        for (sbyte** p = list; *p != null; p++)
            result.Add(Marshal.PtrToStringUTF8((IntPtr)(*p)) ?? string.Empty);
        return result.ToArray();
    }
}

