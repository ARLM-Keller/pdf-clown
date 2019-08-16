using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.ComponentModel;
using System.Windows;
using Xamarin.Forms.Platform.WPF;
using SKFormsView = SkiaSharp.Views.Forms.SKCanvasView;
using SKNativePaintSurfaceEventArgs = SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs;
using SKNativeView = SkiaSharp.Views.WPF.SKElement;

//https://forums.xamarin.com/discussion/139504/2d-graphics-for-xamarin-forms-apps-ios-android-uwp-wpf
namespace SkiaSharp.Views.Forms
{
    public abstract class SKCanvasViewRendererBase<TFormsView, TNativeView> : ViewRenderer<TFormsView, TNativeView>
        where TFormsView : SKFormsView
        where TNativeView : SKNativeView
    {

        private SKTouchHandler touchHandler;

        protected SKCanvasViewRendererBase()
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<TFormsView> e)
        {
            if (e.OldElement != null)
            {
                var oldController = (ISKCanvasViewController)e.OldElement;

                // unsubscribe from events
                oldController.SurfaceInvalidated -= OnSurfaceInvalidated;
                oldController.GetCanvasSize -= OnGetCanvasSize;
            }

            if (e.NewElement != null)
            {
                var newController = (ISKCanvasViewController)e.NewElement;

                // create the native view
                if (Control == null)
                {
                    var view = (TNativeView)new SKNativeView();
                    view.PaintSurface += OnPaintSurface;
                    view.Loaded += OnViewLoaded;
                    SetNativeControl(view);
                }
                touchHandler = new SKTouchHandler(
                    args => ((ISKCanvasViewController)Element).OnTouch(args),
                    (x, y) => GetScaledCoord(x, y));
                touchHandler.SetEnabled(Control, e.NewElement.EnableTouchEvents);// 
                // set the initial values
                Control.IgnorePixelScaling = e.NewElement.IgnorePixelScaling;

                // subscribe to events from the user
                newController.SurfaceInvalidated += OnSurfaceInvalidated;
                newController.GetCanvasSize += OnGetCanvasSize;

                // paint for the first time
                OnSurfaceInvalidated(newController, EventArgs.Empty);
            }

            base.OnElementChanged(e);
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            if (Element.Width > 0)
            {
                Control.Width = Element.Width;
                Control.Height = Element.Height;
            }
            Control.Loaded -= OnViewLoaded;
            Control.Focusable = Element.IsTabStop;
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == SKFormsView.IgnorePixelScalingProperty.PropertyName)
            {
                Control.IgnorePixelScaling = Element.IgnorePixelScaling;
            }
            else if (e.PropertyName == SKFormsView.EnableTouchEventsProperty.PropertyName)
            {
                touchHandler.SetEnabled(Control, Element.EnableTouchEvents);
            }
            else if (e.PropertyName == SKFormsView.IsTabStopProperty.PropertyName)
            {
                Control.Focusable = Element.IsTabStop;
            }
        }

        protected override void Dispose(bool disposing)
        {
            // detach all events before disposing
            var controller = (ISKCanvasViewController)Element;
            if (controller != null)
            {
                controller.SurfaceInvalidated -= OnSurfaceInvalidated;
                controller.GetCanvasSize -= OnGetCanvasSize;
            }

            var control = Control;
            if (control != null)
            {
                control.PaintSurface -= OnPaintSurface;
            }

            touchHandler.Detach(control);

            base.Dispose(disposing);
        }

        private void OnPaintSurface(object sender, SKNativePaintSurfaceEventArgs e)
        {
            var controller = Element as ISKCanvasViewController;

            // the control is being repainted, let the user know
            controller?.OnPaintSurface(new SKPaintSurfaceEventArgs(e.Surface, e.Info));
        }

        private void OnSurfaceInvalidated(object sender, EventArgs eventArgs)
        {
            // repaint the native control
            Control.InvalidateVisual();
        }

        // the user asked for the size
        private void OnGetCanvasSize(object sender, GetPropertyValueEventArgs<SKSize> e)
        {
            e.Value = Control?.CanvasSize ?? SKSize.Empty;
        }

        public SKPoint GetScaledCoord(double x, double y)
        {
            if (Element.IgnorePixelScaling)
            {
                return new SKPoint((float)x, (float)y);
            }
            else
            {
                return SKCanvasHelper.GetScaledCoord(Control, x, y);
            }
        }
    }

    public static class SKCanvasHelper
    {
        public static SKPoint GetScaledCoord(FrameworkElement control, double x, double y)
        {
            var matrix = control != null ? PresentationSource.FromVisual(control)?.CompositionTarget?.TransformToDevice : null;
            var xfactor = matrix?.M11 ?? 1D;
            var yfactor = matrix?.M22 ?? 1D;
            x = x * xfactor;
            y = y * yfactor;

            return new SKPoint((float)x, (float)y);
        }
    }
}