using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.UI.Xaml.Hosting;
using Xamarin.Forms;
using Xamarin.Forms.Platform.UWP;

namespace ImageFromXamarinUI
{
    public static partial class VisualElementExtension
    {
        static async Task<Stream> PlatformCaptureImageAsync(VisualElement view, Xamarin.Forms.Color backgroundColor)
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new NotSupportedException("Screen capture permission is not enabled or capture is not supported on this platform");
            }

            try
            {
                var taskCompletionSource = new TaskCompletionSource<Stream>();

                StartCapture(view, taskCompletionSource);

                return await taskCompletionSource.Task;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }

            return null;
        }

        private static void StartCapture(VisualElement view, TaskCompletionSource<Stream> taskCompletionSource)
        {
            using var canvasDevice = new CanvasDevice();

            using var renderer = view.GetOrCreateRenderer();

            var nativeElement = renderer.GetNativeElement();

            using var nativeVisual = ElementCompositionPreview.GetElementVisual(nativeElement);

            var item = GraphicsCaptureItem.CreateFromVisual(nativeVisual);

            //var size = item.Size;
            var size = new Windows.Graphics.SizeInt32 { Width = 100, Height = 100 };

            using var framePool = Direct3D11CaptureFramePool.Create(
               canvasDevice, // D3D device
               DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
               2, // Number of frames
               size); // Size of the buffers

            TypedEventHandler<Direct3D11CaptureFramePool, object> action = new(async (f, f2) => await Callback(f, f2, canvasDevice, taskCompletionSource));

            framePool.FrameArrived += action;

            using var captureSession = framePool.CreateCaptureSession(item);
            captureSession.StartCapture();

            framePool.FrameArrived -= action;
        }

        private static async Task Callback(Direct3D11CaptureFramePool framePool, object args, CanvasDevice canvasDevice, TaskCompletionSource<Stream> source)
        {
            using var frame = framePool.TryGetNextFrame();

            if (frame == null)
            {
                return;
            }

            // Take the D3D11 surface and draw it into a  
            // Composition surface.

            // Convert our D3D11 surface into a Win2D object.
            CanvasBitmap canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(
            canvasDevice,
                frame.Surface);

            var ms = new MemoryStream();
            var randomAccessStream = ms.AsRandomAccessStream();
            await canvasBitmap.SaveAsync(randomAccessStream, CanvasBitmapFileFormat.Png, 1f);
            source.SetResult(randomAccessStream.AsStream());
        }
    }
}
