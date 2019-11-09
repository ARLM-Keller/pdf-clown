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
        public static readonly BindableProperty ScaleContentProperty = BindableProperty.Create(nameof(ScaleContent), typeof(float), typeof(PdfView), 1F,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnScaleFactorChanged((float)oldValue, (float)newValue));
        public static readonly BindableProperty ShowMarkupProperty = BindableProperty.Create(nameof(ShowMarkup), typeof(bool), typeof(PdfView), true,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).ShowMarkupChanged((bool)oldValue, (bool)newValue));

        private readonly List<PdfPicture> pictures = new List<PdfPicture>();
        private readonly SKPaint paintText = new SKPaint { Style = SKPaintStyle.StrokeAndFill, Color = SKColors.Black, TextSize = 14 };
        private float oldScale = 1;
        private float scale = 1;
        private readonly float indent = 10;
        private SKMatrix CurrentWindowScaleMatrix;
        private SKMatrix CurrentNavigationMatrix;
        private SKMatrix CurrentPictureMatrix;
        private SKPoint CurrentLocation;
        private SKMatrix CurrentViewMatrix;
        private SKRect CurrentArea;
        private PdfPicture CurrentPicture;
        private Annotation CurrentAnnotation;
        private SKRect CurrentAnnotationBounds;
        private string currentAnnotationText;
        private SKRect currentAnnotationTextBounds;

        public PdfView()
        {
            PaintContent += OnPaintContent;
        }

        public float ScaleContent
        {
            get => (float)GetValue(ScaleContentProperty);
            set => SetValue(ScaleContentProperty, value);
        }

        public bool ShowMarkup
        {
            get => (bool)GetValue(ShowMarkupProperty);
            set => SetValue(ShowMarkupProperty, value);
        }

        private string CurrentAnnotationText
        {
            get => currentAnnotationText;
            set
            {
                if (currentAnnotationText != value)
                {
                    currentAnnotationText = value;
                    if (currentAnnotationText != null)
                    {
                        var temp = new SKRect();
                        paintText.MeasureText(currentAnnotationText, ref temp);
                        temp.Inflate(10, 5);
                        currentAnnotationTextBounds = SKRect.Create(
                            CurrentAnnotationBounds.Left / XScaleFactor,
                            CurrentAnnotationBounds.Bottom / YScaleFactor,
                            temp.Width, temp.Height);
                    }
                    InvalidateSurface();
                }
            }
        }


        public File File { get; private set; }
        public Document Document { get; private set; }
        public SKSize DocumentSize { get; private set; }
        public Pages Pages { get; private set; }

        private void OnScaleFactorChanged(float oldValue, float newValue)
        {
            oldScale = oldValue;
            scale = newValue;
            UpdateMaximums();
        }

        private void ShowMarkupChanged(bool oldValue, bool newValue)
        {
            InvalidateSurface();
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

        protected override void OnWindowScaleChanged()
        {
            base.OnWindowScaleChanged();
            CurrentWindowScaleMatrix = SKMatrix.MakeScale(XScaleFactor, YScaleFactor);
        }

        protected virtual void OnPaintContent(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(BackgroundColor.ToSKColor());
            canvas.SetMatrix(CurrentViewMatrix);
            foreach (var pdfPicture in pictures)
            {
                if (CurrentViewMatrix.MapRect(pdfPicture.Bounds).IntersectsWith(CurrentArea))
                {
                    var picture = pdfPicture.GetPicture(this);
                    if (picture != null)
                    {
                        canvas.DrawPicture(picture, ref pdfPicture.Matrix);

                        if (ShowMarkup && pdfPicture.Annotations != null && pdfPicture.Annotations.Count > 0)
                        {
                            canvas.Save();
                            var drawPictureMatrix = CurrentViewMatrix;
                            SKMatrix.PreConcat(ref drawPictureMatrix, pdfPicture.Matrix);
                            //SKMatrix.PreConcat(ref drawPictureMatrix, CurrentPicture.InitialMatrix);

                            canvas.SetMatrix(drawPictureMatrix);
                            foreach (var annotation in pdfPicture.Annotations)
                            {
                                if (annotation.Visible)
                                {
                                    annotation.Draw(canvas);
                                }
                            }

                            canvas.SetMatrix(CurrentWindowScaleMatrix);
                            if (!string.IsNullOrEmpty(CurrentAnnotationText))
                            {
                                using (var color = new SKPaint() { Color = SKColors.Silver, Style = SKPaintStyle.Fill })
                                {
                                    canvas.DrawRect(currentAnnotationTextBounds, color);
                                    canvas.DrawText(CurrentAnnotationText,
                                        currentAnnotationTextBounds.Left + 5,
                                        currentAnnotationTextBounds.MidY + paintText.FontMetrics.Bottom,
                                        paintText);
                                }
                            }
                            canvas.Restore();
                        }
                    }
                }
            }
        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);
            CurrentLocation = e.Location;
            foreach (var picture in pictures)
            {
                if (CurrentViewMatrix.MapRect(picture.Bounds).Contains(CurrentLocation))
                {
                    OnTouchPage(picture, e);
                    return;
                }
            }
            CurrentPicture = null;
        }

        private void OnTouchPage(PdfPicture picture, SKTouchEventArgs e)
        {
            CurrentPicture = picture;
            CurrentPictureMatrix = CurrentViewMatrix;
            SKMatrix.PreConcat(ref CurrentPictureMatrix, CurrentPicture.Matrix);
            //SKMatrix.PreConcat(ref CurrentPictureMatrix, CurrentPicture.InitialMatrix);
            if (CurrentPicture.Annotations != null)
            {
                foreach (var annotation in CurrentPicture.Annotations)
                {
                    if (!annotation.Visible)
                        continue;
                    var bounds = annotation.GetBounds(CurrentPictureMatrix);
                    if (bounds.Contains(CurrentLocation))
                    {
                        CurrentAnnotationBounds = bounds;
                        OnTouchAnnotation(annotation, e);
                        return;
                    }
                }
            }
            CurrentAnnotation = null;
            CurrentAnnotationText = null;
        }

        private void OnTouchAnnotation(Annotation annotation, SKTouchEventArgs e)
        {
            CurrentAnnotation = annotation;
            if (e.ActionType == SKTouchAction.Moved)
            {
                CurrentAnnotationText = CurrentAnnotation.Text;
            }
        }

        public override void OnScrolled(int delta, KeyModifiers keyModifiers)
        {
            if (keyModifiers == KeyModifiers.None)
            {
                base.OnScrolled(delta, keyModifiers);
            }
            if (keyModifiers == KeyModifiers.Ctrl)
            {
                var scaleStep = 0.06F * Math.Sign(delta);

                var newSclae = scale + scaleStep + scaleStep * scale;
                if (newSclae > 0.01F && newSclae < 60F)
                {
                    CurrentNavigationMatrix.TryInvert(out var invertMatrix);
                    var oldSpacePoint = invertMatrix.MapPoint(CurrentLocation);

                    ScaleContent = newSclae;

                    var newPointer = CurrentNavigationMatrix.MapPoint(oldSpacePoint);

                    if (HorizontalScrollBarVisible)
                    {
                        HorizontalValue += newPointer.X - CurrentLocation.X;
                    }
                    if (VerticalScrollBarVisible)
                    {
                        VerticalValue += newPointer.Y - CurrentLocation.Y;
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
            ScaleContent = 1;
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
                ScaleContent = (float)(Width / (DocumentSize.Width));
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
            CurrentArea = SKRect.Create(0, 0, (float)Width * XScaleFactor, (float)Height * YScaleFactor);

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
            CurrentNavigationMatrix.SetScaleTranslate(scale, scale, ((float)-HorizontalValue) + dx, ((float)-VerticalValue) + dy);

            CurrentViewMatrix = CurrentWindowScaleMatrix;
            SKMatrix.PreConcat(ref CurrentViewMatrix, CurrentNavigationMatrix);
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
