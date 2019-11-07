using PdfClown.Documents;
using PdfClown.Documents.Contents;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace PdfClown.Viewer
{
    public class PdfPicture : IDisposable
    {
        private static Task task;
        private SKPicture picture;
        public SKMatrix Matrix = SKMatrix.MakeIdentity();
        public SKMatrix InitialMatrix;

        public PdfPicture()
        {
        }

        public PageAnnotations Annotations { get; private set; }

        public SKPicture GetPicture(SKCanvasView canvasView)
        {
            if (picture == null)
            {
                if (task == null || task.IsCompleted)
                {
                    task = new Task(() => Paint(canvasView));
                    task.Start();
                }
            }
            return picture;
        }

        private void Paint(SKCanvasView canvasView)
        {
            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(SKRect.Create(SKPoint.Empty, Size)))
            {
                try
                {
                    Page.Render(canvas, Size);
                }
                catch (Exception ex)
                {
                    using (var paint = new SKPaint { Color = SKColors.DarkRed })
                    {
                        canvas.Save();
                        if (canvas.TotalMatrix.ScaleY < 0)
                        {
                            var matrix = SKMatrix.MakeScale(1, -1);
                            canvas.Concat(ref matrix);
                        }
                        canvas.DrawText(ex.Message, 0, 0, paint);
                        canvas.Restore();
                    }
                }
                picture = recorder.EndRecording();
                Device.BeginInvokeOnMainThread(() => canvasView.InvalidateSurface());
            }
            InitialMatrix = ContentScanner.GraphicsState.GetInitialMatrix(Size, Page);
            Annotations = Page.Annotations;
        }

        public PdfClown.Documents.Page Page { get; set; }
        public SKSize Size { get; set; }
        public SKRect Bounds => SKRect.Create(
            Matrix.TransX,
            Matrix.TransY,
            Size.Width * Matrix.ScaleX,
            Size.Height * Matrix.ScaleY);

        public void Dispose()
        {
            picture?.Dispose();
        }
    }
}
