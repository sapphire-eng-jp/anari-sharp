// Copyright 2026 Sapphire ENG. Co., Ltd.
// SPDX-License-Identifier: Apache-2.0

using ANARISharp.Interop;

namespace ANARISharp;

/// <summary>Result of mapping a frame channel (anari::MappedFrameData&lt;T&gt;).</summary>
public readonly ref struct MappedFrameData<T> where T : unmanaged
{
    public uint Width { get; }
    public uint Height { get; }
    public ANARIDataType PixelType { get; }
    public ReadOnlySpan<T> Data { get; }

    internal MappedFrameData(uint width, uint height, ANARIDataType pixelType, ReadOnlySpan<T> data)
    {
        Width = width;
        Height = height;
        PixelType = pixelType;
        Data = data;
    }
}

/// <summary>ANARIFrame wrapper (render, wait, map channels).</summary>
public sealed unsafe class ANARIFrame : ANARIObject
{
    public override ANARIDataType DataType => ANARIDataType.ANARI_FRAME;

    internal ANARIFrame(ANARIDevice device, IntPtr handle) : base(device, handle) { }

    private Frame* F => (Frame*)Handle;

    /// <summary>Starts rendering the frame (anariRenderFrame).</summary>
    public void Render() => ANARI.anariRenderFrame(DevicePtr, F);

    /// <summary>True if the frame has finished rendering (anariFrameReady with ANARI_NO_WAIT).</summary>
    public bool IsReady => ANARI.anariFrameReady(DevicePtr, F, (uint)ANARIWaitMask.ANARI_NO_WAIT) != 0;

    /// <summary>Blocks until the frame has finished rendering (anariFrameReady with ANARI_WAIT).</summary>
    public void Wait() => ANARI.anariFrameReady(DevicePtr, F, (uint)ANARIWaitMask.ANARI_WAIT);

    /// <summary>Signals that the current frame result may be discarded (anariDiscardFrame).</summary>
    public void Discard() => ANARI.anariDiscardFrame(DevicePtr, F);

    /// <summary>Maps a frame channel, e.g. "channel.color" (anariMapFrame).</summary>
    public MappedFrameData<T> Map<T>(string channel) where T : unmanaged
    {
        fixed (byte* c = NativeHelpers.ToUtf8(channel))
        {
            uint width = 0, height = 0;
            ANARIDataType pixelType = ANARIDataType.ANARI_UNKNOWN;
            void* data = ANARI.anariMapFrame(DevicePtr, F, (sbyte*)c, &width, &height, &pixelType);

            int length = 0;
            if (data != null)
            {
                long bytes = (long)width * height * ANARIDataTypeUtil.SizeOf(pixelType);
                length = checked((int)(bytes / sizeof(T)));
            }
            return new MappedFrameData<T>(width, height, pixelType, new ReadOnlySpan<T>(data, length));
        }
    }

    /// <summary>Unmaps a previously mapped frame channel (anariUnmapFrame).</summary>
    public void Unmap(string channel)
    {
        fixed (byte* c = NativeHelpers.ToUtf8(channel))
            ANARI.anariUnmapFrame(DevicePtr, F, (sbyte*)c);
    }
}

