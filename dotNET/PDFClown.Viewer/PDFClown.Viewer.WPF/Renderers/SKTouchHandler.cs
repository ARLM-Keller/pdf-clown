using System;
using System.Windows;
using System.Windows.Input;

//https://github.com/mono/SkiaSharp/blob/master/source/SkiaSharp.Views.Forms/SkiaSharp.Views.Forms.UWP/SKTouchHandler.cs
namespace SkiaSharp.Views.Forms
{
    internal class SKTouchHandler
    {
        private Action<SKTouchEventArgs> onTouchAction;
        private Func<double, double, SKPoint> scalePixels;

        public SKTouchHandler(Action<SKTouchEventArgs> onTouchAction, Func<double, double, SKPoint> scalePixels)
        {
            this.onTouchAction = onTouchAction;
            this.scalePixels = scalePixels;
        }

        public void SetEnabled(FrameworkElement view, bool enableTouchEvents)
        {
            if (view != null)
            {
                view.MouseEnter -= OnPointerEntered;
                view.MouseLeave -= OnPointerExited;
                view.MouseDown -= OnPointerPressed;
                view.MouseMove -= OnPointerMoved;
                view.MouseUp -= OnPointerReleased;
                view.MouseWheel -= OnPointerCancelled;
                if (enableTouchEvents)
                {
                    view.MouseEnter += OnPointerEntered;
                    view.MouseLeave += OnPointerExited;
                    view.MouseDown += OnPointerPressed;
                    view.MouseMove += OnPointerMoved;
                    view.MouseUp += OnPointerReleased;
                    view.MouseWheel += OnPointerCancelled;
                }
            }
        }

        public void Detach(FrameworkElement view)
        {
            // clean the view
            SetEnabled(view, false);

            // remove references
            onTouchAction = null;
            scalePixels = null;
        }

        private void OnPointerEntered(object sender, MouseEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Entered, args);
        }

        private void OnPointerExited(object sender, MouseEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Exited, args);
        }

        private void OnPointerPressed(object sender, MouseButtonEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Pressed, args);

            if (args.Handled)
            {
                var view = sender as FrameworkElement;
                //view.ManipulationMode = ManipulationModes.All;
                //view.CapturePointer(args.Pointer);
            }
        }

        private void OnPointerMoved(object sender, MouseEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Moved, args);
        }

        private void OnPointerReleased(object sender, MouseButtonEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Released, args);

            var view = sender as FrameworkElement;
            //view.ManipulationMode = ManipulationModes.System;
        }

        private void OnPointerCancelled(object sender, MouseWheelEventArgs args)
        {
            args.Handled = CommonHandler(sender, SKTouchAction.Cancelled, args);
        }

        private bool CommonHandler(object sender, SKTouchAction touchActionType, MouseEventArgs evt)
        {
            if (onTouchAction == null || scalePixels == null)
                return false;

            var view = sender as FrameworkElement;

            var pointerPoint = evt.MouseDevice.GetPosition(view);
            //var windowsPoint = view.PointToScreen(pointerPoint);
            var skPoint = scalePixels(pointerPoint.X, pointerPoint.Y);

            var mouse = GetMouseButton(evt);
            var device = GetTouchDevice(evt);

            var args = new SKTouchEventArgs(evt.Timestamp, touchActionType, mouse, device, skPoint, true);
            onTouchAction(args);
            return args.Handled;
        }

        public static SKTouchDeviceType GetTouchDevice(MouseEventArgs evt)
        {
            var device = SKTouchDeviceType.Touch;
            if (evt.Device == evt.StylusDevice)
            {
                device = SKTouchDeviceType.Pen;
            }
            else if (evt.Device == evt.MouseDevice)
            {
                device = SKTouchDeviceType.Mouse;
            }
            else
            {
                device = SKTouchDeviceType.Touch;
            }

            return device;
        }

        public static SKMouseButton GetMouseButton(MouseEventArgs args)
        {
            var mouse = SKMouseButton.Unknown;
            if (args is MouseButtonEventArgs margs)
            {
                if (margs.ChangedButton == MouseButton.Left)
                    mouse = SKMouseButton.Left;
                else if (margs.ChangedButton == MouseButton.Middle)
                    mouse = SKMouseButton.Middle;
                else if (margs.ChangedButton == MouseButton.Right)
                    mouse = SKMouseButton.Right;
            }
            else
            {
                if (args.LeftButton == MouseButtonState.Pressed)
                    mouse = SKMouseButton.Left;
                else if (args.MiddleButton == MouseButtonState.Pressed)
                    mouse = SKMouseButton.Middle;
                else if (args.RightButton == MouseButtonState.Pressed)
                    mouse = SKMouseButton.Right;
            }
            return mouse;
        }
    }
}
