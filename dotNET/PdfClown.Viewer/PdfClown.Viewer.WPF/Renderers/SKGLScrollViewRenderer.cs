using PdfClown.Viewer;
using PdfClown.Viewer.WPF;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using SkiaSharp.Views.WPF;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Xamarin.Forms.Platform.WPF;

[assembly: ExportRenderer(typeof(SKGLScrollView), typeof(SKGLScrollViewRenderer))]
namespace PdfClown.Viewer.WPF
{
    public class SKGLScrollViewRenderer : SKGLViewRenderer
    {
        private bool pressed;

        protected override void OnElementChanged(ElementChangedEventArgs<SKGLView> e)
        {
            base.OnElementChanged(e);
            if (e.OldElement is SKGLView)
            {
                e.NewElement.Touch -= OnElementTouch;
            }
            if (e.NewElement is SKGLView)
            {
                e.NewElement.Touch += OnElementTouch;
                if (Control != null)
                {
                    Control.Focusable = Element.IsTabStop;
                    Control.Loaded += OnControlLoaded;
                }
            }
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);
            if (string.Equals(e.PropertyName, SKScrollView.CursorProperty.PropertyName, StringComparison.Ordinal))
            {
                UpdateCursor();
            }
        }

        private void UpdateCursor()
        {
            if (Control != null && Element is SKGLScrollView canvas)
            {
                switch (canvas.Cursor)
                {
                    case CursorType.Arrow:
                        Control.Cursor = Cursors.Arrow;
                        break;
                    case CursorType.SizeWE:
                        Control.Cursor = Cursors.SizeWE;
                        break;
                    case CursorType.SizeNS:
                        Control.Cursor = Cursors.SizeNS;
                        break;
                    case CursorType.SizeNESW:
                        Control.Cursor = Cursors.SizeNESW;
                        break;
                    case CursorType.SizeNWSE:
                        Control.Cursor = Cursors.SizeNWSE;
                        break;
                    case CursorType.Hand:
                        Control.Cursor = Cursors.Hand;
                        break;
                    case CursorType.Wait:
                        Control.Cursor = Cursors.Wait;
                        break;
                    case CursorType.ScrollAll:
                        Control.Cursor = Cursors.ScrollAll;
                        break;
                    case CursorType.Cross:
                        Control.Cursor = Cursors.Cross;
                        break;
                }
            }
        }

        private void OnElementTouch(object sender, SKTouchEventArgs e)
        {
            if (e.ActionType == SKTouchAction.Released)
            {
                Element.Focus();
                Control.Focus();
            }
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            Control.Loaded -= OnControlLoaded;

            Control.PreviewKeyDown += OnControlKeyDown;
            Control.PreviewMouseLeftButtonDown += OnControlMouseLeftButtonDown;
            Control.PreviewMouseLeftButtonUp += OnControlMouseLeftButtonUp;
            Control.PreviewMouseMove += OnControlMouseMove;
        }

        private void OnControlMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var view = sender as FrameworkElement;
            var pointerPoint = e.MouseDevice.GetPosition(view);
            if (Element is SKGLScrollView canvas && canvas.ContainsCaptureBox(pointerPoint.X, pointerPoint.Y))
            {
                pressed = true;
                Mouse.Capture(Control);
            }
            ((SKGLScrollView)Element).KeyModifiers = GetModifiers();
        }

        private static KeyModifiers GetModifiers()
        {
            var keyModifiers = KeyModifiers.None;
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                keyModifiers |= KeyModifiers.Alt;
            }
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                keyModifiers |= KeyModifiers.Ctrl;
            }
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                keyModifiers |= KeyModifiers.Shift;
            }

            return keyModifiers;
        }

        private void OnControlMouseMove(object sender, MouseEventArgs e)
        {
            if (pressed)
            {
                RaiseTouch(sender, e, SKTouchAction.Moved);
            }
        }

        private void OnControlMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (pressed)
            {
                pressed = false;
                Mouse.Capture(null);
                e.Handled = true;
                RaiseTouch(sender, e, SKTouchAction.Released);
            }
        }

        private void RaiseTouch(object sender, MouseEventArgs e, SKTouchAction action)
        {
            var view = sender as FrameworkElement;
            var pointerPoint = e.MouseDevice.GetPosition(view);
            var skPoint = GetScaledCoord(pointerPoint.X, pointerPoint.Y);
            var args = new SKTouchEventArgs(e.Timestamp, action, SKMouseButton.Left, SKTouchDeviceType.Mouse, skPoint, true);
            ((ISKCanvasViewController)Element).OnTouch(args);
        }

        private void OnControlKeyDown(object sender, KeyEventArgs e)
        {
            if (Element is SKGLScrollView scrollView)
            {
                e.Handled = scrollView.OnKeyDown(e.Key.ToString(), GetModifiers());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Element != null)
                {
                    Element.Touch -= OnElementTouch;
                }
                if (Control != null)
                {
                    Control.Loaded -= OnControlLoaded;
                    Control.PreviewKeyDown -= OnControlKeyDown;
                    Control.PreviewMouseLeftButtonDown -= OnControlMouseLeftButtonDown;
                    Control.PreviewMouseLeftButtonUp -= OnControlMouseLeftButtonUp;
                    Control.PreviewMouseMove -= OnControlMouseMove;
                }
            }
            base.Dispose(disposing);
        }

        public SKPoint GetScaledCoord(double x, double y)
        {
            //if (Element.IgnorePixelScaling)
            {
                return new SKPoint((float)x, (float)y);
            }
            //else
            //{
            //    return SKCanvasHelper.GetScaledCoord(Control, x, y);
            //}
        }
    }
}
