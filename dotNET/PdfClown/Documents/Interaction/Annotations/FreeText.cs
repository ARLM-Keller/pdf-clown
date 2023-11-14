/*
  Copyright 2008-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)

  This file should be part of the source code distribution of "PDF Clown library" (the
  Program): see the accompanying README files for more info.

  This Program is free software; you can redistribute it and/or modify it under the terms
  of the GNU Lesser General Public License as published by the Free Software Foundation;
  either version 3 of the License, or (at your option) any later version.

  This Program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
  either expressed or implied; without even the implied warranty of MERCHANTABILITY or
  FITNESS FOR A PARTICULAR PURPOSE. See the License for more details.

  You should have received a copy of the GNU Lesser General Public License along with this
  Program (see README files); if not, go to the GNU website (http://www.gnu.org/licenses/).

  Redistribution and use, with or without modification, are permitted provided that such
  redistributions retain the above copyright notice, license and disclaimer, along with
  this list of conditions.
*/

using PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Objects;
using PdfClown.Util;
using PdfClown.Util.Math.Geom;
using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Interaction.Annotations.ControlPoints;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Free text annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It displays text directly on the page. Unlike an ordinary text annotation, a free text
      annotation has no open or closed state; instead of being displayed in a pop-up window, the text
      is always visible.</remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class FreeText : Markup
    {
        /**
          <summary>Callout line [PDF:1.6:8.4.5].</summary>
        */
        public class CalloutLine : PdfObjectWrapper<PdfArray>
        {
            private SKPoint? pageEnd;
            private SKPoint? pageKnee;
            private SKPoint? pageStart;

            public CalloutLine(Page page, SKPoint start, SKPoint end)
                : this(page, start, null, end)
            { }

            public CalloutLine(Page page, SKPoint start, SKPoint? knee, SKPoint end)
                : base(new PdfArray())
            {
                SKMatrix matrix = page.InvertRotateMatrix;
                PdfArray baseDataObject = BaseDataObject;
                {
                    start = matrix.MapPoint(start);
                    baseDataObject.Add(PdfReal.Get(start.X));
                    baseDataObject.Add(PdfReal.Get(start.Y));
                    if (knee.HasValue)
                    {
                        knee = matrix.MapPoint(knee.Value);
                        baseDataObject.Add(PdfReal.Get(knee.Value.X));
                        baseDataObject.Add(PdfReal.Get(knee.Value.Y));
                    }
                    end = matrix.MapPoint(end);
                    baseDataObject.Add(PdfReal.Get(end.X));
                    baseDataObject.Add(PdfReal.Get(end.Y));
                }
            }

            public CalloutLine(PdfDirectObject baseObject) : base(baseObject)
            { }

            public SKPoint PageEnd
            {
                get
                {
                    return pageEnd ??= BaseDataObject is PdfArray coordinates
                        ? coordinates.Count < 6
                            ? new SKPoint(
                            coordinates.GetFloat(2),
                            coordinates.GetFloat(3))
                            : new SKPoint(
                            coordinates.GetFloat(4),
                            coordinates.GetFloat(5))
                       : SKPoint.Empty;
                }
                set
                {
                    pageEnd = value;
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                    {
                        coordinates[2] = PdfReal.Get(value.X);
                        coordinates[3] = PdfReal.Get(value.Y);
                    }
                    else
                    {
                        coordinates[4] = PdfReal.Get(value.X);
                        coordinates[5] = PdfReal.Get(value.Y);
                    }
                }
            }

            public SKPoint End
            {
                get => FreeText.PageMatrix.MapPoint(PageEnd);
                set
                {
                    PageEnd = FreeText.InvertPageMatrix.MapPoint(value);
                    FreeText.OnPropertyChanged(FreeText.Callout, FreeText.Callout, nameof(FreeText.Callout));
                    FreeText.RefreshBox();
                }
            }

            public SKPoint? PageKnee
            {
                get => pageKnee ??= BaseDataObject is PdfArray coordinates
                        ? coordinates.Count < 6
                            ? null
                            : new SKPoint(coordinates.GetFloat(2), coordinates.GetFloat(3))
                            : SKPoint.Empty;
                set
                {
                    pageKnee = value;
                    PdfArray coordinates = BaseDataObject;
                    if (value is SKPoint val)
                    {
                        coordinates[2] = PdfReal.Get(val.X);
                        coordinates[3] = PdfReal.Get(val.Y);
                    }
                }
            }

            public SKPoint? Knee
            {
                get
                {
                    var pageKnee = PageKnee;
                    return pageKnee == null ? null : FreeText.PageMatrix.MapPoint((SKPoint)pageKnee);
                }
                set
                {
                    PageKnee = FreeText.InvertPageMatrix.MapPoint(value.Value);
                    FreeText.OnPropertyChanged(FreeText.Callout, FreeText.Callout, nameof(FreeText.Callout));
                    //FreeText.RefreshBox();
                }
            }

            public SKPoint PageStart
            {
                get
                {
                    return pageStart ??= BaseDataObject is PdfArray coordinates
                        ? new SKPoint(
                      coordinates.GetFloat(0),
                      coordinates.GetFloat(1))
                        : SKPoint.Empty;
                }
                set
                {
                    pageStart = value;
                    PdfArray coordinates = BaseDataObject;
                    coordinates[0] = PdfReal.Get(value.X);
                    coordinates[1] = PdfReal.Get(value.Y);
                }
            }

            public SKPoint Start
            {
                get => FreeText.PageMatrix.MapPoint(PageStart);
                set
                {
                    PageStart = FreeText.InvertPageMatrix.MapPoint(value);
                    FreeText.OnPropertyChanged(FreeText.Callout, FreeText.Callout, nameof(FreeText.Callout));
                    FreeText.RefreshBox();
                }
            }

            public FreeText FreeText { get; internal set; }
        }


        private static readonly JustificationEnum DefaultJustification = JustificationEnum.Left;
        private TextTopLeftControlPoint cpTexcTopLeft;
        private TextTopRightControlPoint cpTexcTopRight;
        private TextBottomLeftControlPoint cpTexcBottomLeft;
        private TextBottomRightControlPoint cpTexcBottomRight;
        private TextLineStartControlPoint cpLineStart;
        private TextLineEndControlPoint cpLineEnd;
        private TextLineKneeControlPoint cpLineKnee;
        private TextMidControlPoint cpTextMid;
        private SKRect? textBox;
        private SKRect? pageTextBox;
        private bool allowRefresh = true;

        public FreeText(Page page, SKRect box, string text)
            : base(page, PdfName.FreeText, box, text)
        { }

        public FreeText(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets/Sets the justification to be used in displaying the annotation's text.</summary>
        */
        public JustificationEnum Justification
        {
            get => JustificationEnumExtension.Get((PdfInteger)BaseDataObject[PdfName.Q]);
            set
            {
                var oldValue = Justification;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.Q] = value != DefaultJustification ? value.GetCode() : null;
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        public PdfArray Callout
        {
            get => (PdfArray)BaseDataObject[PdfName.CL];
            set
            {
                var oldValue = Callout;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.CL] = value;
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        /**
          <summary>Gets/Sets the callout line attached to the free text annotation.</summary>
        */
        public CalloutLine Line
        {
            get
            {
                var line = Wrap<CalloutLine>(Callout);
                if (line != null)
                {
                    line.FreeText = this;
                }
                return line;
            }
            set
            {
                var oldValue = Line;
                Callout = value?.BaseDataObject;
                if (value != null)
                {
                    /*
                      NOTE: To ensure the callout would be properly rendered, we have to declare the
                      corresponding intent.
                    */
                    Intent = MarkupIntent.FreeTextCallout;
                    value.FreeText = this;
                }
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the style of the ending line ending.</summary>
        */
        public LineEndStyleEnum LineEndStyle
        {
            get => LineEndStyleEnumExtension.Get(BaseDataObject.GetString(PdfName.LE));
            set
            {
                var oldValue = LineEndStyle;
                if (oldValue != value)
                {
                    BaseDataObject.SetName(PdfName.LE, value.GetName());
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        /**
          <summary>Popups not supported.</summary>
        */
        public override Popup Popup
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public SKRect PageTextBox
        {
            get => pageTextBox ??= GetPageTextBox();
            set
            {
                pageTextBox = value;
                textBox = null;
                var bounds = Rect.ToRect();
                Padding = new Objects.Rectangle(new SKRect(
                    bounds.Left - value.Left,
                    bounds.Top - value.Top,
                    value.Right - bounds.Right,
                    value.Bottom - bounds.Bottom));
            }
        }

        private SKRect GetPageTextBox()
        {
            var bounds = Rect.ToRect();
            var padding = Padding?.ToRect();
            return new SKRect(
                bounds.Left + (float)(padding?.Left ?? 0D),
                bounds.Top + (float)(padding?.Top ?? 0D),
                bounds.Right - (float)(padding?.Right ?? 0D),
                bounds.Bottom - (float)(padding?.Bottom ?? 0D));
        }

        public SKRect TextBox
        {
            get
            {
                if (textBox == null)
                {
                    textBox = PageMatrix.MapRect(PageTextBox);
                }
                return textBox.Value;
            }
            set
            {
                if (TextBox != value)
                {
                    var oldValue = TextBox;
                    PageTextBox = InvertPageMatrix.MapRect(value);
                    textBox = value;
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        public Objects.Rectangle Padding
        {
            get => Wrap<Objects.Rectangle>(BaseDataObject[PdfName.RD]);
            set => BaseDataObject[PdfName.RD] = value?.BaseDataObject;
        }

        public SKPoint PageTextTopLeftPoint
        {
            get => new SKPoint(PageTextBox.Left, PageTextBox.Top);
            set
            {
                PageTextBox = new SKRect(value.X, value.Y, PageTextBox.Right, PageTextBox.Bottom);
                RefreshBox();
            }
        }

        public SKPoint TextTopLeftPoint
        {
            get => new SKPoint(TextBox.Left, TextBox.Top);
            set
            {
                TextBox = new SKRect(value.X, value.Y, TextBox.Right, TextBox.Bottom);
                RefreshBox();
            }
        }

        public SKPoint PageTextTopRightPoint
        {
            get => new SKPoint(PageTextBox.Right, PageTextBox.Top);
            set
            {
                PageTextBox = new SKRect(PageTextBox.Left, value.Y, value.X, PageTextBox.Bottom);
                RefreshBox();
            }
        }

        public SKPoint TextTopRightPoint
        {
            get => new SKPoint(TextBox.Right, TextBox.Top);
            set
            {
                TextBox = new SKRect(TextBox.Left, value.Y, value.X, TextBox.Bottom);
                RefreshBox();
            }
        }

        public SKPoint PageTextBottomLeftPoint
        {
            get => new SKPoint(PageTextBox.Left, PageTextBox.Bottom);
            set
            {
                PageTextBox = new SKRect(value.X, PageTextBox.Top, PageTextBox.Right, value.Y);
                RefreshBox();
            }
        }

        public SKPoint TextBottomLeftPoint
        {
            get => new SKPoint(TextBox.Left, TextBox.Bottom);
            set
            {
                TextBox = new SKRect(value.X, TextBox.Top, TextBox.Right, value.Y);
                RefreshBox();
            }
        }

        public SKPoint PageTextBottomRightPoint
        {
            get => new SKPoint(PageTextBox.Right, PageTextBox.Bottom);
            set
            {
                PageTextBox = new SKRect(PageTextBox.Left, PageTextBox.Top, value.X, value.Y);
                RefreshBox();
            }
        }

        public SKPoint TextBottomRightPoint
        {
            get => new SKPoint(TextBox.Right, TextBox.Bottom);
            set
            {
                TextBox = new SKRect(TextBox.Left, TextBox.Top, value.X, value.Y);
                RefreshBox();
            }
        }

        public SKPoint PageTextMidPoint
        {
            get => new SKPoint(PageTextBox.MidX, PageTextBox.MidY);
            set
            {
                var textBox = PageTextBox;
                var oldMid = new SKPoint(textBox.MidX, textBox.MidY);
                textBox.Location += value - oldMid;
                PageTextBox = textBox;
                RefreshBox();
            }
        }

        public SKPoint TextMidPoint
        {
            get => new SKPoint(TextBox.MidX, TextBox.MidY);
            set
            {
                var textBox = TextBox;
                var oldMid = new SKPoint(textBox.MidX, textBox.MidY);
                textBox.Location += value - oldMid;
                TextBox = textBox;
                RefreshBox();
            }
        }

        public override bool ShowToolTip => false;

        public override void DrawSpecial(SKCanvas canvas)
        {
            var textBounds = PageTextBox;
            var color = SKColor;

            using (var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill })
            {
                canvas.DrawRect(textBounds, paint);
            }
            using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke })
            {
                Border?.Apply(paint, BorderEffect);
                canvas.DrawRect(textBounds, paint);
                if (Intent == MarkupIntent.FreeTextCallout && Line != null)
                {
                    var line = Line;
                    using (var linePath = new SKPath())
                    {
                        linePath.MoveTo(Line.PageStart);
                        if (line.PageKnee is SKPoint point)
                            linePath.LineTo(point);
                        linePath.LineTo(Line.PageEnd);

                        //if (LineStartStyle == LineEndStyleEnum.Square)
                        //{
                        //    var normal = linePath[0] - linePath[1];
                        //    normal = SKPoint.Normalize(normal);
                        //    var half = new SKPoint(normal.X * 2.5F, normal.Y * 2.5F);
                        //    var temp = normal.X;
                        //    normal.X = 0 - normal.Y;
                        //    normal.Y = temp;
                        //    var p1 = new SKPoint(half.X + normal.X * 5, half.Y + normal.Y * 5);
                        //    var p2 = new SKPoint(half.X - normal.X * 5, half.Y - normal.Y * 5);
                        //}
                        canvas.DrawPath(linePath, paint);
                    }
                }
            }

            using (var paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.StrokeAndFill,
                TextSize = 11,
                IsAntialias = true
            })
            {
                canvas.DrawLines(Contents, textBounds, paint);
            }

        }

        public override void PageMoveTo(SKRect newBox)
        {
            allowRefresh = false;
            Appearance.Normal[null] = null;

            var oldBox = PageBox;
            var oldTextBox = PageTextBox;

            var dif = SKMatrix.CreateIdentity()
                .PreConcat(SKMatrix.CreateTranslation(newBox.MidX, newBox.MidY))
                .PreConcat(SKMatrix.CreateScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height))
                .PreConcat(SKMatrix.CreateTranslation(-oldBox.MidX, -oldBox.MidY));

            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                Line.PageStart = dif.MapPoint(Line.PageStart);
                Line.PageEnd = dif.MapPoint(Line.PageEnd);
                if (Line.PageKnee != null)
                    Line.PageKnee = dif.MapPoint(Line.PageKnee.Value);
            }
            base.PageMoveTo(newBox);
            PageTextBox = dif.MapRect(oldTextBox);
            allowRefresh = true;
        }

        public void CalcLine()
        {
            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                var textBox = PageTextBox;
                var textBoxInflate = SKRect.Inflate(textBox, 15, 15);
                var midpoint = PageTextMidPoint;
                var start = Line.PageStart;
                if (start.X > (textBox.Left - 5) && start.X < (textBox.Right + 5))
                {
                    if (start.Y < textBox.Top)
                    {
                        Line.PageEnd = new SKPoint(textBox.MidX, textBox.Top);
                        if (Line.PageKnee != null)
                        {
                            Line.PageKnee = new SKPoint(textBoxInflate.MidX, textBoxInflate.Top);
                        }
                    }
                    else
                    {
                        Line.PageEnd = new SKPoint(textBox.MidX, textBox.Bottom);
                        if (Line.PageKnee != null)
                        {
                            Line.PageKnee = new SKPoint(textBoxInflate.MidX, textBoxInflate.Bottom);
                        }
                    }
                }
                else if (start.X < textBox.Left)
                {
                    Line.PageEnd = new SKPoint(textBox.Left, textBox.MidY);
                    if (Line.PageKnee != null)
                    {
                        Line.PageKnee = new SKPoint(textBoxInflate.Left, textBoxInflate.MidY);
                    }
                }
                else
                {
                    Line.PageEnd = new SKPoint(textBox.Right, textBox.MidY);
                    if (Line.PageKnee != null)
                    {
                        Line.PageKnee = new SKPoint(textBoxInflate.Right, textBoxInflate.MidY);
                    }
                }
            }
        }

        public override void RefreshBox()
        {
            if (!allowRefresh)
                return;
            allowRefresh = false;
            Appearance.Normal[null] = null;
            var oldTextBox = PageTextBox;
            CalcLine();
            var box = SKRect.Create(PageTextTopLeftPoint, SKSize.Empty);
            box.Add(PageTextTopRightPoint);
            box.Add(PageTextBottomRightPoint);
            box.Add(PageTextBottomLeftPoint);
            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                box.Add(Line.PageStart);
                if (Line.PageKnee is SKPoint knee)
                    box.Add(knee);
                box.Add(Line.PageEnd);

            }
            PageBox = box;
            PageTextBox = oldTextBox;
            base.RefreshBox();
            allowRefresh = true;
        }

        public override IEnumerable<ControlPoint> GetControlPoints()
        {
            yield return cpTexcTopLeft ?? (cpTexcTopLeft = new TextTopLeftControlPoint { Annotation = this });
            yield return cpTexcTopRight ?? (cpTexcTopRight = new TextTopRightControlPoint { Annotation = this });
            yield return cpTexcBottomLeft ?? (cpTexcBottomLeft = new TextBottomLeftControlPoint { Annotation = this });
            yield return cpTexcBottomRight ?? (cpTexcBottomRight = new TextBottomRightControlPoint { Annotation = this });
            yield return cpTextMid ?? (cpTextMid = new TextMidControlPoint { Annotation = this });
            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                yield return cpLineStart ?? (cpLineStart = new TextLineStartControlPoint { Annotation = this });
                yield return cpLineEnd ?? (cpLineEnd = new TextLineEndControlPoint { Annotation = this });
                if (Line.Knee != null)
                {
                    yield return cpLineKnee ?? (cpLineKnee = new TextLineKneeControlPoint { Annotation = this });
                }
            }

            foreach (var cpBase in GetDefaultControlPoint())
            {
                yield return cpBase;
            }

        }

        public override object Clone(Cloner cloner)
        {
            var cloned = (FreeText)base.Clone(cloner);
            cloned.cpTexcTopLeft = null;
            cloned.cpTexcTopRight = null;
            cloned.cpTexcBottomLeft = null;
            cloned.cpTexcBottomRight = null;
            cloned.cpLineStart = null;
            cloned.cpLineEnd = null;
            cloned.cpLineKnee = null;
            cloned.cpTextMid = null;
            return cloned;
        }
    }

    public abstract class FreeTextControlPoint : ControlPoint
    {
        public FreeText FreeText => (FreeText)Annotation;
    }

    public class TextLineStartControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.Line.Start;
            set => FreeText.Line.Start = value;
        }
    }

    public class TextLineEndControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.Line.End;
            set => FreeText.Line.End = value;
        }
    }

    public class TextLineKneeControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.Line.Knee ?? SKPoint.Empty;
            set => FreeText.Line.Knee = value;
        }
    }

    public class TextTopLeftControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.TextTopLeftPoint;
            set => FreeText.TextTopLeftPoint = value;
        }
    }

    public class TextTopRightControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.TextTopRightPoint;
            set => FreeText.TextTopRightPoint = value;
        }
    }

    public class TextBottomLeftControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.TextBottomLeftPoint;
            set => FreeText.TextBottomLeftPoint = value;
        }
    }

    public class TextBottomRightControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.TextBottomRightPoint;
            set => FreeText.TextBottomRightPoint = value;
        }
    }

    public class TextMidControlPoint : FreeTextControlPoint
    {
        public override SKPoint Point
        {
            get => FreeText.TextMidPoint;
            set => FreeText.TextMidPoint = value;
        }
    }

    public static class DrawHelper
    {
        private static readonly string[] split = new string[] { "\r\n", "\n" };

        public static float DrawLines(this SKCanvas canvas, string text, SKRect textBounds, SKPaint paint)
        {
            var left = textBounds.Left + 5;
            var top = textBounds.Top + paint.FontSpacing;

            if (!string.IsNullOrEmpty(text))
            {
                foreach (var line in DrawHelper.GetLines(text.Trim(), textBounds, paint))
                {
                    if (line.Length > 0)
                    {
                        canvas.DrawText(line, left, top, paint);
                    }
                    top += paint.FontSpacing;
                }
            }

            return top;
        }

        public static IEnumerable<string> GetLines(string text, SKRect textBounds, SKPaint paint)
        {
            //var builder = new SKTextBlobBuilder();
            foreach (var line in text.Split(split, StringSplitOptions.None))
            {
                var count = line.Length == 0 ? 0 : (int)paint.BreakText(line, textBounds.Width);
                if (count == line.Length)
                    yield return line;
                else
                {

                    var index = 0;
                    while (true)
                    {
                        if (count == 0)
                        {
                            count = 1;
                        }

                        for (int i = (index + count) - 1; i > index; i--)
                        {
                            if (line[i] == ' ')
                            {
                                count = (i + 1) - index;
                                break;
                            }
                        }
                        yield return line.Substring(index, count);
                        index += count;
                        if (index >= line.Length)
                            break;
                        count = (int)paint.BreakText(line.Substring(index), textBounds.Width);
                    }
                }
            }
        }
    }
}