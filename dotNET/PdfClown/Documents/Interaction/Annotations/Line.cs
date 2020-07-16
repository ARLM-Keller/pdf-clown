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
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Util.Math.Geom;
using PdfClown.Documents.Interaction.Actions;
using PdfClown.Util;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Line annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It displays displays a single straight line on the page.
      When opened, it displays a pop-up window containing the text of the associated note.</remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class Line : Markup
    {
        #region static
        #region fields
        private static readonly double DefaultLeaderLineExtension = 0;
        private static readonly double DefaultLeaderLineLength = 0;
        private static readonly double DefaultLeaderLineOffset = 0;
        private static readonly LineEndStyleEnum DefaultLineEndStyle = LineEndStyleEnum.None;
        #endregion
        #endregion

        #region dynamic
        private SKPoint? startPoint;
        private SKPoint? endPoint;
        private SKPoint? captionOffset;

        private LineStartControlPoint cpStart;
        private LineEndControlPoint cpEnd;
        #region constructors
        public Line(Page page, SKPoint startPoint, SKPoint endPoint, string text, DeviceRGBColor color)
            : base(page, PdfName.Line, SKRect.Create(startPoint.X, startPoint.Y, endPoint.X - startPoint.X, endPoint.Y - startPoint.Y), text)
        {
            BaseDataObject[PdfName.L] = new PdfArray(new PdfDirectObject[] { PdfReal.Get(0), PdfReal.Get(0), PdfReal.Get(0), PdfReal.Get(0) });
            StartPoint = startPoint;
            EndPoint = endPoint;
            Color = color;
        }

        public Line(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets whether the contents should be shown as a caption.</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public bool CaptionVisible
        {
            get => BaseDataObject.GetBool(PdfName.Cap);
            set
            {
                var oldValue = CaptionVisible;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.Cap] = PdfBoolean.Get(value);
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        [PDF(VersionEnum.PDF17)]
        public LineCaptionPosition? CaptionPosition
        {
            get => CaptionPositionExtension.Get((PdfName)BaseDataObject[PdfName.CP]);
            set
            {
                var oldValue = CaptionPosition;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.CP] = CaptionPositionExtension.GetName(value);
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        [PDF(VersionEnum.PDF17)]
        public SKPoint? CaptionOffset
        {
            get
            {
                if (captionOffset == null)
                {
                    var offset = (PdfArray)BaseDataObject[PdfName.CO];
                    captionOffset = new SKPoint(offset?.GetFloat(0) ?? 0F, offset?.GetFloat(1) ?? 0F);
                }
                return captionOffset;
            }
            set
            {
                var oldValue = CaptionOffset;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.CO] = value == null ? null : new PdfArray(PdfReal.Get(value.Value.X), PdfReal.Get((float)value.Value.Y));
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        /**
          <summary>Gets/Sets the style of the starting line ending.</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public LineEndStyleEnum StartStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null
                  ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[0])
                  : DefaultLineEndStyle;
            }
            set
            {
                var oldValue = StartStyle;
                if (oldValue != value)
                {
                    EnsureLineEndStylesObject()[0] = value.GetName();
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        /**
          <summary>Gets/Sets the style of the ending line ending.</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public LineEndStyleEnum EndStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null
                  ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[1])
                  : DefaultLineEndStyle;
            }
            set
            {
                var oldValue = EndStyle;
                if (oldValue != value)
                {
                    EnsureLineEndStylesObject()[1] = value.GetName();
                    OnPropertyChanged(oldValue, value);
                }
            }
        }

        /**
          <summary>Gets/Sets the length of leader line extensions that extend
          in the opposite direction from the leader lines.</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public double LeaderLineExtension
        {
            get => BaseDataObject.GetDouble(PdfName.LLE, DefaultLeaderLineExtension);
            set
            {
                var oldValue = LeaderLineExtension;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.LLE] = PdfReal.Get(value);
                    /*
                      NOTE: If leader line extension entry is present, leader line MUST be too.
                    */
                    if (!BaseDataObject.ContainsKey(PdfName.LL))
                    {
                        LeaderLineLength = DefaultLeaderLineLength;
                    }
                    OnPropertyChanged(oldValue, value);
                    RefreshBox();
                }
            }
        }

        [PDF(VersionEnum.PDF17)]
        public double LeaderLineOffset
        {
            get => BaseDataObject.GetDouble(PdfName.LLO, DefaultLeaderLineOffset);
            set
            {
                var oldValue = LeaderLineOffset;
                if (oldValue != value)
                {
                    BaseDataObject.SetDouble(PdfName.LLO, value);
                    /*
                      NOTE: If leader line extension entry is present, leader line MUST be too.
                    */
                    if (!BaseDataObject.ContainsKey(PdfName.LL))
                    {
                        LeaderLineLength = DefaultLeaderLineLength;
                    }
                    OnPropertyChanged(oldValue, value);
                    RefreshBox();
                }
            }
        }

        /**
          <summary>Gets/Sets the length of leader lines that extend from each endpoint
          of the line perpendicular to the line itself.</summary>
          <remarks>A positive value means that the leader lines appear in the direction
          that is clockwise when traversing the line from its starting point
          to its ending point; a negative value indicates the opposite direction.</remarks>
        */
        [PDF(VersionEnum.PDF16)]
        public double LeaderLineLength
        {
            get => BaseDataObject.GetDouble(PdfName.LL, DefaultLeaderLineLength);
            set
            {
                var oldValue = LeaderLineOffset;
                if (oldValue != value)
                {
                    BaseDataObject.SetDouble(PdfName.LL, value);
                    OnPropertyChanged(oldValue, value);
                    RefreshBox();
                }
            }
        }

        public PdfArray LineData
        {
            get => (PdfArray)BaseDataObject[PdfName.L];
            set
            {
                var oldValue = LineData;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.L] = value;
                    if (startPoint != null)
                    {
                        startPoint = null;
                        endPoint = null;
                        RefreshBox();
                    }
                }
            }
        }

        /**
          <summary>Gets/Sets the starting coordinates.</summary>
        */
        public SKPoint StartPoint
        {
            get => startPoint ?? (startPoint = PageMatrix.MapPoint(new SKPoint(LineData.GetFloat(0), LineData.GetFloat(1)))).Value;
            set
            {
                var oldValue = StartPoint;
                if (oldValue != value)
                {
                    startPoint = value;
                    value = InvertPageMatrix.MapPoint(value);
                    PdfArray coordinatesObject = LineData;
                    coordinatesObject[0] = PdfReal.Get(value.X);
                    coordinatesObject[1] = PdfReal.Get(value.Y);
                    OnPropertyChanged(coordinatesObject, coordinatesObject, nameof(LineData));
                    RefreshBox();
                }
            }
        }

        /**
          <summary>Gets/Sets the ending coordinates.</summary>
        */
        public SKPoint EndPoint
        {
            get => endPoint ?? (endPoint = PageMatrix.MapPoint(new SKPoint(LineData.GetFloat(2), LineData.GetFloat(3)))).Value;
            set
            {
                var oldValue = EndPoint;
                if (oldValue != value)
                {
                    endPoint = value;
                    value = InvertPageMatrix.MapPoint(value);
                    PdfArray coordinatesObject = LineData;
                    coordinatesObject[2] = PdfReal.Get(value.X);
                    coordinatesObject[3] = PdfReal.Get(value.Y);
                    OnPropertyChanged(coordinatesObject, coordinatesObject, nameof(LineData));
                    RefreshBox();
                }
            }
        }

        public override bool ShowToolTip => !CaptionVisible;

        public override void MoveTo(SKRect newBox)
        {
            var oldBox = Box;
            if (oldBox.Width != newBox.Width
               || oldBox.Height != newBox.Height)
            {
                Appearance.Normal[null] = null;
            }
            var dif = SKMatrix.CreateIdentity()
                .PreConcat(SKMatrix.CreateTranslation(newBox.MidX, newBox.MidY))
                .PreConcat(SKMatrix.CreateScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height))
                .PreConcat(SKMatrix.CreateTranslation(-oldBox.MidX, -oldBox.MidY));

            StartPoint = dif.MapPoint(StartPoint);
            EndPoint = dif.MapPoint(EndPoint);
            base.MoveTo(newBox);
        }
        #endregion

        #region private
        private PdfArray EnsureLineEndStylesObject()
        {
            PdfArray endStylesObject = (PdfArray)BaseDataObject[PdfName.LE];
            if (endStylesObject == null)
            {
                BaseDataObject[PdfName.LE] = endStylesObject = new PdfArray(
                  new PdfDirectObject[] {
                      DefaultLineEndStyle.GetName(),
                      DefaultLineEndStyle.GetName() }
                  );
            }
            return endStylesObject;
        }

        public override void DrawSpecial(SKCanvas canvas)
        {
            var color = Color == null ? SKColors.Black : DeviceColorSpace.CalcSKColor(Color, Alpha);
            using (var paint = new SKPaint { Color = color })
            using (var path = new SKPath())
            {
                var lineLength = SKPoint.Distance(StartPoint, EndPoint);
                var normal = SKPoint.Normalize(EndPoint - StartPoint);
                var invertNormal = new SKPoint(normal.X * -1, normal.Y * -1);

                Border?.Apply(paint, null);
                path.MoveTo(StartPoint);
                path.LineTo(EndPoint);

                if (CaptionVisible && !string.IsNullOrEmpty(Contents))
                {

                    using (var textPaint = new SKPaint { Color = color, TextSize = 9, IsAntialias = true })
                    {
                        var textLength = textPaint.MeasureText(Contents);
                        var offset = (lineLength - textLength) / 2;

                        canvas.DrawTextOnPath(Contents, path, new SKPoint(offset, 2), textPaint);
                        path.Rewind();
                        path.MoveTo(StartPoint);
                        path.LineTo(StartPoint + new SKPoint(normal.X * offset, normal.Y * offset));

                        path.MoveTo(EndPoint);
                        path.LineTo(EndPoint + new SKPoint(normal.X * -offset, normal.Y * -offset));
                    }
                }
                if (StartStyle == LineEndStyleEnum.OpenArrow)
                {
                    path.AddOpenArrow(StartPoint, normal);
                }
                else if (StartStyle == LineEndStyleEnum.ClosedArrow)
                {
                    path.AddCloseArrow(StartPoint, normal);
                }
                else if (StartStyle == LineEndStyleEnum.Circle)
                {
                    path.AddCircle(StartPoint.X, StartPoint.Y, 4);
                }

                if (EndStyle == LineEndStyleEnum.OpenArrow)
                {
                    path.AddOpenArrow(EndPoint, invertNormal);
                }
                else if (EndStyle == LineEndStyleEnum.ClosedArrow)
                {
                    path.AddCloseArrow(EndPoint, invertNormal);
                }
                else if (EndStyle == LineEndStyleEnum.Circle)
                {
                    path.AddCircle(EndPoint.X, EndPoint.Y, 4);
                }


                canvas.DrawPath(path, paint);

            }
        }

        public override void RefreshBox()
        {
            Appearance.Normal[null] = null;
            var box = SKRect.Create(StartPoint, SKSize.Empty);
            box.Add(EndPoint);
            box.Inflate(box.Width < 5 ? 5 : 0, box.Height < 5 ? 5 : 0);
            Box = box;
        }

        public override IEnumerable<ControlPoint> GetControlPoints()
        {
            yield return cpStart ?? (cpStart = new LineStartControlPoint { Annotation = this });
            yield return cpEnd ?? (cpEnd = new LineEndControlPoint { Annotation = this });
        }

        public override object Clone(Cloner cloner)
        {
            var cloned = (Line)base.Clone(cloner);
            cloned.cpStart = null;
            cloned.cpEnd = null;
            return cloned;
        }

        #endregion
        #endregion
        #endregion
    }

    public enum LineCaptionPosition
    {
        Inline,
        Top
    }

    public static class CaptionPositionExtension
    {
        private static readonly BiDictionary<LineCaptionPosition, PdfName> codes;

        static CaptionPositionExtension()
        {
            codes = new BiDictionary<LineCaptionPosition, PdfName>
            {
                [LineCaptionPosition.Inline] = PdfName.Inline,
                [LineCaptionPosition.Top] = PdfName.Top
            };
        }

        public static LineCaptionPosition? Get(PdfName name)
        {
            if (name == null)
                return LineCaptionPosition.Inline;

            return codes.GetKey(name);
        }

        public static PdfName GetName(this LineCaptionPosition? type)
        {
            return type == null ? null : codes[type.Value];
        }
    }

    public class LineStartControlPoint : ControlPoint
    {
        public Line Line => (Line)Annotation;
        public override SKPoint Point
        {
            get => Line.StartPoint;
            set => Line.StartPoint = value;
        }
    }

    public class LineEndControlPoint : ControlPoint
    {
        public Line Line => (Line)Annotation;
        public override SKPoint Point
        {
            get => Line.EndPoint;
            set => Line.EndPoint = value;
        }
    }
}