using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.Scanner;
using PdfClown.Documents.Interaction.Annotations;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfClown.Viewer
{
    public class PdfPagePicture : IDisposable
    {
        private static Task task;
        public static bool IsPaintComplete => (task?.IsCompleted ?? true);

        private SKPicture picture;
        public SKMatrix Matrix = SKMatrix.MakeIdentity();
        public SKMatrix InitialMatrix;
        private Page page;

        public PdfPagePicture()
        {
        }

        public List<Annotation> Annotations { get; private set; }

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
                Xamarin.Forms.Device.BeginInvokeOnMainThread(() => canvasView.InvalidateSurface());
            }
            InitialMatrix = GraphicsState.GetInitialMatrix(Page, Size);
        }

        public Page Page
        {
            get => page;
            set
            {
                page = value;
                Annotations = new List<Annotation>();
                foreach (var annotation in Page.Annotations)
                {
                    if (annotation == null)
                        continue;
                    if (!Annotations.Contains(annotation))
                    {
                        Annotations.Add(annotation);
                    }
                    if (annotation is Popup popup)
                    {
                        if (popup.Markup != null && !Annotations.Contains(popup.Markup))
                            Annotations.Add(popup.Markup);
                    }
                }
            }
        }

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
