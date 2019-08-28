using org.pdfclown.documents;
using org.pdfclown.files;
using org.pdfclown.tools;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace PDFClown.Viewer
{
    public class SKPDFView : SKScrollView
    {
        public static readonly BindableProperty ScaleFactorProperty = BindableProperty.Create(nameof(ScaleFactorProperty), typeof(float), typeof(SKPDFView), 1F,
            propertyChanged: (bindable, oldValue, newValue) => ((SKPDFView)bindable).OnScaleFactorChanged((float)oldValue, (float)newValue));

        private SKMatrix currentMatrix;
        private List<SKPictureDetails> pictures = new List<SKPictureDetails>();
        private float scale = 1;
        private float indent = 10;
        private SKPoint pointerLocation;

        public SKPDFView()
        {
            PaintContent += OnPaintContent;
        }

        public float ScaleFactor
        {
            get => (float)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        public File File { get; private set; }
        public Document Document { get; private set; }
        public SKSize DocumentSize { get; private set; }
        public Pages Pages { get; private set; }

        private void OnScaleFactorChanged(float oldValue, float newValue)
        {
            scale = newValue;
            UpdateMaximums();
        }

        protected virtual void OnPaintContent(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Silver);
            canvas.Concat(ref currentMatrix);
            var area = SKRect.Create(0, 0, (float)Width, (float)Height);
            foreach (var pictureDetails in pictures)
            {
                if (currentMatrix.MapRect(pictureDetails.Bounds).IntersectsWith(area))
                {
                    var picture = pictureDetails.GetPicture();
                    if (picture != null)
                    {
                        canvas.DrawPicture(picture, ref pictureDetails.Matrix);
                    }
                }
            }
        }

        protected override void OnTouch(object sender, SKTouchEventArgs e)
        {
            base.OnTouch(sender, e);
            pointerLocation = e.Location;
        }

        public override void OnScrolled(int delta, KeyModifiers keyModifiers)
        {
            base.OnScrolled(delta, keyModifiers);
            if (keyModifiers == KeyModifiers.Ctrl)
            {
                var scaleStep = 0.03F * Math.Sign(delta);
                var newSclae = scale + scaleStep;
                if (newSclae > 0.03 && newSclae < 4)
                {
                    currentMatrix.TryInvert(out var oldMatrix);
                    var oldPointer = oldMatrix.MapPoint(pointerLocation);
                    ScaleFactor = newSclae;
                    var newPointerLocation = currentMatrix.MapPoint(oldPointer);
                    if (HorizontalScrollBarVisible)
                    {
                        HorizontalValue += newPointerLocation.X - pointerLocation.X;
                    }
                    if (VerticalScrollBarVisible)
                    {
                        VerticalValue += newPointerLocation.Y - pointerLocation.Y;
                    }

                }
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateCurrentMatrix();
        }

        public Task LoadAsync(System.IO.Stream stream)
        {
            return Task.Run(() => Load(stream));
        }

        public void Load(System.IO.Stream stream)
        {
            if (File != null)
            {
                File.Dispose();
            }
            File = new File(stream);
            Document = File.Document;
            LoadPages();
        }

        private SKSize LoadPages()
        {
            float totalWidth, totalHeight;
            Pages = Document.Pages;
            ClearPictures();
            totalWidth = 0F;
            totalHeight = 0F;
            foreach (var page in Pages)
            {
                totalHeight += indent;
                var box = page.RotatedBox;
                var imageSize = new SKSize(box.Width, box.Height);
                var details = new SKPictureDetails()
                {
                    Page = page,
                    Size = imageSize
                };
                details.Matrix.SetScaleTranslate(1, 1, indent, totalHeight);
                pictures.Add(details);
                if (imageSize.Width > totalWidth)
                    totalWidth = imageSize.Width;

                totalHeight += imageSize.Height;
            }
            DocumentSize = new SKSize(totalWidth, totalHeight);

            Device.BeginInvokeOnMainThread(() => UpdateMaximums());

            return DocumentSize;
        }

        private void UpdateMaximums()
        {
            UpdateCurrentMatrix();
            HorizontalMaximum = DocumentSize.Width * scale;
            VerticalMaximum = DocumentSize.Height * scale;
            InvalidateSurface();
        }

        private void UpdateCurrentMatrix()
        {
            var maximumWidth = DocumentSize.Width * scale;
            var maximumHeight = DocumentSize.Height * scale;
            var dx = 0F; var dy = 0F;
            if (maximumWidth < Width)
            {
                dx = (float)(Width - maximumWidth) / 2;
            }

            if (maximumHeight < Height)
            {
                dy = (float)(Height - maximumHeight) / 2;
            }
            currentMatrix.SetScaleTranslate(scale, scale, ((float)-HorizontalValue) + dx, ((float)-VerticalValue) + dy);
        }

        protected override void OnVerticalValueChanged(double oldValue, double newValue)
        {
            UpdateCurrentMatrix();
            base.OnVerticalValueChanged(oldValue, newValue);
        }

        protected override void OnHorizontalValueChanged(double oldValue, double newValue)
        {
            UpdateCurrentMatrix();
            base.OnHorizontalValueChanged(oldValue, newValue);
        }

        private void ClearPictures()
        {
            foreach (var picture in pictures)
            {
                picture.Dispose();
            }
            pictures.Clear();
        }
    }

    public class SKPictureDetails : IDisposable
    {
        SKPicture picture;
        public SKPictureDetails()
        {
        }

        public SKMatrix Matrix = SKMatrix.MakeIdentity();

        public SKPicture GetPicture()
        {
            if (picture == null)
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
                            var matrix = SKMatrix.MakeScale(1, -1);
                            canvas.Concat(ref matrix);
                            canvas.DrawText(ex.Message, 0, 0, paint);
                            canvas.Restore();
                        }
                    }
                    picture = recorder.EndRecording();
                }

            }
            return picture;
        }

        public org.pdfclown.documents.Page Page { get; set; }
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
