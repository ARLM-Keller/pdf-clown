using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace PDFClown.Viewer
{
    public class SKScrollView : SKCanvasView
    {
        public static readonly BindableProperty CursorProperty = BindableProperty.Create(nameof(Cursor), typeof(CursorType), typeof(SKScrollView), CursorType.Arrow,
            propertyChanged: (bindable, oldValue, newValue) => ((SKScrollView)bindable).OnCursorChanged((CursorType)oldValue, (CursorType)newValue));

        public static readonly BindableProperty VerticalMaximumProperty = BindableProperty.Create(nameof(VerticalMaximum), typeof(double), typeof(SKScrollView), 1D,
            propertyChanged: (bidable, oldValue, newValue) => ((SKScrollView)bidable).OnVerticalMaximumChanged((double)oldValue, (double)newValue));

        public static readonly BindableProperty HorizontalMaximumProperty = BindableProperty.Create(nameof(HorizontalMaximum), typeof(double), typeof(SKScrollView), 1D,
            propertyChanged: (bidable, oldValue, newValue) => ((SKScrollView)bidable).OnHorizontalMaximumChanged((double)oldValue, (double)newValue));

        public static readonly BindableProperty VerticalValueProperty = BindableProperty.Create(nameof(VerticalValue), typeof(double), typeof(SKScrollView), 0D,
            propertyChanged: (bidable, oldValue, newValue) => ((SKScrollView)bidable).OnVerticalValueChanged((double)oldValue, (double)newValue));
        public static readonly BindableProperty HorizontalValueProperty = BindableProperty.Create(nameof(HorizontalValue), typeof(double), typeof(SKScrollView), 0D,
            propertyChanged: (bidable, oldValue, newValue) => ((SKScrollView)bidable).OnHorizontalValueChanged((double)oldValue, (double)newValue));

        public static readonly BindableProperty TargetProperty = BindableProperty.Create(nameof(Target), typeof(SKScrollView), typeof(SKScrollView), null,
            propertyChanged: (bidable, oldValue, newValue) => ((SKScrollView)bidable).OnTargetChanged((SKScrollView)oldValue, (SKScrollView)newValue));

        public const int step = 16;
        private const string ahScroll = "VerticalScrollAnimation";
        private SKPoint nullLocation;
        private StackOrientation nullDirection = StackOrientation.Vertical;
        private double vHeight;
        private double vWidth;
        private double vsHeight;
        private double hsWidth;
        private double kWidth;
        private double kHeight;
        private SkiaSharp.Extended.Svg.SKSvg upSvg = SvgImage.GetCache("caret-up");
        private SkiaSharp.Extended.Svg.SKSvg downSvg = SvgImage.GetCache("caret-down");
        private SkiaSharp.Extended.Svg.SKSvg leftSvg = SvgImage.GetCache("caret-left");
        private SkiaSharp.Extended.Svg.SKSvg rightSvg = SvgImage.GetCache("caret-right");
        private bool verticalHovered;
        private bool нorizontalHovered;
        private Thickness verticalPadding = new Thickness(0, 0, 0, step);
        private Thickness horizontalPadding = new Thickness(0, 0, step, 0);

        protected EventHandler<ScrollEventArgs> verticalScrolledHandler;
        protected EventHandler<ScrollEventArgs> нorizontalScrolledHandler;
        public float XScaleFactor = 1F;
        public float YScaleFactor = 1F;

        private readonly SKPaint bottonPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = 1,
            IsAntialias = true,
            Color = SKColors.WhiteSmoke.WithAlpha(200)
        };

        private readonly SKPaint scrollPaint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = 1,
            IsAntialias = true,
            Color = SKColors.Silver.WithAlpha(200)
        };

        private SKPaint svgPaint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Black, SKBlendMode.SrcIn)
        };

        public SKScrollView()
        {
            IsTabStop = true;
            EnableTouchEvents = true;
            Touch += OnTouch;
        }


        public CursorType Cursor
        {
            get => (CursorType)GetValue(CursorProperty);
            set => SetValue(CursorProperty, value);
        }

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(new SKTouchEventArgs(e.Id, e.ActionType, e.MouseButton, e.DeviceType,
                new SkiaSharp.SKPoint(e.Location.X / XScaleFactor, e.Location.Y / YScaleFactor),
                e.InContact));
        }

        public event EventHandler<ScrollEventArgs> VerticalScrolled
        {
            add => verticalScrolledHandler += value;
            remove => verticalScrolledHandler -= value;
        }

        public event EventHandler<ScrollEventArgs> HorizontalScrolled
        {
            add => нorizontalScrolledHandler += value;
            remove => нorizontalScrolledHandler -= value;
        }

        public event EventHandler<CanvasKeyEventArgs> KeyDown;

        public SKScrollView Target
        {
            get => (SKScrollView)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        public double VerticalMaximum
        {
            get => (double)GetValue(VerticalMaximumProperty);
            set => SetValue(VerticalMaximumProperty, value);
        }

        public double HorizontalMaximum
        {
            get => (double)GetValue(HorizontalMaximumProperty);
            set => SetValue(HorizontalMaximumProperty, value);
        }

        public double VerticalValue
        {
            get => (double)GetValue(VerticalValueProperty);
            set
            {
                var max = VerticalMaximum - Height + step;
                value = value < 0 || max < 0 ? 0 : value > max ? max : value;

                SetValue(VerticalValueProperty, value);
            }
        }

        public double HorizontalValue
        {
            get => (double)GetValue(HorizontalValueProperty);
            set
            {
                var max = HorizontalMaximum - Width + step;
                value = value < 0 || max < 0 ? 0 : value > max ? max : value;

                SetValue(HorizontalValueProperty, value);
            }
        }

        public KeyModifiers KeyModifiers { get; set; }

        public Animation ScrollAnimation { get; private set; }

        public double VericalSize => VerticalHovered ? 14 : 7;

        public double HorizontalSize => HorizontalHovered ? 14 : 7;

        public Thickness VerticalPadding
        {
            get => verticalPadding;
            set
            {
                if (verticalPadding != value)
                {
                    verticalPadding = value;
                    OnVerticalMaximumChanged(1, VerticalMaximum);
                }
            }
        }

        public Thickness HorizontalPadding
        {
            get => horizontalPadding;
            set
            {
                if (horizontalPadding != value)
                {
                    horizontalPadding = value;
                    OnHorizontalMaximumChanged(1, HorizontalMaximum);
                }
            }
        }

        public bool VerticalHovered
        {
            get => verticalHovered;
            set
            {
                if (verticalHovered != value)
                {
                    verticalHovered = value;
                    OnPropertyChanged();
                    OnVerticalMaximumChanged(1, VerticalMaximum);
                    InvalidateSurface();
                }
            }
        }

        public bool HorizontalHovered
        {
            get => нorizontalHovered;
            set
            {
                if (нorizontalHovered != value)
                {
                    нorizontalHovered = value;
                    OnPropertyChanged();
                    OnHorizontalMaximumChanged(1, HorizontalMaximum);
                    InvalidateSurface();
                }
            }
        }

        public event EventHandler<SKPaintSurfaceEventArgs> PaintContent;

        public event EventHandler<SKPaintSurfaceEventArgs> PaintOver;

        public bool VerticalScrollBarVisible => VerticalMaximum >= (Height + step);

        public bool HorizontalScrollBarVisible => HorizontalMaximum >= (Width + step);

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            OnVerticalMaximumChanged(1, VerticalMaximum);
            OnHorizontalMaximumChanged(1, HorizontalMaximum);
        }

        private void OnVerticalMaximumChanged(double oldValue, double newValue)
        {
            vsHeight = (Height - verticalPadding.VerticalThickness) - VericalSize * 2;
            kHeight = vsHeight / VerticalMaximum;
            vHeight = (float)((Height - step) * kHeight);

            if (vHeight < VericalSize)
            {
                vHeight = VericalSize;
                vsHeight = (Height - verticalPadding.VerticalThickness) - (VericalSize * 2 + vHeight);
                kHeight = vsHeight / VerticalMaximum;
            }

            VerticalValue = VerticalValue;
        }

        private void OnHorizontalMaximumChanged(double oldValue, double newValue)
        {
            hsWidth = (Width - horizontalPadding.HorizontalThickness) - HorizontalSize * 2;
            kWidth = hsWidth / HorizontalMaximum;
            vWidth = (float)((Width - step) * kWidth);

            if (vWidth < HorizontalSize)
            {
                vWidth = HorizontalSize;
                hsWidth = (Width - horizontalPadding.HorizontalThickness) - (HorizontalSize * 2 + vWidth);
                kWidth = hsWidth / HorizontalMaximum;
            }

            HorizontalValue = HorizontalValue;
        }

        protected virtual void OnVerticalValueChanged(double oldValue, double newValue)
        {
            InvalidateSurface();
            verticalScrolledHandler?.Invoke(this, new ScrollEventArgs((int)newValue, KeyModifiers));
        }

        protected virtual void OnHorizontalValueChanged(double oldValue, double newValue)
        {
            InvalidateSurface();
            нorizontalScrolledHandler?.Invoke(this, new ScrollEventArgs((int)newValue, KeyModifiers));
        }

        private void OnTargetChanged(SKScrollView oldValue, SKScrollView newValue)
        {
            if (oldValue != null)
            {
                oldValue.VerticalScrolled -= OnTargetScrolled;
            }

            if (newValue != null)
            {
                newValue.VerticalScrolled += OnTargetScrolled;
            }
        }

        protected virtual void OnTouch(object sender, SKTouchEventArgs e)
        {
            switch (e.ActionType)
            {
                case SKTouchAction.Released:
                    if (nullLocation != SKPoint.Empty)
                    {
                        nullLocation = SKPoint.Empty;
                        e.Handled = true;
                    }
                    goto case SKTouchAction.Exited;
                case SKTouchAction.Exited:
                case SKTouchAction.Moved:
                    if (nullLocation == SKPoint.Empty)
                    {
                        VerticalHovered = VerticalScrollBarVisible && GetVerticalScrollBounds().Contains(e.Location);
                        HorizontalHovered = HorizontalScrollBarVisible && GetHorizontalScrollBounds().Contains(e.Location);
                    }
                    break;
            }

            if ((!VerticalScrollBarVisible && !HorizontalScrollBarVisible)
                || e.MouseButton != SKMouseButton.Left)
                return;
            switch (e.ActionType)
            {
                case SKTouchAction.Moved:
                    if (nullLocation != SKPoint.Empty)
                    {
                        var newLocation = e.Location - nullLocation;
                        if (nullDirection == StackOrientation.Vertical)
                        {
                            VerticalValue += newLocation.Y / kHeight;
                        }
                        else if (nullDirection == StackOrientation.Horizontal)
                        {
                            HorizontalValue += newLocation.X / kWidth;
                        }
                        nullLocation = e.Location;
                        e.Handled = true;
                    }
                    break;
                case SKTouchAction.Pressed:
                    var verticalScrollBound = GetVerticalScrollBounds();
                    var нorizontalScrollBound = GetHorizontalScrollBounds();
                    if (verticalScrollBound.Contains(e.Location))
                    {
                        e.Handled = true;
                        var upBound = GetUpBounds();
                        if (upBound.Contains(e.Location))
                        {
                            VerticalValue -= step;
                            return;
                        }

                        var downBound = GetDownBounds();
                        if (downBound.Contains(e.Location))
                        {
                            VerticalValue += step;
                            return;
                        }

                        var valueBound = GetVerticalValueBounds();
                        if (valueBound.Contains(e.Location))
                        {
                            nullLocation = e.Location;
                            nullDirection = StackOrientation.Vertical;
                            return;
                        }

                        VerticalValue = e.Location.Y / kHeight - Height / 2;
                    }
                    if (нorizontalScrollBound.Contains(e.Location))
                    {
                        e.Handled = true;
                        var leftBound = GetLeftBounds();
                        if (leftBound.Contains(e.Location))
                        {
                            HorizontalValue -= step;
                            return;
                        }

                        var rightBound = GetRightBounds();
                        if (rightBound.Contains(e.Location))
                        {
                            HorizontalValue += step;
                            return;
                        }

                        var valueBound = GetHorizontalValueBounds();
                        if (valueBound.Contains(e.Location))
                        {
                            nullLocation = e.Location;
                            nullDirection = StackOrientation.Horizontal;
                            return;
                        }

                        HorizontalValue = e.Location.X / kWidth - Width / 2;
                    }
                    break;
            }
        }

        private void OnTargetScrolled(object sender, ScrollEventArgs e)
        {
            OnScrolled(e.Delta, KeyModifiers.None);
        }

        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            XScaleFactor = (float)(e.Info.Width / Width);
            YScaleFactor = (float)(e.Info.Height / Height);

            canvas.Scale(XScaleFactor, YScaleFactor);
            canvas.Clear();


            base.OnPaintSurface(e);

            if (!IsVisible)
                return;

            canvas.Save();
            PaintContent?.Invoke(this, e);
            canvas.Restore();

            if (VerticalScrollBarVisible)
            {
                canvas.DrawRect(GetVerticalScrollBounds(), scrollPaint);
                // if (Hovered)
                {
                    var upBound = GetUpBounds();
                    canvas.DrawRect(upBound, bottonPaint);
                    var upMatrix = SvgImage.GetMatrix(upSvg, upBound, 0.5F);
                    canvas.DrawPicture(upSvg.Picture, ref upMatrix, svgPaint);

                    var downBound = GetDownBounds();
                    canvas.DrawRect(downBound, bottonPaint);
                    var downMatrix = SvgImage.GetMatrix(downSvg, downBound, 0.5F);
                    canvas.DrawPicture(downSvg.Picture, ref downMatrix, svgPaint);
                }
                var valueBound = GetVerticalValueBounds();
                valueBound.Inflate(-1, -1);
                canvas.DrawRect(valueBound, bottonPaint);
            }

            if (HorizontalScrollBarVisible)
            {
                canvas.DrawRect(GetHorizontalScrollBounds(), scrollPaint);
                // if (Hovered)
                {
                    var leftBound = GetLeftBounds();
                    canvas.DrawRect(leftBound, bottonPaint);
                    var leftMatrix = SvgImage.GetMatrix(leftSvg, leftBound, 0.5F);
                    canvas.DrawPicture(leftSvg.Picture, ref leftMatrix, svgPaint);

                    var rightBound = GetRightBounds();
                    canvas.DrawRect(rightBound, bottonPaint);
                    var rightMatrix = SvgImage.GetMatrix(downSvg, rightBound, 0.5F);
                    canvas.DrawPicture(rightSvg.Picture, ref rightMatrix, svgPaint);
                }
                var valueBound = GetHorizontalValueBounds();
                valueBound.Inflate(-1, -1);
                canvas.DrawRect(valueBound, bottonPaint);
            }
            PaintOver?.Invoke(this, e);
        }

        public SKRect GetVerticalScrollBounds()
        {
            return SKRect.Create((float)(Width - VericalSize),
                (float)verticalPadding.Top,
                (float)VericalSize,
                (float)(Height - verticalPadding.VerticalThickness));
        }

        public SKRect GetHorizontalScrollBounds()
        {
            return SKRect.Create((float)horizontalPadding.Left,
                (float)(Height - HorizontalSize),
                (float)(Width - horizontalPadding.HorizontalThickness),
                (float)HorizontalSize);
        }

        private SKRect GetUpBounds()
        {
            return SKRect.Create((float)(Width - VericalSize),
                (float)verticalPadding.Top,
                (float)VericalSize, (float)VericalSize);
        }

        private SKRect GetDownBounds()
        {
            return SKRect.Create((float)(Width - VericalSize),
                (float)(Height - (VericalSize + verticalPadding.Bottom)),
                (float)VericalSize, (float)VericalSize);
        }

        private SKRect GetLeftBounds()
        {
            return SKRect.Create((float)horizontalPadding.Left,
                (float)(Height - HorizontalSize),
                (float)HorizontalSize, (float)HorizontalSize);
        }

        private SKRect GetRightBounds()
        {
            return SKRect.Create((float)(Width - (HorizontalSize + horizontalPadding.Right)),
                (float)(Height - HorizontalSize),
                (float)HorizontalSize, (float)HorizontalSize);
        }

        private SKRect GetVerticalValueBounds()
        {
            var top = VericalSize + verticalPadding.Top + (float)(VerticalValue * kHeight);
            return SKRect.Create((float)(Width - VericalSize),
                (float)top, (float)VericalSize, (float)vHeight);
        }

        private SKRect GetHorizontalValueBounds()
        {
            var left = HorizontalSize + horizontalPadding.Left + (float)(HorizontalValue * kWidth);
            return SKRect.Create((float)left, (float)(Height - HorizontalSize), (float)vWidth, (float)HorizontalSize);
        }

        public void SetVerticalScrolledPosition(double top)
        {
            VerticalValue = top;
        }

        public void AnimateScroll(double newValue)
        {
            if (ScrollAnimation != null)
            {
                this.AbortAnimation(ahScroll);
            }
            ScrollAnimation = new Animation(v => VerticalValue = v, VerticalValue, newValue, Easing.SinOut);
            ScrollAnimation.Commit(this, ahScroll, 16, 270, finished: (d, f) => ScrollAnimation = null);
        }

        private void OnCursorChanged(CursorType oldValue, CursorType newValue)
        {
        }

        public virtual bool OnKeyDown(string keyName, KeyModifiers modifiers)
        {
            var args = new CanvasKeyEventArgs(keyName, modifiers);
            KeyDown?.Invoke(this, args);
            return args.Handled;
        }

        public virtual void OnScrolled(int delta, KeyModifiers keyModifiers)
        {
            if (keyModifiers == KeyModifiers.None)
            {
                VerticalValue = VerticalValue - step * Math.Sign(delta);
                verticalScrolledHandler?.Invoke(this, new ScrollEventArgs(delta, keyModifiers));
            }
        }

        public virtual bool ContainsCaptureBox(double x, double y)
        {
            var baseValue = CheckCaptureBox?.Invoke(x, y) ?? false;
            return baseValue
                || (VerticalScrollBarVisible && GetVerticalValueBounds().Contains((float)x, (float)y))
                || (HorizontalScrollBarVisible && GetHorizontalValueBounds().Contains((float)x, (float)y));
        }

        public Func<double, double, bool> CheckCaptureBox;
    }

    public enum CursorType
    {
        Arrow,
        SizeWE,
        SizeNS,
        Hand,
        Wait
    }

    [Flags]
    public enum KeyModifiers
    {
        None = 0,
        Alt = 1,
        Ctrl = 2,
        Shift = 4,
    }

    public static class VisualElementExtension
    {
        public static bool IsParentsVisible(this VisualElement visual)
        {
            var parent = visual.Parent as VisualElement;
            var flag = visual.IsVisible;
            while (flag && parent != null)
            {
                flag = parent.IsVisible;
                parent = parent.Parent as VisualElement;
            }
            return flag;
        }
    }
}
