using PdfClown.Tools;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace PdfClown.Viewer
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
        private readonly SvgImage upSvg = SvgImage.GetCache(typeof(SKScrollView).Assembly, "caret-up");
        private readonly SvgImage downSvg = SvgImage.GetCache(typeof(SKScrollView).Assembly, "caret-down");
        private readonly SvgImage leftSvg = SvgImage.GetCache(typeof(SKScrollView).Assembly, "caret-left");
        private readonly SvgImage rightSvg = SvgImage.GetCache(typeof(SKScrollView).Assembly, "caret-right");
        private bool verticalHovered;
        private bool нorizontalHovered;
        private Thickness verticalPadding = new Thickness(0, 0, 0, step);
        private Thickness horizontalPadding = new Thickness(0, 0, step, 0);

        protected EventHandler<ScrollEventArgs> verticalScrolledHandler;
        protected EventHandler<ScrollEventArgs> нorizontalScrolledHandler;
        private float xScaleFactor = 1F;
        private float yScaleFactor = 1F;

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
            Color = SKColors.DarkGray.WithAlpha(180)
        };

        private SKPaint svgPaint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(SKColors.Black, SKBlendMode.SrcIn)
        };

        public SKScrollView()
        {
            IsTabStop = true;
            IgnorePixelScaling = false;
            EnableTouchEvents = true;
            Touch += OnTouch;
        }

        public CursorType Cursor
        {
            get => (CursorType)GetValue(CursorProperty);
            set => SetValue(CursorProperty, value);
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

        public Animation ScrollAnimation { get; protected set; }

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

        public float XScaleFactor
        {
            get => xScaleFactor;
            set
            {
                if (xScaleFactor != value)
                {
                    xScaleFactor = value;
                    OnWindowScaleChanged();
                }
            }
        }

        public float YScaleFactor
        {
            get => yScaleFactor;
            set
            {
                if (yScaleFactor != value)
                {
                    yScaleFactor = value;
                    OnWindowScaleChanged();
                }
            }
        }

        public bool WheelTouchSupported { get; set; } = true;

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(new SKTouchEventArgs(e.Id, e.ActionType, e.MouseButton, e.DeviceType,
                new SkiaSharp.SKPoint(e.Location.X / XScaleFactor, e.Location.Y / YScaleFactor),
                e.InContact));
            if (WheelTouchSupported && e.ActionType == SKTouchAction.WheelChanged)
            {
                OnScrolled(e.WheelDelta, KeyModifiers);
            }
        }

        protected virtual void OnWindowScaleChanged()
        {
        }

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
            var location = e.Location;// new SKPoint(e.Location.X / XScaleFactor, e.Location.Y / YScaleFactor);
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
                        VerticalHovered = VerticalScrollBarVisible && GetVerticalScrollBounds().Contains(location);
                        HorizontalHovered = HorizontalScrollBarVisible && GetHorizontalScrollBounds().Contains(location);
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
                        var newLocation = location - nullLocation;
                        if (nullDirection == StackOrientation.Vertical)
                        {
                            VerticalValue += newLocation.Y / kHeight;
                        }
                        else if (nullDirection == StackOrientation.Horizontal)
                        {
                            HorizontalValue += newLocation.X / kWidth;
                        }
                        nullLocation = location;
                        e.Handled = true;
                    }
                    break;
                case SKTouchAction.Pressed:
                    var verticalScrollBound = GetVerticalScrollBounds();
                    var нorizontalScrollBound = GetHorizontalScrollBounds();
                    if (verticalScrollBound.Contains(location))
                    {
                        e.Handled = true;
                        var upBound = GetUpBounds();
                        if (upBound.Contains(location))
                        {
                            VerticalValue -= step;
                            return;
                        }

                        var downBound = GetDownBounds();
                        if (downBound.Contains(location))
                        {
                            VerticalValue += step;
                            return;
                        }

                        var valueBound = GetVerticalValueBounds();
                        if (valueBound.Contains(location))
                        {
                            nullLocation = location;
                            nullDirection = StackOrientation.Vertical;
                            return;
                        }

                        VerticalValue = location.Y / kHeight - Height / 2;
                    }
                    if (нorizontalScrollBound.Contains(location))
                    {
                        e.Handled = true;
                        var leftBound = GetLeftBounds();
                        if (leftBound.Contains(location))
                        {
                            HorizontalValue -= step;
                            return;
                        }

                        var rightBound = GetRightBounds();
                        if (rightBound.Contains(location))
                        {
                            HorizontalValue += step;
                            return;
                        }

                        var valueBound = GetHorizontalValueBounds();
                        if (valueBound.Contains(location))
                        {
                            nullLocation = location;
                            nullDirection = StackOrientation.Horizontal;
                            return;
                        }

                        HorizontalValue = location.X / kWidth - Width / 2;
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
            canvas.Clear(BackgroundColor.ToSKColor());
            //canvas.Clear();

            base.OnPaintSurface(e);

            //if (!IsVisible)
            //    return;

            canvas.Save();
            OnPaintContent(e);
            canvas.Restore();

            if (VerticalScrollBarVisible)
            {
                canvas.DrawRect(GetVerticalScrollBounds(), scrollPaint);
                // if (Hovered)
                {
                    var upBound = GetUpBounds();
                    canvas.DrawRect(upBound, bottonPaint);
                    SvgImage.DrawImage(canvas, upSvg, svgPaint, upBound, 0.5F);

                    var downBound = GetDownBounds();
                    canvas.DrawRect(downBound, bottonPaint);
                    SvgImage.DrawImage(canvas, downSvg, svgPaint, downBound, 0.5F);
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
                    SvgImage.DrawImage(canvas, leftSvg, svgPaint, leftBound, 0.5F);

                    var rightBound = GetRightBounds();
                    canvas.DrawRect(rightBound, bottonPaint);
                    SvgImage.DrawImage(canvas, rightSvg, svgPaint, rightBound, 0.5F);
                }
                var valueBound = GetHorizontalValueBounds();
                valueBound.Inflate(-1, -1);
                canvas.DrawRect(valueBound, bottonPaint);
            }
            PaintOver?.Invoke(this, e);
        }

        protected virtual void OnPaintContent(SKPaintSurfaceEventArgs e)
        {
            PaintContent?.Invoke(this, e);
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

        protected void AnimateScroll(float top, double left)
        {
            if (ScrollAnimation != null)
            {
                this.AbortAnimation(ahScroll);
            }
            ScrollAnimation = new Animation();
            ScrollAnimation.Add(0, 1, new Animation(v => VerticalValue = v, VerticalValue, top, Easing.SinOut));
            ScrollAnimation.Add(0, 1, new Animation(v => HorizontalValue = v, HorizontalValue, left, Easing.SinOut));
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
            VerticalValue = VerticalValue - step * 2 * Math.Sign(delta);
            verticalScrolledHandler?.Invoke(this, new ScrollEventArgs(delta, keyModifiers));
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
}
