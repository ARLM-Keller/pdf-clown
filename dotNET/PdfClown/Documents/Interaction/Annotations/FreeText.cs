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
            internal Page page;

            public CalloutLine(Page page, SKPoint start, SKPoint end)
                : this(page, start, null, end)
            { }

            public CalloutLine(Page page, SKPoint start, SKPoint? knee, SKPoint end)
                : base(new PdfArray())
            {
                this.page = page;
                PdfArray baseDataObject = BaseDataObject;
                {
                    double pageHeight = page.Box.Height;
                    baseDataObject.Add(PdfReal.Get(start.X));
                    baseDataObject.Add(PdfReal.Get(pageHeight - start.Y));
                    if (knee.HasValue)
                    {
                        baseDataObject.Add(PdfReal.Get(knee.Value.X));
                        baseDataObject.Add(PdfReal.Get(pageHeight - knee.Value.Y));
                    }
                    baseDataObject.Add(PdfReal.Get(end.X));
                    baseDataObject.Add(PdfReal.Get(pageHeight - end.Y));
                }
            }

            public CalloutLine(PdfDirectObject baseObject) : base(baseObject)
            { }

            public SKPoint End
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                        return new SKPoint(
                          (float)((IPdfNumber)coordinates[2]).RawValue,
                          (float)(page.Box.Height - ((IPdfNumber)coordinates[3]).RawValue)
                          );
                    else
                        return new SKPoint(
                          (float)((IPdfNumber)coordinates[4]).RawValue,
                          (float)(page.Box.Height - ((IPdfNumber)coordinates[5]).RawValue)
                          );
                }
            }

            public SKPoint? Knee
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                        return null;

                    return new SKPoint(
                      (float)((IPdfNumber)coordinates[2]).RawValue,
                      (float)(page.Box.Height - ((IPdfNumber)coordinates[3]).RawValue)
                      );
                }
            }

            public SKPoint Start
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;

                    return new SKPoint(
                      (float)((IPdfNumber)coordinates[0]).RawValue,
                      (float)(page.Box.Height - ((IPdfNumber)coordinates[1]).RawValue)
                      );
                }
            }
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
                return Wrap<CalloutLine>(calloutCalloutLine);
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
                var bounds = Box;
                var box = Wrap<Objects.Rectangle>(BaseDataObject[PdfName.RD]) ?? new Objects.Rectangle(SKRect.Empty);
                return new SKRect(
                  (float)box.Right + bounds.Left,
                  (float)box.Bottom + bounds.Top,
                  bounds.Right - (float)box.Left,
                  bounds.Bottom - (float)box.Top);
            }
            set
            {
                var bounds = Box;
                BaseDataObject[PdfName.RD] = new Objects.Rectangle(value.Left, GetPageHeight() - value.Top, value.Width, value.Height)
                    .BaseDataObject;
                OnPropertyChanged();
            }
        }

        public override bool ShowToolTip => false;

        public override void Draw(SKCanvas canvas)
        {
            var bounds = Box;
            var textBounds = TextBox;
            var color = Color == null ? SKColors.Black : Color.ColorSpace.GetColor(Color, Alpha);

            using (var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill })
            {
                canvas.DrawRect(textBounds, paint);
            }

            using (var paint = new SKPaint())
            {
                Border?.Apply(paint, BorderEffect);
                canvas.DrawRect(textBounds, paint);
            }

            using (var paint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.StrokeAndFill })
            {
                var temp = SKRect.Create(0, 0, textBounds.Width, 0);
                var left = textBounds.Left + 5;
                var top = textBounds.Top + paint.FontSpacing;
                foreach (var line in GetLines(Text.Trim(), textBounds, paint))
                {
                    canvas.DrawText(line, left, top, paint);
                    top += paint.FontSpacing;
                }
            }
            if (Type == TypeEnum.Callout && Line != null)
            {
                var line = Line;
                line.page = Page;
                using (var linePath = new SKPath())
                using (var paint = new SKPaint { Style = SKPaintStyle.Stroke })
                {
                    Border?.Apply(paint, BorderEffect);

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

        private IEnumerable<string> GetLines(string text, SKRect textBounds, SKPaint paint)
        {
            //var builder = new SKTextBlobBuilder();
            foreach (var line in text.Split('\r', '\n'))
            {
                var count = (int)paint.BreakText(line, textBounds.Width);
                if (count == line.Length)
                    yield return line;
                else
                {
                    var index = 0;
                    while (true)
                    {
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

        #endregion
        #endregion
        #endregion
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
}