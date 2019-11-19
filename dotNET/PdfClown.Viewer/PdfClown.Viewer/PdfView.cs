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
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnScaleContentChanged((float)oldValue, (float)newValue));
        public static readonly BindableProperty ShowMarkupProperty = BindableProperty.Create(nameof(ShowMarkup), typeof(bool), typeof(PdfView), true,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnShowMarkupChanged((bool)oldValue, (bool)newValue));
        public static readonly BindableProperty SelectedAnnotationProperty = BindableProperty.Create(nameof(SelectedAnnotation), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnSelectedAnnotationChanged((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty SelectedMarkupProperty = BindableProperty.Create(nameof(SelectedMarkup), typeof(Markup), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnSelectedMarkupChanged((Markup)oldValue, (Markup)newValue));
        public static readonly BindableProperty DraggingProperty = BindableProperty.Create(nameof(Dragging), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnDragging((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty SizingProperty = BindableProperty.Create(nameof(Sizing), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnSizingChanged((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty PointingProperty = BindableProperty.Create(nameof(Pointing), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnPointingChanged((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty IsChangedProperty = BindableProperty.Create(nameof(IsChanged), typeof(bool), typeof(PdfView), false,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnIsChangedChanged((bool)oldValue, (bool)newValue));

        private readonly List<PdfPagePicture> pictures = new List<PdfPagePicture>();
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
        private PdfPagePicture CurrentPicture;

        private Annotation CurrentAnnotation;
        private SKRect CurrentAnnotationBounds;
        private string currentAnnotationText;
        private SKRect currentAnnotationTextBounds;
        private SKPoint CurrentMoveLocation;
        private SKPoint? PressedCursorLocation;

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

        public Annotation SelectedAnnotation
        {
            get => (Annotation)GetValue(SelectedAnnotationProperty);
            set => SetValue(SelectedAnnotationProperty, value);
        }

        public Markup SelectedMarkup
        {
            get => (Markup)GetValue(SelectedMarkupProperty);
            set => SetValue(SelectedMarkupProperty, value);
        }

        public Annotation Dragging
        {
            get => (Annotation)GetValue(DraggingProperty);
            set => SetValue(DraggingProperty, value);
        }

        public Annotation Sizing
        {
            get => (Annotation)GetValue(SizingProperty);
            set => SetValue(SizingProperty, value);
        }

        public Annotation Pointing
        {
            get => (Annotation)GetValue(PointingProperty);
            set => SetValue(PointingProperty, value);
        }

        public bool IsChanged
        {
            get => (bool)GetValue(IsChangedProperty);
            set => SetValue(IsChangedProperty, value);
        }

        private string CurrentAnnotationText
        {
            get => currentAnnotationText;
            set
            {
                if (currentAnnotationText != value)
                {
                    currentAnnotationText = value;
                    if (!string.IsNullOrEmpty(currentAnnotationText))
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

        public PdfPagePicture CurrentPage
        {
            get
            {
                var area = CurrentArea;
                area.Inflate(-50, -50);
                foreach (var pdfPicture in pictures)
                {
                    if (CurrentViewMatrix.MapRect(pdfPicture.Bounds).IntersectsWith(area))
                    {
                        return pdfPicture;
                    }
                }
                return null;
            }
        }

        public string FilePath { get; private set; }
        public string TempFilePath { get; private set; }

        public event EventHandler<EventArgs> DragComplete;

        public IEnumerable<Annotation> GetAllAnnotations()
        {
            foreach (var picture in pictures)
            {
                if (picture.Annotations != null && picture.Annotations.Count > 0)
                {
                    foreach (var annotation in picture.Annotations)
                    {
                        yield return annotation;
                    }
                }
            }
        }

        private void OnScaleContentChanged(float oldValue, float newValue)
        {
            oldScale = oldValue;
            scale = newValue;
            UpdateMaximums();
        }

        private void OnShowMarkupChanged(bool oldValue, bool newValue)
        {
            InvalidateSurface();
        }

        private void OnSelectedAnnotationChanged(Annotation oldValue, Annotation newValue)
        {
            if (newValue == null)
                SelectedMarkup = null;
            else if (newValue is Markup markup)
                SelectedMarkup = markup;
            InvalidateSurface();
        }

        private void OnSelectedMarkupChanged(Markup oldValue, Markup newValue)
        {
            SelectedAnnotation = newValue;
        }

        private void OnDragging(Annotation oldValue, Annotation newValue)
        {
            if (newValue == null)
            {
                IsChanged = true;
                SelectedAnnotation = oldValue;
            }
        }

        private void OnSizingChanged(Annotation oldValue, Annotation newValue)
        {
            if (newValue != null)
            {
                CheckoutDrag();
                Cursor = CursorType.SizeNWSE;
            }
            else
            {
                Cursor = CursorType.Arrow;
                IsChanged = true;
                SelectedAnnotation = oldValue;
            }
        }

        private void OnPointingChanged(Annotation oldValue, Annotation newValue)
        {
            if (newValue != null)
            {
                CheckoutDrag();
                Cursor = CursorType.Cross;
            }
            else
            {
                Cursor = CursorType.Arrow;
                IsChanged = true;
                SelectedAnnotation = oldValue;
            }
        }

        private void OnIsChangedChanged(bool oldValue, bool newValue)
        {

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

                        if (ShowMarkup && pdfPicture.Annotations.Count > 0)
                        {
                            OnPaintAnnotations(canvas, pdfPicture);
                        }
                    }
                }
            }
        }

        private void OnPaintAnnotations(SKCanvas canvas, PdfPagePicture pdfPicture)
        {
            canvas.Save();
            var drawPictureMatrix = CurrentViewMatrix;
            SKMatrix.PreConcat(ref drawPictureMatrix, pdfPicture.Matrix);

            canvas.SetMatrix(drawPictureMatrix);
            foreach (var annotation in pdfPicture.Annotations)
            {
                if (annotation.Visible)
                {
                    annotation.Draw(canvas);
                    if (annotation == SelectedAnnotation
                        && annotation != Dragging
                        && annotation != Sizing
                        && annotation != Pointing)
                    {
                        canvas.SetMatrix(drawPictureMatrix);
                        using (var paint = new SKPaint { Color = SKColors.OrangeRed, Style = SKPaintStyle.Stroke, StrokeWidth = 1 })
                        {
                            var bounds = annotation.Box;
                            if (annotation is StickyNote stick)
                            {
                                bounds.Size = new SKSize(StickyNote.size / canvas.TotalMatrix.ScaleX, StickyNote.size / canvas.TotalMatrix.ScaleY);
                            }
                            bounds.Inflate(3, 3);
                            canvas.DrawRoundRect(bounds, 3, 3, paint);
                        }
                    }
                }
            }
            if (Dragging != null)
            {
                Dragging.Draw(canvas);
            }
            OnPaintAnnotationToolTip(canvas);
            canvas.Restore();
        }

        private void OnPaintAnnotationToolTip(SKCanvas canvas)
        {
            if (!string.IsNullOrEmpty(CurrentAnnotationText))
            {
                canvas.SetMatrix(CurrentWindowScaleMatrix);
                using (var color = new SKPaint() { Color = SKColors.Silver, Style = SKPaintStyle.Fill })
                {
                    canvas.DrawRect(currentAnnotationTextBounds, color);
                    canvas.DrawText(CurrentAnnotationText,
                        currentAnnotationTextBounds.Left + 5,
                        currentAnnotationTextBounds.MidY + paintText.FontMetrics.Bottom,
                        paintText);
                }
            }
        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);
            CurrentLocation = e.Location;
            if (e.MouseButton == SKMouseButton.Middle)
            {
                if (e.ActionType == SKTouchAction.Pressed)
                {
                    CurrentMoveLocation = CurrentLocation;
                    Cursor = CursorType.ScrollAll;
                    return;
                }
                else if (e.ActionType == SKTouchAction.Moved)
                {
                    var vector = CurrentLocation - CurrentMoveLocation;
                    HorizontalValue -= vector.X;
                    VerticalValue -= vector.Y;
                    CurrentMoveLocation = CurrentLocation;
                    return;
                }
            }
            if (!PdfPagePicture.IsPaintComplete)
            {
                return;
            }
            foreach (var picture in pictures)
            {
                if (CurrentViewMatrix.MapRect(picture.Bounds).Contains(CurrentLocation))
                {
                    OnTouchPage(picture, e);
                    return;
                }
            }
            Cursor = CursorType.Arrow;
            CurrentPicture = null;
        }

        private void OnTouchPage(PdfPagePicture picture, SKTouchEventArgs e)
        {
            CurrentPicture = picture;
            CurrentPictureMatrix = CurrentViewMatrix;
            SKMatrix.PreConcat(ref CurrentPictureMatrix, CurrentPicture.Matrix);
            //SKMatrix.PreConcat(ref CurrentPictureMatrix, CurrentPicture.InitialMatrix);
            if (Pointing != null)
            {
                OnTouchPointed(e);
                return;
            }
            else if (Sizing != null)
            {
                OnTouchSized(e);
                return;
            }
            else if (Dragging != null)
            {
                OnTouchDragged(e);
                return;
            }
            else
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
            if (e.ActionType == SKTouchAction.Released)
            {
                SelectedAnnotation = null;
            }
            Cursor = CursorType.Arrow;
            CurrentAnnotation = null;
            CurrentAnnotationText = null;
        }

        private void OnTouchPointed(SKTouchEventArgs e)
        {
            if (PressedCursorLocation == null)
                return;
            CurrentPictureMatrix.TryInvert(out var invert);

            if (e.ActionType == SKTouchAction.Moved)
            {
                if (Pointing is Line line)
                {
                    line.EndPoint = invert.MapPoint(e.Location);
                }
                PressedCursorLocation = e.Location;
                InvalidateSurface();
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                if (Pointing is Line line)
                {
                    line.RefreshBox();
                }
                Pointing = null;
            }
        }

        private void OnTouchSized(SKTouchEventArgs e)
        {
            if (PressedCursorLocation == null)
                return;
            CurrentPictureMatrix.TryInvert(out var invert);

            if (e.ActionType == SKTouchAction.Moved)
            {
                var bound = Sizing.GetBounds(CurrentPictureMatrix);
                bound.Size += new SKSize(e.Location - PressedCursorLocation.Value);

                Sizing.MoveTo(invert.MapRect(bound));

                PressedCursorLocation = e.Location;
                InvalidateSurface();
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                Sizing = null;
            }
        }

        private void OnTouchDragged(SKTouchEventArgs e)
        {
            CurrentPictureMatrix.TryInvert(out var invert);

            if (e.ActionType == SKTouchAction.Moved)
            {
                var bound = Dragging.GetBounds(CurrentPictureMatrix);
                if (PressedCursorLocation == null || e.MouseButton == SKMouseButton.Unknown)
                {
                    bound.Location = e.Location;
                }
                else
                {
                    bound.Location += e.Location - PressedCursorLocation.Value;
                    PressedCursorLocation = e.Location;
                }
                Dragging.MoveTo(invert.MapRect(bound));

                InvalidateSurface();
            }
            else if (e.ActionType == SKTouchAction.Pressed)
            {
                PressedCursorLocation = e.Location;
                if (Dragging is StickyNote sticky)
                {
                    CheckoutDrag();
                }
                if (Dragging is Line line)
                {
                    line.StartPoint = invert.MapPoint(e.Location);
                    Pointing = Dragging;
                }
                else
                {
                    Sizing = Dragging;
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                CheckoutDrag();
            }
        }

        private void OnTouchAnnotation(Annotation annotation, SKTouchEventArgs e)
        {
            CurrentAnnotation = annotation;

            if (e.ActionType == SKTouchAction.Moved)
            {
                CurrentAnnotationText = CurrentAnnotation.Text;
                if (e.MouseButton == SKMouseButton.Left)
                {
                    if (SelectedAnnotation != null && PressedCursorLocation != null && Cursor == CursorType.Hand)
                    {
                        if (Dragging == null)
                        {
                            var dif = SKPoint.Distance(e.Location, PressedCursorLocation.Value);
                            if (Math.Abs(dif) > 5)
                            {
                                Dragging = SelectedAnnotation;
                            }
                        }
                    }
                }
                else if (e.MouseButton == SKMouseButton.Unknown)
                {
                    var rect = new SKRect(
                        CurrentAnnotationBounds.Right - 10,
                        CurrentAnnotationBounds.Bottom - 10,
                        CurrentAnnotationBounds.Right,
                        CurrentAnnotationBounds.Bottom);
                    if (rect.Contains(e.Location))
                    {
                        Cursor = CursorType.SizeNWSE;
                    }
                    else
                    {
                        Cursor = CursorType.Hand;
                    }
                }
            }
            else if (e.ActionType == SKTouchAction.Pressed)
            {
                if (e.MouseButton == SKMouseButton.Left)
                {
                    PressedCursorLocation = e.Location;
                    SelectedAnnotation = CurrentAnnotation;
                    if (Cursor == CursorType.SizeNWSE)
                    {
                        Sizing = CurrentAnnotation;
                    }
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                if (e.MouseButton == SKMouseButton.Left)
                {
                    PressedCursorLocation = null;
                }
            }
        }

        private void CheckoutDrag()
        {
            if (Dragging == null)
                return;
            var picture = GetPicture(Dragging.Page);
            if (picture != null && !picture.Annotations.Contains(Dragging))
            {
                picture.Annotations.Add(Dragging);
            }
            if (!Dragging.Page.Annotations.Contains(Dragging))
            {
                Dragging.Page.Annotations.Add(Dragging);
            }
            DragComplete?.Invoke(this, EventArgs.Empty);
            Dragging = null;
        }

        private PdfPagePicture GetPicture(Documents.Page page)
        {
            foreach (var picture in pictures)
            {
                if (picture.Page == page)
                {
                    return picture;
                }
            }
            return null;
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
                if (newSclae < 0.01F)
                    newSclae = 0.01F;
                if (newSclae > 60F)
                    newSclae = 60F;
                if (newSclae != scale)
                {
                    var unscaleLocations = new SKPoint(CurrentLocation.X / XScaleFactor, CurrentLocation.Y / YScaleFactor);
                    CurrentNavigationMatrix.TryInvert(out var oldInvertMatrix);
                    var oldSpacePoint = oldInvertMatrix.MapPoint(unscaleLocations);

                    ScaleContent = newSclae;

                    var newCurrentLocation = CurrentNavigationMatrix.MapPoint(oldSpacePoint);

                    var vector = newCurrentLocation - unscaleLocations;
                    if (HorizontalScrollBarVisible)
                    {
                        HorizontalValue += vector.X;
                    }
                    if (VerticalScrollBarVisible)
                    {
                        VerticalValue += vector.Y;
                    }

                }
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateCurrentMatrix();
        }

        public void Load(string filePath)
        {
            if (File != null)
            {
                ClearPictures();
                File.Dispose();
            }
            FilePath = filePath;
            TempFilePath = filePath + "~";
            System.IO.File.Copy(filePath, TempFilePath, true);
            var fileStream = new System.IO.FileStream(TempFilePath, System.IO.FileMode.Open,
                                                             System.IO.FileAccess.ReadWrite,
                                                             System.IO.FileShare.ReadWrite);
            Load(fileStream);
        }

        public void Save()
        {
            File.Save(FilePath, SerializationModeEnum.Standard);
            System.IO.File.SetLastWriteTimeUtc(FilePath, DateTime.UtcNow);
            IsChanged = false;
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
            IsChanged = false;
        }

        private SKSize LoadPages()
        {
            float totalWidth, totalHeight;
            Pages = Document.Pages;

            totalWidth = 0F;
            totalHeight = 0F;
            var pictures = new List<PdfPagePicture>();
            foreach (var page in Pages)
            {
                totalHeight += indent;
                var box = page.RotatedBox;
                var imageSize = new SKSize(box.Width, box.Height);
                var details = new PdfPagePicture()
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
            foreach (var picture in pictures)
            {
                if ((picture.Size.Width + indent * 2) < DocumentSize.Width)
                {
                    picture.Matrix.TransX += (DocumentSize.Width - picture.Size.Width + indent * 2) / 2;
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

    public enum PdfViewrAnnotationMode
    {
        None,
        Drag,
        Create
    }
}
