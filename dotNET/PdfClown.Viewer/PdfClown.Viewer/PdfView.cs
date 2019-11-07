using PdfClown.Documents;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Tools;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace PdfClown.Viewer
{
    public class PdfView : SKScrollView
    {
        public static readonly BindableProperty ScaleFactorProperty = BindableProperty.Create(nameof(ScaleFactorProperty), typeof(float), typeof(PdfView), 1F,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnScaleFactorChanged((float)oldValue, (float)newValue));
        public static readonly BindableProperty ShowMarkupProperty = BindableProperty.Create(nameof(ShowMarkup), typeof(bool), typeof(PdfView), true,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).ShowMarkupChanged((bool)oldValue, (bool)newValue));

        private SKMatrix currentMatrix;
        private readonly List<PdfPicture> pictures = new List<PdfPicture>();
        private float scale = 1;
        private readonly float indent = 10;
        private SKPoint pointerLocation;

        public PdfView()
        {
            PaintContent += OnPaintContent;
        }

        public float ScaleFactor
        {
            get => (float)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        public bool ShowMarkup
        {
            get => (bool)GetValue(ShowMarkupProperty);
            set => SetValue(ShowMarkupProperty, value);
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

        private void ShowMarkupChanged(bool oldValue, bool newValue)
        {
            throw new NotImplementedException();
        }

        protected virtual void OnPaintContent(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(BackgroundColor.ToSKColor());
            canvas.Concat(ref currentMatrix);
            var area = SKRect.Create(0, 0, (float)Width, (float)Height);
            foreach (var pictureDetails in pictures)
            {
                if (currentMatrix.MapRect(pictureDetails.Bounds).IntersectsWith(area))
                {
                    var picture = pictureDetails.GetPicture(this);
                    if (picture != null)
                    {
                        canvas.DrawPicture(picture, ref pictureDetails.Matrix);
                    }
                    if (ShowMarkup && pictureDetails.Annotations != null)
                    {
                        foreach (var annotation in pictureDetails.Annotations)
                        {
                            annotation.Draw(canvas);
                        }
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
                var scaleStep = 0.06F * Math.Sign(delta);

                var newSclae = scale + scaleStep + scaleStep * scale;
                if (newSclae > 0.01F && newSclae < 60F)
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

        public void Load(string filePath)
        {
            var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Open,
                                                             System.IO.FileAccess.ReadWrite,
                                                             System.IO.FileShare.ReadWrite);
            Load(fileStream);
        }

        public void Load(System.IO.Stream stream)
        {
            if (File != null)
            {
                ClearPictures();
                File.Dispose();
            }
            ScaleFactor = 1;
            HorizontalValue = 0;
            VerticalValue = 0;
            File = new File(stream);
            Document = File.Document;
            LoadPages();
        }

        private SKSize LoadPages()
        {
            float totalWidth, totalHeight;
            Pages = Document.Pages;

            totalWidth = 0F;
            totalHeight = 0F;
            var pictures = new List<PdfPicture>();
            foreach (var page in Pages)
            {
                totalHeight += indent;
                var box = page.RotatedBox;
                var imageSize = new SKSize(box.Width, box.Height);
                var details = new PdfPicture()
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
            DocumentSize = new SKSize(totalWidth + indent * 2, totalHeight);
            foreach (var details in pictures)
            {
                if ((details.Size.Width + indent * 2) < DocumentSize.Width)
                {
                    details.Matrix.TransX += (DocumentSize.Width - details.Size.Width + indent * 2) / 2;
                }
            }
            this.pictures.AddRange(pictures);
            Device.BeginInvokeOnMainThread(() =>
            {
                ScaleFactor = (float)(Width / (DocumentSize.Width));
                UpdateMaximums();

            });

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
}
