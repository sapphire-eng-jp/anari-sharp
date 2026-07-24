' Copyright 2026 Sapphire ENG. Co., Ltd.
' SPDX-License-Identifier: Apache-2.0

' VB.NET port of the HelloANARI sample: renders a single triangle with the
' requested library (helide by default) and writes the result as PPM.
'
' VB-specific notes compared to the C# sample:
'  - Span-taking generic methods need explicit type arguments, e.g.
'    SetParameterArray1D(Of Vector3)(...): VB does not infer T through the
'    array -> ReadOnlySpan conversion, so without the type argument overload
'    resolution picks the ReadOnlySpan(Of ANARIObject) overload and fails.
'  - ANARIFrame.Map returns a ref struct; copy the pixels out with
'    mapped.Data.ToArray() before unmapping.
'  - The enum types (ANARIDataType, ...) live in ANARISharp.Interop.

Imports System.Numerics
Imports ANARISharp
Imports ANARISharp.Interop

Module Program

    Sub Main(args As String())
        Const width As UInteger = 800
        Const height As UInteger = 600

        Dim libraryName = If(args.Length > 0, args(0), "helide")

        Using library = ANARILibrary.Load(libraryName, AddressOf OnStatus)
            Console.WriteLine($"Loaded library '{libraryName}' (devices: {String.Join(", ", library.GetDeviceSubtypes())})")

            Using device = library.NewDevice()
                device.CommitParameters()

                ' Geometry: one triangle with per-vertex colors.
                Dim positions As Vector3() = {
                    New Vector3(-1.0F, -1.0F, 3.0F),
                    New Vector3(1.0F, -1.0F, 3.0F),
                    New Vector3(0.0F, 1.0F, 3.0F)
                }
                Dim colors As Vector4() = {
                    New Vector4(1.0F, 0.0F, 0.0F, 1.0F),
                    New Vector4(0.0F, 1.0F, 0.0F, 1.0F),
                    New Vector4(0.0F, 0.0F, 1.0F, 1.0F)
                }

                Using geometry = device.NewGeometry("triangle"),
                      material = device.NewMaterial("matte"),
                      surface = device.NewSurface(),
                      world = device.NewWorld(),
                      camera = device.NewCamera("perspective"),
                      renderer = device.NewRenderer("default"),
                      frame = device.NewFrame()

                    geometry.SetParameterArray1D(Of Vector3)("vertex.position", positions)
                    geometry.SetParameterArray1D(Of Vector4)("vertex.color", colors)
                    geometry.CommitParameters()

                    material.SetParameter("color", "color") ' bind the vertex color attribute
                    material.CommitParameters()

                    surface.SetParameter("geometry", geometry)
                    surface.SetParameter("material", material)
                    surface.CommitParameters()

                    world.SetParameterArray1D("surface", New ANARIObject() {surface})
                    world.CommitParameters()

                    camera.SetParameter("aspect", CSng(width) / height)
                    camera.SetParameter("position", Vector3.Zero)
                    camera.SetParameter("direction", New Vector3(0.0F, 0.0F, 1.0F))
                    camera.SetParameter("up", New Vector3(0.0F, 1.0F, 0.0F))
                    camera.CommitParameters()

                    renderer.SetParameter("background", New Vector4(0.1F, 0.1F, 0.1F, 1.0F))
                    renderer.CommitParameters()

                    frame.SetParameter("size", New UVec2(width, height))
                    frame.SetParameter("channel.color", ANARIDataType.ANARI_UFIXED8_RGBA_SRGB)
                    frame.SetParameter("world", world)
                    frame.SetParameter("camera", camera)
                    frame.SetParameter("renderer", renderer)
                    frame.CommitParameters()

                    frame.Render()
                    frame.Wait()

                    Dim mapped = frame.Map(Of Byte)("channel.color")
                    Console.WriteLine($"Rendered {mapped.Width}x{mapped.Height} ({mapped.PixelType})")
                    Dim rgba = mapped.Data.ToArray()
                    frame.Unmap("channel.color")

                    WritePpm("hello_anari_vb.ppm", width, height, rgba)
                    Console.WriteLine($"Wrote {IO.Path.GetFullPath("hello_anari_vb.ppm")}")
                End Using
            End Using
        End Using
    End Sub

    Sub OnStatus(device As IntPtr, source As IntPtr, sourceType As ANARIDataType,
                 severity As ANARIStatusSeverity, code As ANARIStatusCode, message As String)
        Console.Error.WriteLine($"[{severity}] {message}")
    End Sub

    Sub WritePpm(path As String, w As UInteger, h As UInteger, rgba As Byte())
        Using f As New IO.FileStream(path, IO.FileMode.Create, IO.FileAccess.Write)
            Dim header = Text.Encoding.ASCII.GetBytes($"P6{vbLf}{w} {h}{vbLf}255{vbLf}")
            f.Write(header, 0, header.Length)

            ' PPM stores top-down RGB; the frame buffer is bottom-up RGBA.
            Dim row(CInt(w) * 3 - 1) As Byte
            For y As Long = CLng(h) - 1 To 0 Step -1
                Dim base0 As Integer = CInt(y * w * 4)
                For x As Integer = 0 To CInt(w) - 1
                    row(x * 3 + 0) = rgba(base0 + x * 4 + 0)
                    row(x * 3 + 1) = rgba(base0 + x * 4 + 1)
                    row(x * 3 + 2) = rgba(base0 + x * 4 + 2)
                Next
                f.Write(row, 0, row.Length)
            Next
        End Using
    End Sub

End Module
