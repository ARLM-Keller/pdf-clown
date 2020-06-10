using Org.BouncyCastle.Security;
using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Tools;
using PdfClown.Util.Math.Geom;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
        public static readonly BindableProperty HoverAnnotationProperty = BindableProperty.Create(nameof(HoverAnnotation), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnHoverAnnotationChanged((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty SelectedAnnotationProperty = BindableProperty.Create(nameof(SelectedAnnotation), typeof(Annotation), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnSelectedAnnotationChanged((Annotation)oldValue, (Annotation)newValue));
        public static readonly BindableProperty SelectedMarkupProperty = BindableProperty.Create(nameof(SelectedMarkup), typeof(Markup), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnSelectedMarkupChanged((Markup)oldValue, (Markup)newValue));
        public static readonly BindableProperty CurrentOperationProperty = BindableProperty.Create(nameof(CurrentOperation), typeof(OperationType), typeof(PdfView), OperationType.None,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnCurrentOperationChanged((OperationType)oldValue, (OperationType)newValue));
        public static readonly BindableProperty IsChangedProperty = BindableProperty.Create(nameof(IsChanged), typeof(bool), typeof(PdfView), false,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnIsChangedChanged((bool)oldValue, (bool)newValue));
        public static readonly BindableProperty CurrentPointProperty = BindableProperty.Create(nameof(CurrentPoint), typeof(ControlPoint), typeof(PdfView), null,
            propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnCurrentPointChanged((ControlPoint)oldValue, (ControlPoint)newValue));
        public static readonly BindableProperty HoverPointProperty = BindableProperty.Create(nameof(HoverPoint), typeof(ControlPoint), typeof(PdfView), null,
           propertyChanged: (bindable, oldValue, newValue) => ((PdfView)bindable).OnHoverPointChanged((ControlPoint)oldValue, (ControlPoint)newValue));

        private readonly List<PdfPagePicture> pictures = new List<PdfPagePicture>();
        private readonly SKPaint paintRed = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.OrangeRed };
        private readonly SKPaint paintText = new SKPaint { Style = SKPaintStyle.StrokeAndFill, Color = SKColors.Black, TextSize = 14, IsAntialias = true };
        private readonly SKPaint paintPointFill = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };
        private readonly SKPaint paintTextSelectionFill = new SKPaint { Color = SKColors.LightBlue, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.Multiply, IsAntialias = true };
        private readonly SKPaint paintBorderDefault = new SKPaint { Color = SKColors.Gray, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        private readonly SKPaint paintBorderSelection = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        private readonly SKPaint paintAnnotationToolTip = new SKPaint() { Color = SKColors.LightGray, Style = SKPaintStyle.Fill, IsAntialias = true };

        private float oldScale = 1;
        private float scale = 1;
        private readonly float indent = 10;
        private SKMatrix currentWindowScaleMatrix = SKMatrix.Identity;
        private SKMatrix currentNavigationMatrix = SKMatrix.Identity;
        private SKMatrix currentPictureMatrix = SKMatrix.Identity;
        private SKMatrix invertPictureMatrix = SKMatrix.Identity;
        private SKMatrix currentViewMatrix = SKMatrix.Identity;

        private SKPoint currentLocation;
        private SKRect currentArea;
        private PdfPagePicture currentPicture;

        private Annotation currentAnnotation;
        private SKRect currentAnnotationBounds;
        private string currentAnnotationText;
        private SKRect currentAnnotationTextBounds;

        private SKPoint currentMoveLocation;
        private SKPoint? pressedCursorLocation;
        private SKPoint currentPointerLocation;
        private string selectedString;
        private Quad? selectedQuad;
        private TextChar startSelectionChar;

        private LinkedList<OperationEntry> operations = new LinkedList<OperationEntry>();
        private LinkedListNode<OperationEntry> operationLink;
        private bool handlePropertyChanged = true;

        public PdfView()
        {
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

        public Annotation HoverAnnotation
        {
            get => (Annotation)GetValue(HoverAnnotationProperty);
            set => SetValue(HoverAnnotationProperty, value);
        }

        public Markup SelectedMarkup
        {
            get => (Markup)GetValue(SelectedMarkupProperty);
            set => SetValue(SelectedMarkupProperty, value);
        }

        public ControlPoint CurrentPoint
        {
            get => (ControlPoint)GetValue(CurrentPointProperty);
            set => SetValue(CurrentPointProperty, value);
        }

        public ControlPoint HoverPoint
        {
            get => (ControlPoint)GetValue(HoverPointProperty);
            set => SetValue(HoverPointProperty, value);
        }

        public OperationType CurrentOperation
        {
            get => (OperationType)GetValue(CurrentOperationProperty);
            set => SetValue(CurrentOperationProperty, value);
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
                            currentAnnotationBounds.Left / XScaleFactor,
                            currentAnnotationBounds.Bottom / YScaleFactor,
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

        public PdfPagePicture CenterPage
        {
            get
            {
                var area = currentArea;
                area.Inflate(-(float)Width / 4F, -(float)Height / 4F);
                foreach (var pdfPicture in pictures)
                {
                    if (currentViewMatrix.MapRect(pdfPicture.Bounds).IntersectsWith(area))
                    {
                        return pdfPicture;
                    }
                }
                return null;
            }
        }

        public string FilePath { get; private set; }

        public string TempFilePath { get; private set; }

        public List<TextChar> TextSelection { get; private set; } = new List<TextChar>();

        public Quad? SelectedQuad
        {
            get
            {
                if (selectedQuad == null)
                {
                    foreach (TextChar textChar in TextSelection)
                    {
                        if (!selectedQuad.HasValue)
                        { selectedQuad = textChar.Quad; }
                        else
                        { selectedQuad = Quad.Union(selectedQuad.Value, textChar.Quad); }
                    }
                }
                return selectedQuad;
            }
        }

        public string SelectedString
        {
            get
            {
                if (selectedString == null)
                {
                    var textBuilder = new StringBuilder();
                    TextChar prevTextChar = null;
                    foreach (TextChar textChar in TextSelection)
                    {
                        if (prevTextChar != null && prevTextChar.TextString != textChar.TextString)
                        {
                            textBuilder.Append(' ');
                        }
                        textBuilder.Append(textChar.Value);
                        prevTextChar = textChar;
                    }
                    selectedString = textBuilder.ToString();
                }
                return selectedString;
            }
        }

        public bool CanRedo => (operationLink == null ? operations.First : operationLink?.Next) != null;

        public bool CanUndo => operationLink != null;

        public event EventHandler<AnnotationEventArgs> AnnotationAdded;

        public event EventHandler<AnnotationEventArgs> AnnotationRemoved;

        public event EventHandler<EventArgs> TextSelectionChanged;

        public event EventHandler<EventArgs> DragComplete;

        public event EventHandler<AnnotationEventArgs> SelectedAnnotationChanged;

        public void BeginDragAnnotation(Annotation note)
        {
            SelectedAnnotation = note;
            CurrentOperation = OperationType.AnnotationDrag;
        }

        public IEnumerable<Annotation> GetAllAnnotations()
        {
            foreach (var picture in pictures)
            {
                if (picture.Page.Annotations != null && picture.Page.Annotations.Count > 0)
                {
                    foreach (var annotation in picture.Page.Annotations)
                    {
                        yield return annotation;
                        if (annotation is Markup markup
                            && markup.Popup != null
                            && !picture.Page.Annotations.Contains(markup.Popup))
                        {
                            yield return markup.Popup;
                        }
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

        private void OnHoverAnnotationChanged(Annotation oldValue, Annotation newValue)
        {

        }

        private void OnSelectedAnnotationChanged(Annotation oldValue, Annotation newValue)
        {
            SelectedMarkup = newValue as Markup;
            CurrentOperation = OperationType.None;
            CurrentPoint = null;
            if (oldValue != null)
            {
                SuspendAnnotationPropertyHandler(oldValue);
            }
            if (newValue != null)
            {
                ResumeAnnotationPropertyHandler(newValue);
                if (newValue.IsNew)
                {
                    AddAnnotation(newValue);
                }
            }
            SelectedAnnotationChanged?.Invoke(this, new AnnotationEventArgs(newValue));
            InvalidateSurface();
        }

        private void SuspendAnnotationPropertyHandler(Annotation annotation)
        {
            if (annotation != null)
            {
                annotation.PropertyChanged -= OnAnnotationPropertyChanged;
            }
        }

        private void ResumeAnnotationPropertyHandler(Annotation annotation)
        {
            if (annotation != null)
            {
                annotation.PropertyChanged += OnAnnotationPropertyChanged;
            }
        }

        private void OnAnnotationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!handlePropertyChanged)
                return;
            var annotation = (Annotation)sender;
            var details = (DetailedPropertyChangedEventArgs)e;
            switch (e.PropertyName)
            {
                case "SKColor":
                    BeginOperation(annotation, OperationType.AnnotationColor, nameof(Annotation.Color), details.OldValue, details.NewValue);
                    break;
                case "Text":
                    BeginOperation(annotation, OperationType.AnnotationText, nameof(Annotation.Text), details.OldValue, details.NewValue);
                    break;
                case "Subject":
                    BeginOperation(annotation, OperationType.AnnotationSubject, nameof(Annotation.Subject), details.OldValue, details.NewValue);
                    break;
            }
            InvalidateSurface();
        }

        private void OnSelectedMarkupChanged(Markup oldValue, Markup newValue)
        {
            SelectedAnnotation = newValue;
        }

        private void OnCurrentOperationChanged(OperationType oldValue, OperationType newValue)
        {
            var annotation = SelectedAnnotation;

            if (annotation != null && operationLink?.Value.Annotation == annotation)
            {
                switch (oldValue)
                {
                    case OperationType.AnnotationDrag:
                        EndOperation(operationLink.Value);
                        break;
                    case OperationType.AnnotationSize:
                        EndOperation(operationLink.Value);
                        break;
                    case OperationType.PointMove:
                    case OperationType.PointAdd:
                    case OperationType.PointRemove:
                        EndOperation(operationLink.Value);

                        break;
                }
            }
            if (newValue != OperationType.None)
            {
                if (annotation == null)
                {
                    throw new InvalidOperationException("Operation on non");
                }

                if (oldValue == OperationType.AnnotationDrag)
                {
                    DragComplete?.Invoke(this, new AnnotationEventArgs(annotation));
                }

            }
            switch (newValue)
            {
                case OperationType.AnnotationDrag:
                    BeginOperation(annotation, newValue, "Box");
                    break;
                case OperationType.AnnotationSize:
                    BeginOperation(annotation, newValue, "Box");
                    Cursor = CursorType.SizeNWSE;
                    break;
                case OperationType.PointMove:
                case OperationType.PointAdd:
                case OperationType.PointRemove:
                    BeginOperation(annotation, newValue, CurrentPoint);
                    Cursor = CursorType.Cross;
                    break;
                case OperationType.None:
                    Cursor = CursorType.Arrow;
                    IsChanged = true;
                    break;
            }
        }

        private void OnCurrentPointChanged(ControlPoint oldValue, ControlPoint newValue)
        {
            if (newValue != null)
            {
                SelectedAnnotation = newValue.Annotation;
            }
            else
            {
                CurrentOperation = OperationType.None;
            }
        }

        private void OnHoverPointChanged(ControlPoint oldValue, ControlPoint newValue)
        {
            if (newValue != null)
            {
                Cursor = CursorType.Cross;
            }
            else
            {
                Cursor = CursorType.Arrow;
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
            currentWindowScaleMatrix = SKMatrix.MakeScale(XScaleFactor, YScaleFactor);
        }

        protected override void OnPaintContent(SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;

            canvas.SetMatrix(currentViewMatrix);
            foreach (var pdfPicture in pictures)
            {
                if (currentViewMatrix.MapRect(pdfPicture.Bounds).IntersectsWith(currentArea))
                {
                    var picture = pdfPicture.GetPicture(this);
                    if (picture != null)
                    {
                        canvas.Save();
                        canvas.Concat(ref pdfPicture.Matrix);

                        canvas.DrawPicture(picture);

                        if (ShowMarkup && pdfPicture.Page.Annotations.Count > 0)
                        {
                            OnPaintAnnotations(canvas, pdfPicture);
                        }
                        if (TextSelection.Count > 0)
                        {
                            foreach (var textChar in TextSelection)
                            {
                                if (textChar.TextString.Context == pdfPicture.Page)
                                {
                                    using (var path = textChar.Quad.GetPath())
                                    {
                                        canvas.DrawPath(path, paintTextSelectionFill);
                                    }
                                }
                            }
                        }
                        ////temp
                        //{
                        //    try
                        //    {
                        //        foreach (var textString in pdfPicture.Page.Strings)
                        //        {
                        //            foreach (var textChar in textString.TextChars)
                        //            {
                        //                canvas.DrawPoints(SKPointMode.Polygon, textChar.Quad.GetPoints(), paintRed);
                        //            }
                        //        }
                        //    }
                        //    catch
                        //    {

                        //    }
                        //}
                        canvas.Restore();
                    }
                }
            }
        }

        private void OnPaintAnnotations(SKCanvas canvas, PdfPagePicture pdfPicture)
        {
            foreach (var annotation in pdfPicture.Page.Annotations)
            {
                if (annotation.Visible)
                {
                    annotation.Draw(canvas);
                    if (annotation == SelectedAnnotation)
                    {
                        OnPaintSelectedAnnotation(canvas, annotation);
                    }
                    if (annotation is Popup popup
                        && popup.Markup != null
                        && !pdfPicture.Page.Annotations.Contains(popup.Markup))
                    {
                        popup.Markup.Draw(canvas);
                    }
                }

            }
            //if (Dragging != null && !pdfPicture.Annotations.Contains(Dragging))
            //{
            //    Dragging.Draw(canvas);
            //}
            OnPaintAnnotationToolTip(canvas);
        }

        private void OnPaintSelectedAnnotation(SKCanvas canvas, Annotation annotation)
        {
            if (CurrentOperation == OperationType.None
            && annotation != CurrentPoint?.Annotation)
            {
                var bounds = annotation.Box;
                if (annotation is StickyNote stick)
                {
                    bounds.Size = new SKSize(StickyNote.size / canvas.TotalMatrix.ScaleX, StickyNote.size / canvas.TotalMatrix.ScaleY);
                }
                bounds.Inflate(2, 2);
                canvas.DrawRoundRect(bounds, 3, 3, paintBorderSelection);
            }
            foreach (var controlPoint in annotation.GetControlPoints())
            {
                var bounds = controlPoint.Bounds;
                canvas.DrawOval(bounds, paintPointFill);
                canvas.DrawOval(bounds, controlPoint == CurrentPoint ? paintBorderSelection : paintBorderDefault);
            }
        }

        private void OnPaintAnnotationToolTip(SKCanvas canvas)
        {
            if (!string.IsNullOrEmpty(CurrentAnnotationText))
            {
                canvas.Save();
                canvas.SetMatrix(currentWindowScaleMatrix);
                canvas.DrawRect(currentAnnotationTextBounds, paintAnnotationToolTip);
                canvas.DrawText(CurrentAnnotationText,
                    currentAnnotationTextBounds.Left + 5,
                    currentAnnotationTextBounds.MidY + paintText.FontMetrics.Bottom,
                    paintText);
                canvas.Restore();
            }
        }

        public override bool OnKeyDown(string keyName, KeyModifiers modifiers)
        {
            if (string.Equals(keyName, "Escape", StringComparison.OrdinalIgnoreCase))
            {
                if (CurrentPoint != null && SelectedAnnotation is VertexShape vertexShape && vertexShape.IsNew)
                {
                    CloseVertextShape(vertexShape);
                }
            }
            else if (string.Equals(keyName, "Z", StringComparison.OrdinalIgnoreCase))
            {
                if (modifiers == KeyModifiers.Ctrl)
                {
                    Undo();
                }
                else if (modifiers == (KeyModifiers.Ctrl | KeyModifiers.Shift))
                {
                    Redo();
                }
            }
            return base.OnKeyDown(keyName, modifiers);

        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);
            currentLocation = e.Location;
            if (e.MouseButton == SKMouseButton.Middle)
            {
                if (e.ActionType == SKTouchAction.Pressed)
                {
                    currentMoveLocation = currentLocation;
                    Cursor = CursorType.ScrollAll;
                    return;
                }
                else if (e.ActionType == SKTouchAction.Moved)
                {
                    var vector = currentLocation - currentMoveLocation;
                    HorizontalValue -= vector.X;
                    VerticalValue -= vector.Y;
                    currentMoveLocation = currentLocation;
                    return;
                }
            }
            if (!PdfPagePicture.IsPaintComplete)
            {
                return;
            }
            foreach (var picture in pictures)
            {
                if (currentViewMatrix.MapRect(picture.Bounds).Contains(currentLocation))
                {
                    OnTouchPage(picture, e);
                    return;
                }
            }
            Cursor = CursorType.Arrow;
            currentPicture = null;
        }

        private void OnTouchPage(PdfPagePicture picture, SKTouchEventArgs e)
        {
            currentPicture = picture;
            currentPictureMatrix = currentViewMatrix.PreConcat(currentPicture.Matrix);
            currentPictureMatrix.TryInvert(out invertPictureMatrix);
            currentPointerLocation = invertPictureMatrix.MapPoint(e.Location);
            //SKMatrix.PreConcat(ref CurrentPictureMatrix, CurrentPicture.InitialMatrix);

            if (CurrentOperation == OperationType.PointMove)
            {
                if (CurrentPoint != null)
                {
                    OnTouchPointMove(e);
                    return;
                }
            }
            if (CurrentOperation == OperationType.PointAdd)
            {
                if (CurrentPoint != null)
                {
                    OnTouchPointAdd(e);
                    return;
                }
            }
            if (CurrentOperation == OperationType.AnnotationSize)
            {
                OnTouchSized(e);
                return;
            }
            if (CurrentOperation == OperationType.AnnotationDrag)
            {
                OnTouchDragged(e);
                return;
            }
            if (SelectedAnnotation != null && SelectedAnnotation.Page == currentPicture.Page)
            {
                var bounds = SelectedAnnotation.GetBounds(currentPictureMatrix);
                bounds.Inflate(4, 4);
                if (bounds.Contains(currentLocation))
                {
                    currentAnnotationBounds = bounds;
                    OnTouchAnnotation(SelectedAnnotation, e);
                    return;
                }
            }
            if (OnTouchText(picture, e))
            {
                return;
            }
            foreach (var annotation in currentPicture.Page.Annotations)
            {
                if (!annotation.Visible)
                    continue;
                var bounds = annotation.GetBounds(currentPictureMatrix);
                bounds.Inflate(4, 4);
                if (bounds.Contains(currentLocation))
                {
                    currentAnnotationBounds = bounds;
                    OnTouchAnnotation(annotation, e);
                    return;
                }
            }
            if (e.ActionType == SKTouchAction.Released)
            {
                SelectedAnnotation = null;
            }
            else if (e.ActionType == SKTouchAction.Pressed)
            {
                TextSelection.Clear();
                OnTextSelectionChanged();
            }
            Cursor = CursorType.Arrow;
            currentAnnotation = null;
            CurrentAnnotationText = null;
            HoverPoint = null;
        }

        private bool OnTouchText(PdfPagePicture picture, SKTouchEventArgs e)
        {
            var textSelectionChanged = false;
            var page = picture.Page;
            foreach (var textString in page.Strings)
            {
                foreach (var textChar in textString.TextChars)
                {
                    if (textChar.Quad.Contains(currentPointerLocation))
                    {
                        Cursor = CursorType.IBeam;
                        if (e.ActionType == SKTouchAction.Pressed && e.MouseButton == SKMouseButton.Left)
                        {
                            TextSelection.Clear();
                            startSelectionChar = textChar;
                            TextSelection.Add(textChar);
                            OnTextSelectionChanged();
                        }
                        textSelectionChanged = true;
                        break;
                    }
                }
            }
            if (e.ActionType == SKTouchAction.Moved && e.MouseButton == SKMouseButton.Left && startSelectionChar != null)
            {
                var firstCharIndex = startSelectionChar.TextString.TextChars.IndexOf(startSelectionChar);
                var firstString = startSelectionChar.TextString;
                var firstStringIndex = page.Strings.IndexOf(firstString);
                var firstMiddle = startSelectionChar.Quad.Middle.Value;
                var line = new SKLine(firstMiddle, currentPointerLocation);
                TextSelection.Clear();
                if (startSelectionChar.Quad.Contains(line.a) && startSelectionChar.Quad.Contains(line.b))
                {
                    TextSelection.Add(startSelectionChar);
                }
                else
                {
                    for (int i = firstStringIndex < 1 ? 0 : firstStringIndex - 1; i < page.Strings.Count; i++)
                    {
                        var textString = page.Strings[i];
                        foreach (var textChar in textString.TextChars)
                        {
                            if (SKLine.FindIntersection(line, textChar.Quad, true) != null)
                            {
                                TextSelection.Add(textChar);
                            }
                            else if (TextSelection.Count > 1)
                            {
                                break;
                            }
                        }
                    }
                }
                OnTextSelectionChanged();
                textSelectionChanged = true;
            }
            if (e.ActionType == SKTouchAction.Released)
            {
                startSelectionChar = null;
            }

            return textSelectionChanged;
        }

        private void OnTextSelectionChanged()
        {
            selectedQuad = null;
            selectedString = null;
            InvalidateSurface();
            TextSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTouchPointAdd(SKTouchEventArgs e)
        {
            var annotation = SelectedAnnotation as VertexShape;
            if (e.ActionType == SKTouchAction.Moved)
            {
                CurrentPoint.Point = invertPictureMatrix.MapPoint(e.Location);
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                if (annotation.IsNew)
                {
                    var rect = new SKRect(annotation.FirstPoint.X - 5, annotation.FirstPoint.Y - 5, annotation.FirstPoint.X + 5, annotation.FirstPoint.Y + 5);
                    if (annotation.Points.Length > 2 && rect.Contains(annotation.LastPoint))
                    {
                        //EndOperation(operationLink.Value);
                        //BeginOperation(annotation, OperationType.PointRemove, CurrentPoint);
                        //annotation.RemovePoint(annotation.Points.Length - 1);
                        CloseVertextShape(annotation);
                    }
                    else
                    {
                        EndOperation(operationLink.Value);
                        CurrentPoint = annotation.AddPoint(invertPictureMatrix.MapPoint(e.Location));
                        BeginOperation(annotation, OperationType.PointAdd, CurrentPoint);
                        return;
                    }

                }

                CurrentOperation = OperationType.None;
            }

            InvalidateSurface();
        }

        private void OnTouchPointMove(SKTouchEventArgs e)
        {
            var annotation = SelectedAnnotation;
            if (e.ActionType == SKTouchAction.Moved)
            {
                if (e.MouseButton == SKMouseButton.Left)
                {
                    CurrentPoint.Point = invertPictureMatrix.MapPoint(e.Location);
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                //CurrentPoint.Point = invertPictureMatrix.MapPoint(e.Location);

                CurrentOperation = OperationType.None;
            }

            InvalidateSurface();
        }

        private void CloseVertextShape(VertexShape vertexShape)
        {
            vertexShape.IsNew = false;
            vertexShape.RefreshBox();
            CurrentPoint = null;
            CurrentOperation = OperationType.None;
        }

        private void OnTouchSized(SKTouchEventArgs e)
        {
            if (pressedCursorLocation == null)
                return;

            var annotation = SelectedAnnotation;
            if (e.ActionType == SKTouchAction.Moved)
            {
                var bound = annotation.GetBounds(currentPictureMatrix);
                bound.Size += new SKSize(e.Location - pressedCursorLocation.Value);

                annotation.MoveTo(invertPictureMatrix.MapRect(bound));

                pressedCursorLocation = e.Location;
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                CurrentOperation = OperationType.None;
            }
            InvalidateSurface();

        }

        private void OnTouchDragged(SKTouchEventArgs e)
        {
            var annotation = SelectedAnnotation;
            if (e.ActionType == SKTouchAction.Moved)
            {
                if (currentPicture.Page != annotation.Page)
                {
                    EndOperation(operationLink.Value);
                    BeginOperation(annotation, OperationType.AnnotationRePage, nameof(Annotation.Page), annotation.Page.Index, currentPicture.Page.Index);
                    annotation.Page = currentPicture.Page;
                    BeginOperation(annotation, OperationType.AnnotationDrag, nameof(Annotation.Box));
                    pressedCursorLocation = null;
                }
                var bound = annotation.GetBounds(currentPictureMatrix);
                if (bound.Width == 0)
                    bound.Right = bound.Left + 1;
                if (bound.Height == 0)
                    bound.Bottom = bound.Top + 1;
                if (pressedCursorLocation == null || e.MouseButton == SKMouseButton.Unknown)
                {
                    bound.Location = e.Location;
                }
                else
                {
                    bound.Location += e.Location - pressedCursorLocation.Value;
                    pressedCursorLocation = e.Location;
                }
                annotation.MoveTo(invertPictureMatrix.MapRect(bound));

                InvalidateSurface();
            }
            else if (e.ActionType == SKTouchAction.Pressed)
            {
                pressedCursorLocation = e.Location;
                if (!annotation.IsNew)
                    return;
                annotation.IsNew = false;
                if (annotation is StickyNote sticky)
                {
                    CurrentOperation = OperationType.None;
                }
                else if (annotation is Line line)
                {
                    line.StartPoint = invertPictureMatrix.MapPoint(e.Location);
                    CurrentPoint = line.GetControlPoints().OfType<LineEndControlPoint>().FirstOrDefault();
                    CurrentOperation = OperationType.PointMove;
                }
                else if (annotation is VertexShape vertexShape)
                {
                    annotation.IsNew = true;
                    vertexShape.FirstPoint = invertPictureMatrix.MapPoint(e.Location);
                    CurrentPoint = vertexShape.FirstControlPoint;
                    CurrentOperation = OperationType.PointAdd;
                }
                else if (annotation is Shape shape)
                {
                    CurrentOperation = OperationType.AnnotationSize;
                }
                else if (annotation is FreeText freeText)
                {
                    if (freeText.Line == null)
                    {
                        CurrentOperation = OperationType.AnnotationSize;
                    }
                    else
                    {
                        freeText.Line.Start = invertPictureMatrix.MapPoint(e.Location);
                        CurrentPoint = annotation.GetControlPoints().OfType<TextMidControlPoint>().FirstOrDefault();
                        CurrentOperation = OperationType.PointMove;
                    }
                }
                else
                {
                    var controlPoint = annotation.GetControlPoints().OfType<BottomRightControlPoint>().FirstOrDefault();
                    if (controlPoint != null)
                    {
                        CurrentPoint = controlPoint;
                        CurrentOperation = OperationType.PointMove;
                    }
                    else
                    {
                        CurrentOperation = OperationType.AnnotationSize;
                    }
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                pressedCursorLocation = null;
                var picture = GetPicture(annotation.Page);
                if (annotation.IsNew && annotation is VertexShape vertexShape)
                {
                    return;
                }
                else
                {
                    CurrentOperation = OperationType.None;
                }
            }
        }

        private void OnTouchAnnotation(Annotation annotation, SKTouchEventArgs e)
        {
            currentAnnotation = annotation;

            if (e.ActionType == SKTouchAction.Moved)
            {
                CurrentAnnotationText = currentAnnotation.Text;
                if (e.MouseButton == SKMouseButton.Left)
                {
                    if (SelectedAnnotation != null && pressedCursorLocation != null && Cursor == CursorType.Hand)
                    {
                        if (CurrentOperation == OperationType.None)
                        {
                            var dif = SKPoint.Distance(e.Location, pressedCursorLocation.Value);
                            if (Math.Abs(dif) > 5)
                            {
                                CurrentOperation = OperationType.AnnotationDrag;
                            }
                        }
                    }
                }
                else if (e.MouseButton == SKMouseButton.Unknown)
                {
                    if (annotation == SelectedAnnotation)
                    {
                        foreach (var controlPoint in annotation.GetControlPoints())
                        {
                            if (currentPictureMatrix.MapRect(controlPoint.Bounds).Contains(e.Location))
                            {
                                Cursor = CursorType.Cross;
                                HoverPoint = controlPoint;
                                return;
                            }
                        }
                    }
                    HoverPoint = null;
                    var rect = new SKRect(
                        currentAnnotationBounds.Right - 10,
                        currentAnnotationBounds.Bottom - 10,
                        currentAnnotationBounds.Right,
                        currentAnnotationBounds.Bottom);
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
                    pressedCursorLocation = e.Location;
                    SelectedAnnotation = currentAnnotation;
                    if (annotation == SelectedAnnotation && HoverPoint != null)
                    {
                        CurrentPoint = HoverPoint;
                        CurrentOperation = OperationType.PointMove;
                    }
                    else if (Cursor == CursorType.SizeNWSE)
                    {
                        CurrentOperation = OperationType.AnnotationSize;
                    }
                }
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                if (e.MouseButton == SKMouseButton.Left)
                {
                    pressedCursorLocation = null;
                }
            }
        }

        public PdfPagePicture GetPicture(Documents.Page page)
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
                    var unscaleLocations = new SKPoint(currentLocation.X / XScaleFactor, currentLocation.Y / YScaleFactor);
                    currentNavigationMatrix.TryInvert(out var oldInvertMatrix);
                    var oldSpacePoint = oldInvertMatrix.MapPoint(unscaleLocations);

                    ScaleContent = newSclae;

                    var newCurrentLocation = currentNavigationMatrix.MapPoint(oldSpacePoint);

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
                details.Matrix = details.Matrix.PreConcat(SKMatrix.CreateTranslation(indent, totalHeight));
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

        public void ScrollTo(Annotation annotation)
        {
            if (annotation?.Page == null)
            {
                return;
            }

            var picture = GetPicture(annotation.Page);
            if (picture == null)
            {
                return;
            }
            var matrix = SKMatrix.MakeIdentity()
                .PreConcat(SKMatrix.MakeScale(scale, scale))
                .PreConcat(picture.Matrix);
            var bound = annotation.GetBounds(matrix);
            var top = bound.Top - (currentArea.MidY / XScaleFactor - bound.Height / 2);
            var left = bound.Left - (currentArea.MidX / YScaleFactor - bound.Width / 2);
            AnimateScroll(Math.Max(top, 0), Math.Max(left, 0));
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
            currentArea = SKRect.Create(0, 0, (float)Width * XScaleFactor, (float)Height * YScaleFactor);

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
            currentNavigationMatrix = new SKMatrix(
                scale, 0, ((float)-HorizontalValue) + dx,
                0, scale, ((float)-VerticalValue) + dy,
                0, 0, 1);

            currentViewMatrix = currentWindowScaleMatrix;
            currentViewMatrix = currentViewMatrix.PreConcat(currentNavigationMatrix);
        }

        private void ClearPictures()
        {
            foreach (var picture in pictures)
            {
                picture.Dispose();
            }
            pictures.Clear();
        }

        private void EndOperation(OperationEntry operation)
        {
            var annotation = operation.Annotation;
            var property = operation.Property;
            var type = operation.Type;
            if (type == OperationType.AnnotationDrag
                || type == OperationType.AnnotationSize)
            {
                operation.NewValue = annotation.Box;
            }
            if (property is ControlPoint controlPoint)
            {
                operation.NewValue = controlPoint.Point;
            }
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        private void BeginOperation(Annotation annotation, OperationType type, object property = null, object begin = null, object end = null)
        {
            var operation = new OperationEntry
            {
                Annotation = annotation,
                Type = type,
                Property = property,
                OldValue = begin,
                NewValue = end
            };
            if (type == OperationType.AnnotationDrag
                || type == OperationType.AnnotationSize)
            {
                operation.OldValue = annotation.Box;
            }
            if (property is ControlPoint controlPoint)
            {
                operation.OldValue = controlPoint.Point;
            }
            if (operationLink == null)
            {
                operations.Clear();
                operationLink = operations.AddFirst(operation);
            }
            else
            {
                var next = operationLink;
                while (next?.Next != null)
                {
                    next = next.Next;
                }
                while (next != operationLink)
                {
                    next = next.Previous;
                    operations.Remove(next.Next);
                }
                operationLink = operations.AddAfter(operationLink, operation);
            }


        }

        public void Redo()
        {
            var operation = operationLink == null ? operations.First : operationLink?.Next;
            if (operation != null)
            {
                operationLink = operation;
                var entry = operationLink.Value;
                try
                {
                    handlePropertyChanged = false;
                    if (entry.Annotation.Page?.Annotations.Contains(entry.Annotation) ?? false)
                    {
                        SelectedAnnotation = entry.Annotation;
                    }
                    switch (entry.Type)
                    {
                        case OperationType.AnnotationAdd:
                            AddAnnotation(entry.Annotation, true);
                            break;
                        case OperationType.AnnotationRemove:
                            RemoveAnnotation(entry.Annotation, true);
                            break;
                        case OperationType.AnnotationDrag:
                            entry.Annotation.MoveTo((SKRect)entry.NewValue);
                            break;
                        case OperationType.AnnotationSize:
                            entry.Annotation.Box = (SKRect)entry.NewValue;
                            break;
                        case OperationType.AnnotationRePage:
                            var page = Document.Pages[(int)entry.NewValue];
                            entry.Annotation.Page = page;
                            break;
                        case OperationType.AnnotationColor:
                            entry.Annotation.SKColor = (SKColor)entry.NewValue;
                            break;
                        case OperationType.AnnotationText:
                            entry.Annotation.Text = (string)entry.NewValue;
                            break;
                        case OperationType.AnnotationSubject:
                            entry.Annotation.Subject = (string)entry.NewValue;
                            break;
                        case OperationType.PointMove:
                            {
                                if (entry.Property is ControlPoint controlPoint)
                                {
                                    controlPoint.Point = (SKPoint)entry.NewValue;
                                }
                                break;
                            }
                        case OperationType.PointAdd:
                            {
                                if (entry.Property is IndexControlPoint controlPoint)
                                {
                                    if (entry.Annotation is VertexShape vertexShape)
                                    {
                                        vertexShape.InsertPoint(controlPoint.Index, (SKPoint)entry.NewValue);
                                    }
                                }
                            }
                            break;
                        case OperationType.PointRemove:
                            {
                                if (entry.Property is IndexControlPoint controlPoint)
                                {
                                    if (entry.Annotation is VertexShape vertexShape)
                                    {
                                        vertexShape.RemovePoint(controlPoint.Index);
                                    }
                                }
                            }
                            break;
                    }
                    if (entry.Annotation.Page?.Annotations.Contains(entry.Annotation) ?? false)
                    {
                        SelectedAnnotation = entry.Annotation;
                    }
                }
                finally
                {
                    handlePropertyChanged = true;
                }
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }



        public void Undo()
        {
            if (operationLink != null)
            {
                var entry = operationLink.Value;
                operationLink = operationLink.Previous;
                try
                {
                    handlePropertyChanged = false;
                    if (entry.Annotation.Page?.Annotations.Contains(entry.Annotation) ?? false)
                    {
                        SelectedAnnotation = entry.Annotation;
                    }
                    switch (entry.Type)
                    {
                        case OperationType.AnnotationAdd:
                            RemoveAnnotation(entry.Annotation, true);
                            break;
                        case OperationType.AnnotationRemove:
                            AddAnnotation(entry.Annotation, true);
                            break;
                        case OperationType.AnnotationDrag:
                            entry.Annotation.MoveTo((SKRect)entry.OldValue);
                            break;
                        case OperationType.AnnotationSize:
                            entry.Annotation.Box = (SKRect)entry.OldValue;
                            break;
                        case OperationType.AnnotationRePage:
                            var page = Document.Pages[(int)entry.OldValue];
                            entry.Annotation.Page = page;
                            break;
                        case OperationType.AnnotationColor:
                            entry.Annotation.SKColor = (SKColor)entry.OldValue;
                            break;
                        case OperationType.AnnotationText:
                            entry.Annotation.Text = (string)entry.OldValue;
                            break;
                        case OperationType.AnnotationSubject:
                            entry.Annotation.Subject = (string)entry.OldValue;
                            break;
                        case OperationType.PointMove:
                            {
                                if (entry.Property is ControlPoint controlPoint)
                                {
                                    controlPoint.Point = (SKPoint)entry.OldValue;
                                    InvalidateSurface();
                                }
                                break;
                            }
                        case OperationType.PointAdd:
                            {
                                if (entry.Property is IndexControlPoint controlPoint)
                                {
                                    if (entry.Annotation is VertexShape vertexShape)
                                    {
                                        vertexShape.RemovePoint(controlPoint.Index);
                                        InvalidateSurface();
                                    }
                                }
                            }
                            break;
                        case OperationType.PointRemove:
                            {
                                if (entry.Property is IndexControlPoint controlPoint)
                                {
                                    if (entry.Annotation is VertexShape vertexShape)
                                    {
                                        vertexShape.InsertPoint(controlPoint.Index, (SKPoint)entry.NewValue);
                                        InvalidateSurface();
                                    }
                                }
                            }
                            break;
                    }
                    if (entry.Annotation.Page?.Annotations.Contains(entry.Annotation) ?? false)
                    {
                        SelectedAnnotation = entry.Annotation;
                    }
                }
                finally
                {
                    handlePropertyChanged = true;
                }
                OnPropertyChanged(nameof(CanUndo));
                OnPropertyChanged(nameof(CanRedo));
            }
        }

        public void AddAnnotation(Annotation annotation, bool log = false)
        {
            if (!log)
            {
                BeginOperation(annotation, OperationType.AnnotationAdd);
            }
            if (annotation.Page != null)
            {
                annotation.Page = annotation.Page;
                if (!annotation.Page.Annotations.Contains(annotation))
                {
                    annotation.Page.Annotations.Add(annotation);
                }

                foreach (var item in annotation.Replies)
                {
                    if (item is Markup markup)
                    {
                        AddAnnotation(item);
                    }
                }
            }
            if (annotation is Popup popup
                && popup.Markup != null)
            {
                AddAnnotation(popup.Markup);
            }
            AnnotationAdded?.Invoke(this, new AnnotationEventArgs(annotation));
            InvalidateSurface();
        }

        public IEnumerable<Annotation> RemoveAnnotation(Annotation annotation, bool log = false)
        {
            if (!log)
            {
                BeginOperation(annotation, OperationType.AnnotationRemove);
            }
            var list = new List<Annotation>();
            if (annotation.Page != null)
            {
                foreach (var item in annotation.Page.Annotations.ToList())
                {
                    if (item is Markup markup
                        && markup.InReplyTo == annotation)//&& markup.ReplyType == Markup.ReplyTypeEnum.Thread
                    {
                        annotation.Replies.Add(item);
                        foreach (var deleted in RemoveAnnotation(markup))
                        {
                            list.Add(deleted);
                        }
                    }
                }
            }
            if (annotation == SelectedAnnotation)
                SelectedAnnotation = null;
            annotation.Remove();
            IsChanged = true;
            if (annotation is Popup popup)
            {
                RemoveAnnotation(popup.Markup);
                list.Add(popup.Markup);
            }
            list.Add(annotation);
            AnnotationRemoved?.Invoke(this, new AnnotationEventArgs(annotation));
            InvalidateSurface();
            return list;
        }
    }
}
