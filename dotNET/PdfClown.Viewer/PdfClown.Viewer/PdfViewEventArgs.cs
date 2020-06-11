using PdfClown.Documents.Interaction.Annotations;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;

namespace PdfClown.Viewer
{
    public partial class PdfView
    {
        public class PdfViewEventArgs : EventArgs
        {
            //Common
            public PdfView Viewer;
            public SKMatrix WindowScaleMatrix = SKMatrix.Identity;
            public SKMatrix NavigationMatrix = SKMatrix.Identity;
            public SKMatrix PageMatrix = SKMatrix.Identity;
            public SKMatrix InvertPageMatrix = SKMatrix.Identity;
            public SKMatrix ViewMatrix = SKMatrix.Identity;
            public SKRect Area;

            public PdfPageView PageView;

            //Touch
            public SKTouchEventArgs TouchEvent;
            public SKPoint PointerLocation;
            public SKPoint MoveLocation;
            public SKPoint PagePointerLocation;
            public SKPoint? PressedLocation;
            public Annotation Annotation;
            public SKRect AnnotationBounds;
            public SKRect AnnotationTextBounds;
            //Draw
            public SKCanvas Canvas;
            public Annotation DrawAnnotation;

            private string annotationText;
            public string AnnotationText
            {
                get => annotationText;
                set
                {
                    if (annotationText != value)
                    {
                        annotationText = value;
                        if (!string.IsNullOrEmpty(annotationText))
                        {
                            var temp = new SKRect();
                            Viewer.paintText.MeasureText(annotationText, ref temp);
                            temp.Inflate(10, 5);
                            AnnotationTextBounds = SKRect.Create(
                                AnnotationBounds.Left / Viewer.XScaleFactor,
                                AnnotationBounds.Bottom / Viewer.YScaleFactor,
                                temp.Width, temp.Height);
                        }
                        Viewer.InvalidateSurface();
                    }
                }
            }
        }
    }
}
