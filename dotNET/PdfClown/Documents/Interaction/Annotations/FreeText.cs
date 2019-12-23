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
        #region types
        /**
          <summary>Callout line [PDF:1.6:8.4.5].</summary>
        */
        public class CalloutLine : PdfObjectWrapper<PdfArray>
        {

            public CalloutLine(Page page, SKPoint start, SKPoint end)
                : this(page, start, null, end)
            { }

            public CalloutLine(Page page, SKPoint start, SKPoint? knee, SKPoint end)
                : base(new PdfArray())
            {
                SKMatrix matrix = page.InRotateMatrix;
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
                    PdfArray coordinates = BaseDataObject;
                    var point = (coordinates.Count < 6)
                        ? new SKPoint(
                          (float)((IPdfNumber)coordinates[2]).RawValue,
                          (float)((IPdfNumber)coordinates[3]).RawValue)
                        : new SKPoint(
                          (float)((IPdfNumber)coordinates[4]).RawValue,
                          (float)((IPdfNumber)coordinates[5]).RawValue);

                    return FreeText.PageMatrix.MapPoint(point);
                }
                set
                {
                    PdfArray coordinates = BaseDataObject;
                    var val = FreeText.InvertPageMatrix.MapPoint(value);
                    if (coordinates.Count < 6)
                    {
                        coordinates[2] = PdfReal.Get(val.X);
                        coordinates[3] = PdfReal.Get(val.Y);
                    }
                    else
                    {
                        coordinates[4] = PdfReal.Get(val.X);
                        coordinates[5] = PdfReal.Get(val.Y);
                    }
                    FreeText.RefreshBox();
                }
            }

            public SKPoint? Knee
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                        return null;

                    return FreeText.PageMatrix.MapPoint(new SKPoint(
                      (float)((IPdfNumber)coordinates[2]).RawValue,
                      (float)((IPdfNumber)coordinates[3]).RawValue));
                }
                set
                {
                    PdfArray coordinates = BaseDataObject;
                    var val = FreeText.InvertPageMatrix.MapPoint(value.Value);
                    coordinates[2] = PdfReal.Get(val.X);
                    coordinates[3] = PdfReal.Get(val.Y);
                    //FreeText.RefreshBox();
                }
            }

            public SKPoint Start
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;

                    return FreeText.PageMatrix.MapPoint(new SKPoint(
                      (float)((IPdfNumber)coordinates[0]).RawValue,
                      (float)((IPdfNumber)coordinates[1]).RawValue));
                }
                set
                {
                    PdfArray coordinates = BaseDataObject;
                    var val = FreeText.InvertPageMatrix.MapPoint(value);
                    coordinates[0] = PdfReal.Get(val.X);
                    coordinates[1] = PdfReal.Get(val.Y);
                    FreeText.RefreshBox();
                }
            }

            public FreeText FreeText { get; internal set; }
        }

        /**
          <summary>Note type [PDF:1.6:8.4.5].</summary>
        */
        public enum TypeEnum
        {
            /**
              Default.
            */
            Default,
            /**
              Callout.
            */
            Callout,
            /**
              Typewriter.
            */
            TypeWriter
        }
        #endregion

        #region static
        #region fields
        private static readonly JustificationEnum DefaultJustification = JustificationEnum.Left;
        private static readonly LineEndStyleEnum DefaultLineEndStyle = LineEndStyleEnum.None;
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
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public FreeText(Page page, SKRect box, string text)
            : base(page, PdfName.FreeText, box, text)
        { }

        internal FreeText(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the border effect.</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public BorderEffect BorderEffect
        {
            get => Wrap<BorderEffect>(BaseDataObject.Get<PdfDictionary>(PdfName.BE));
            set
            {
                BaseDataObject[PdfName.BE] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the justification to be used in displaying the annotation's text.</summary>
        */
        public JustificationEnum Justification
        {
            get => JustificationEnumExtension.Get((PdfInteger)BaseDataObject[PdfName.Q]);
            set
            {
                BaseDataObject[PdfName.Q] = value != DefaultJustification ? value.GetCode() : null;
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the callout line attached to the free text annotation.</summary>
        */
        public CalloutLine Line
        {
            get
            {
                var calloutCalloutLine = (PdfArray)BaseDataObject[PdfName.CL];
                var line = Wrap<CalloutLine>(calloutCalloutLine);
                if (line != null)
                {
                    line.FreeText = this;
                }

                return line;
            }
            set
            {
                BaseDataObject[PdfName.CL] = PdfObjectWrapper.GetBaseObject(value);
                if (value != null)
                {
                    /*
                      NOTE: To ensure the callout would be properly rendered, we have to declare the
                      corresponding intent.
                    */
                    Type = TypeEnum.Callout;
                    value.FreeText = this;
                }
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the style of the ending line ending.</summary>
        */
        public LineEndStyleEnum LineEndStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[1]) : DefaultLineEndStyle;
            }
            set
            {
                EnsureLineEndStylesObject()[1] = value.GetName();
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the style of the starting line ending.</summary>
        */
        public LineEndStyleEnum LineStartStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[0]) : DefaultLineEndStyle;
            }
            set
            {
                EnsureLineEndStylesObject()[0] = value.GetName();
                OnPropertyChanged();
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
        #endregion

        #region private
        private PdfArray EnsureLineEndStylesObject()
        {
            PdfArray endStylesObject = (PdfArray)BaseDataObject[PdfName.LE];
            if (endStylesObject == null)
            {
                BaseDataObject[PdfName.LE] = endStylesObject = new PdfArray(
                  new PdfDirectObject[] { DefaultLineEndStyle.GetName(), DefaultLineEndStyle.GetName() }
                  );
            }
            return endStylesObject;
        }

        public TypeEnum? Type
        {
            get => FreeTextTypeEnumExtension.Get(TypeBase);
            set => TypeBase = value.HasValue ? value.Value.GetName() : null;
        }

        public SKRect TextBox
        {
            get
            {
                if (textBox == null)
                {
                    var bounds = Rect;
                    var diff = (PdfArray)BaseDataObject[PdfName.RD];
                    textBox = PageMatrix.MapRect(new SKRect(
                        bounds.Left + (((IPdfNumber)diff?[0])?.FloatValue ?? 0F),
                        bounds.Top + (((IPdfNumber)diff?[1])?.FloatValue ?? 0F),
                        bounds.Right - (((IPdfNumber)diff?[2])?.FloatValue ?? 0F),
                        bounds.Bottom - (((IPdfNumber)diff?[3])?.FloatValue ?? 0F)));
                }
                return textBox.Value;
            }
            set
            {
                textBox = value;
                var mapped = InvertPageMatrix.MapRect(value);
                var bounds = Rect;
                BaseDataObject[PdfName.RD] = new PdfArray(
                    PdfReal.Get(mapped.Left - bounds.Left),
                    PdfReal.Get(mapped.Top - bounds.Top),
                    PdfReal.Get(bounds.Right - mapped.Right),
                    PdfReal.Get(bounds.Bottom - mapped.Bottom));
                OnPropertyChanged();
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
            var bounds = Box;
            var textBounds = TextBox;
            var color = SKColor;

            using (var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill })
            {
                canvas.DrawRect(textBounds, paint);
            }
            using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke })
            {
                Border?.Apply(paint, BorderEffect);
                canvas.DrawRect(textBounds, paint);
                if (Type == TypeEnum.Callout && Line != null)
                {
                    var line = Line;
                    using (var linePath = new SKPath())
                    {
                        linePath.MoveTo(Line.Start);
                        if (line.Knee != null)
                            linePath.LineTo(Line.Knee.Value);
                        linePath.LineTo(Line.End);

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
                canvas.DrawLines(Text, textBounds, paint);
            }

        }

        public override void MoveTo(SKRect newBox)
        {
            allowRefresh = false;
            Appearance.Normal[null] = null;

            var oldBox = Box;
            var oldTextBox = TextBox;

            var dif = SKMatrix.MakeIdentity();
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(newBox.MidX, newBox.MidY));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(-oldBox.MidX, -oldBox.MidY));

            if (Type == TypeEnum.Callout && Line != null)
            {
                Line.Start = dif.MapPoint(Line.Start);
                Line.End = dif.MapPoint(Line.End);
                if (Line.Knee != null)
                    Line.Knee = dif.MapPoint(Line.Knee.Value);
            }
            base.MoveTo(newBox);
            TextBox = dif.MapRect(oldTextBox);
            allowRefresh = true;
        }

        public void CalcLine()
        {
            if (Type == TypeEnum.Callout && Line != null)
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
            Appearance.Normal[null] = null;
            var oldTextBox = TextBox;
            CalcLine();
            var box = SKRect.Create(TextTopLeftPoint, SKSize.Empty);
            box.Add(TextTopRightPoint);
            box.Add(TextBottomRightPoint);
            box.Add(TextBottomLeftPoint);
            if (Type == TypeEnum.Callout && Line != null)
            {
                box.Add(Line.Start);
                box.Add(Line.End);
                if (Line.Knee != null)
                {
                    box.Add(Line.Knee.Value);
                }
            }
            Box = box;
            TextBox = oldTextBox;
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
            if (Type == TypeEnum.Callout && Line != null)
            {
                yield return cpLineStart ?? (cpLineStart = new TextLineStartControlPoint { Annotation = this });
                yield return cpLineEnd ?? (cpLineEnd = new TextLineEndControlPoint { Annotation = this });
                if (Line.Knee != null)
                {
                    yield return cpLineKnee ?? (cpLineKnee = new TextLineKneeControlPoint { Annotation = this });
                }
            }

            foreach (var cpBase in base.GetControlPoints())
            {
                yield return cpBase;
            }

        }

        #endregion
        #endregion
        #endregion
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

    internal static class FreeTextTypeEnumExtension
    {
        private static readonly BiDictionary<FreeText.TypeEnum, PdfName> codes;

        static FreeTextTypeEnumExtension()
        {
            codes = new BiDictionary<FreeText.TypeEnum, PdfName>
            {
                [FreeText.TypeEnum.Default] = PdfName.FreeText,
                [FreeText.TypeEnum.Callout] = PdfName.FreeTextCallout,
                [FreeText.TypeEnum.TypeWriter] = PdfName.FreeTextTypeWriter
            };
        }

        public static FreeText.TypeEnum? Get(PdfName name)
        {
            if (name == null)
                return null;

            FreeText.TypeEnum? type = codes.GetKey(name);
            if (!type.HasValue)
                throw new NotSupportedException("Type unknown: " + name);

            return type.Value;
        }

        public static PdfName GetName(this FreeText.TypeEnum type)
        { return codes[type]; }
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