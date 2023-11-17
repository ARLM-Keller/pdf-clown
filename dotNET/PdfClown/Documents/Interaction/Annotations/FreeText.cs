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
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Fonts;
using System.Xml.Linq;
using PdfClown.Documents.Contents.ColorSpaces;

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

            public SKPoint End
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
                    FreeText.OnPropertyChanged(FreeText.Callout, FreeText.Callout, nameof(FreeText.Callout));
                    FreeText.RefreshBox();
                }
            }

            public SKPoint? Knee
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
                    FreeText.OnPropertyChanged(FreeText.Callout, FreeText.Callout, nameof(FreeText.Callout));
                    FreeText.RefreshAppearance();
                }
            }

            public SKPoint Start
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

        public SKRect TextBox
        {
            get => textBox ??= GetTextBox();
            set
            {
                var oldValue = textBox;
                if (oldValue != value)
                {
                    textBox = value;
                    var bounds = Rect.ToRect();
                    Padding = new Objects.Rectangle(new SKRect(
                        bounds.Left - value.Left,
                        bounds.Top - value.Top,
                        value.Right - bounds.Right,
                        value.Bottom - bounds.Bottom));
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        private SKRect GetTextBox()
        {
            var bounds = Rect.ToRect();
            var padding = Padding?.ToRect();
            return new SKRect(
                bounds.Left + (float)(padding?.Left ?? 0D),
                bounds.Top + (float)(padding?.Top ?? 0D),
                bounds.Right - (float)(padding?.Right ?? 0D),
                bounds.Bottom - (float)(padding?.Bottom ?? 0D));
        }

        public Objects.Rectangle Padding
        {
            get => Wrap<Objects.Rectangle>(BaseDataObject[PdfName.RD]);
            set => BaseDataObject[PdfName.RD] = value?.BaseDataObject;
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

        public SKPoint TextTopRightPoint
        {
            get => new SKPoint(TextBox.Right, TextBox.Top);
            set
            {
                TextBox = new SKRect(TextBox.Left, value.Y, value.X, TextBox.Bottom);
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

        public SKPoint TextBottomRightPoint
        {
            get => new SKPoint(TextBox.Right, TextBox.Bottom);
            set
            {
                TextBox = new SKRect(TextBox.Left, TextBox.Top, value.X, value.Y);
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
            //    Border?.Apply(paint, BorderEffect);

            RefreshAppearance();
            DrawAppearance(canvas, Appearance.Normal[null]);
        }

        protected override void RefreshAppearance()
        {
            var textBounds = TextBox;
            SKRect box = Box;
            var normalAppearance = ResetAppearance(box, out var matrix);
            var font = DAOperation?.Name ?? normalAppearance.GetDefaultFont(out _);
            var fontSize = DAOperation?.Size ?? 10;
            var composer = new PrimitiveComposer(normalAppearance);
            {

                textBounds = matrix.MapRect(textBounds);
                composer.SetStrokeColor(DeviceRGBColor.Default);
                composer.SetFillColor(Color ?? DeviceRGBColor.White);
                composer.DrawRectangle(textBounds, 5);
                composer.FillStroke();

                if (Intent == MarkupIntent.FreeTextCallout && Line != null)
                {
                    var startPoint = matrix.MapPoint(Line.Start);
                    var endPoint = matrix.MapPoint(Line.End);
                    var kneePoint = Line.Knee is SKPoint knee ? matrix.MapPoint(knee) : (SKPoint?)null;
                    composer.StartPath(startPoint);
                    if (kneePoint != null)
                        composer.DrawLine(kneePoint.Value);
                    composer.DrawLine(endPoint);
                    composer.Stroke();
                    var normal = kneePoint != null
                        ? SKPoint.Normalize(startPoint - kneePoint.Value)
                        : SKPoint.Normalize(startPoint - endPoint);
                    var invertNormal = normal.Invert();
                    if (LineEndStyle == LineEndStyleEnum.Circle)
                    {
                        composer.DrawCircle(startPoint, 4);
                        composer.FillStroke();
                    }
                    else if (LineEndStyle == LineEndStyleEnum.Square)
                    {
                        composer.DrawQuad(startPoint, invertNormal.Multiply(4));
                        composer.FillStroke();
                    }
                    else if (LineEndStyle == LineEndStyleEnum.OpenArrow)
                    {
                        composer.AddOpenArrow(startPoint, invertNormal);
                        composer.Stroke();
                    }
                    else if (LineEndStyle == LineEndStyleEnum.ClosedArrow)
                    {
                        composer.AddClosedArrow(startPoint, invertNormal);
                        composer.FillStroke();
                    }

                }

                var block = new BlockComposer(composer);
                block.Begin(SKRect.Inflate(textBounds, -2, -2), XAlignmentEnum.Left, YAlignmentEnum.Top);
                composer.SetFillColor(DeviceRGBColor.Default);
                composer.SetFont(font, fontSize);
                block.ShowText(Contents);
                block.End();

                composer.Flush();
            }
        }

        public override void MoveTo(SKRect newBox)
        {
            allowRefresh = false;

            var oldBox = Box;
            var oldTextBox = TextBox;

            var dif = SKMatrix.CreateIdentity()
                .PreConcat(SKMatrix.CreateTranslation(newBox.MidX, newBox.MidY))
                .PreConcat(SKMatrix.CreateScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height))
                .PreConcat(SKMatrix.CreateTranslation(-oldBox.MidX, -oldBox.MidY));

            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                Line.Start = dif.MapPoint(Line.Start);
                Line.End = dif.MapPoint(Line.End);
                if (Line.Knee != null)
                    Line.Knee = dif.MapPoint(Line.Knee.Value);
            }
            base.MoveTo(newBox);
            TextBox = dif.MapRect(oldTextBox);
            RefreshAppearance();
            allowRefresh = true;
        }

        public void CalcLine()
        {
            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                var textBox = TextBox;
                var textBoxInflate = SKRect.Inflate(textBox, 15, 15);
                var midpoint = TextMidPoint;
                var start = Line.Start;
                if (start.X > (textBox.Left - 5) && start.X < (textBox.Right + 5))
                {
                    if (start.Y < textBox.Top)
                    {
                        Line.End = new SKPoint(textBox.MidX, textBox.Top);
                        if (Line.Knee != null)
                        {
                            Line.Knee = new SKPoint(textBoxInflate.MidX, textBoxInflate.Top);
                        }
                    }
                    else
                    {
                        Line.End = new SKPoint(textBox.MidX, textBox.Bottom);
                        if (Line.Knee != null)
                        {
                            Line.Knee = new SKPoint(textBoxInflate.MidX, textBoxInflate.Bottom);
                        }
                    }
                }
                else if (start.X < textBox.Left)
                {
                    Line.End = new SKPoint(textBox.Left, textBox.MidY);
                    if (Line.Knee != null)
                    {
                        Line.Knee = new SKPoint(textBoxInflate.Left, textBoxInflate.MidY);
                    }
                }
                else
                {
                    Line.End = new SKPoint(textBox.Right, textBox.MidY);
                    if (Line.Knee != null)
                    {
                        Line.Knee = new SKPoint(textBoxInflate.Right, textBoxInflate.MidY);
                    }
                }
            }
        }

        public override void RefreshBox()
        {
            if (!allowRefresh)
                return;
            allowRefresh = false;
            var oldTextBox = TextBox;
            CalcLine();
            var box = SKRect.Create(TextTopLeftPoint, SKSize.Empty);
            box.Add(TextTopRightPoint);
            box.Add(TextBottomRightPoint);
            box.Add(TextBottomLeftPoint);
            if (Intent == MarkupIntent.FreeTextCallout && Line != null)
            {
                box.Add(Line.Start);
                if (Line.Knee is SKPoint knee)
                    box.Add(knee);
                box.Add(Line.End);

            }
            Box = box;
            TextBox = oldTextBox;
            base.RefreshBox();
            RefreshAppearance();
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