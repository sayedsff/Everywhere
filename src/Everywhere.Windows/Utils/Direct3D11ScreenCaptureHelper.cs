using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Win32.Foundation;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Everywhere.Windows.Services;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using WinRT;

namespace Everywhere.Windows.Utils;

public class Direct3D11ScreenCaptureHelper
{
    private readonly StrategyBasedComWrappers comWrappers = new();
    private readonly Interop.IGraphicsCaptureItemInterop interop;

    public Direct3D11ScreenCaptureHelper()
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        Marshal.QueryInterface(factory.ThisPtr, typeof(Interop.IGraphicsCaptureItemInterop).GUID, out var pInterop);
        interop = (Interop.IGraphicsCaptureItemInterop)comWrappers.GetOrCreateObjectForComInstance(pInterop, CreateObjectFlags.None);
    }

    public async Task<Bitmap> CaptureAsync(nint hWnd, PixelRect relativeRect)
    {
        using var device = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var pD3d11Device).ThrowOnFailure();
        using var direct3dDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pD3d11Device);

        var pItem = interop.CreateForWindow(hWnd, new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"));
        var item = GraphicsCaptureItem.FromAbi(pItem);
        var size = item.Size;

        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            direct3dDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            size);
        var tcs = new TaskCompletionSource<Bitmap>();
        framePool.FrameArrived += (f, _) => tcs.TrySetResult(ToBitmap(f.TryGetNextFrame(), relativeRect));

        using var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.StartCapture();
        return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(500));
    }

    private static Bitmap ToBitmap(Direct3D11CaptureFrame frame, PixelRect relativeRect)
    {
        using var capturedTexture = CreateTexture2D(frame.Surface);

        var device = capturedTexture.Device;
        var description = capturedTexture.Description;
        description.Width = (uint)relativeRect.Width;
        description.Height = (uint)relativeRect.Height;
        description.CPUAccessFlags = CpuAccessFlags.Read;
        description.BindFlags = BindFlags.None;
        description.Usage = ResourceUsage.Staging;
        description.MiscFlags = ResourceOptionFlags.None;
        using var stagingTexture = device.CreateTexture2D(description);

        device.ImmediateContext.CopySubresourceRegion(
            stagingTexture,
            0,
            0,
            0,
            0,
            capturedTexture,
            0,
            new Box(relativeRect.X, relativeRect.Y, 0, relativeRect.Right, relativeRect.Bottom, 1));

        var mappedSource = device.ImmediateContext.Map(stagingTexture, 0);
        try
        {
            var stagingDescription = stagingTexture.Description;
            return new Bitmap(
                PixelFormat.Bgra8888,
                AlphaFormat.Premul,
                mappedSource.DataPointer,
                new PixelSize((int)stagingDescription.Width, (int)stagingDescription.Height),
                new Vector(96d, 96d),
                (int)mappedSource.RowPitch
            );
        }
        finally
        {
            device.ImmediateContext.Unmap(stagingTexture, 0);
        }
    }

    private static ID3D11Texture2D CreateTexture2D(IDirect3DSurface surface)
    {
        using var access = new IDirect3DDxgiInterfaceAccess(Marshal.GetIUnknownForObject(surface));
        return access.GetInterface<ID3D11Texture2D>();
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern HRESULT CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}