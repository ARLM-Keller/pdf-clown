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
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Objects;
using PdfClown.Util.Math.Geom;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Text markup annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It displays highlights, underlines, strikeouts, or jagged ("squiggly") underlines in
      the text of a document.</remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class TextMarkup : Markup
    {
        #region types
        /**
          <summary>Markup type [PDF:1.6:8.4.5].</summary>
        */
        public enum MarkupTypeEnum
        {
            /**
              <summary>Highlight.</summary>
            */
            [PDF(VersionEnum.PDF13)]
            Highlight,
            /**
              <summary>Squiggly.</summary>
            */
            [PDF(VersionEnum.PDF14)]
            Squiggly,
            /**
              <summary>StrikeOut.</summary>
            */
            [PDF(VersionEnum.PDF13)]
            StrikeOut,
            /**
              <summary>Underline.</summary>
            */
            [PDF(VersionEnum.PDF13)]
            Underline
        };
        #endregion

        #region static
        #region fields
        private static readonly Dictionary<MarkupTypeEnum, PdfName> MarkupTypeEnumCodes;
        #endregion

        #region constructors
        static TextMarkup()
        {
            MarkupTypeEnumCodes = new Dictionary<MarkupTypeEnum, PdfName>
            {
                [MarkupTypeEnum.Highlight] = PdfName.Highlight,
                [MarkupTypeEnum.Squiggly] = PdfName.Squiggly,
                [MarkupTypeEnum.StrikeOut] = PdfName.StrikeOut,
                [MarkupTypeEnum.Underline] = PdfName.Underline
            };
        }
        #endregion

        #region interface
        #region private
        /**
          <summary>Gets the code corresponding to the given value.</summary>
        */
        private static PdfName ToCode(MarkupTypeEnum value)
        { return MarkupTypeEnumCodes[value]; }

        /**
          <summary>Gets the markup type corresponding to the given value.</summary>
        */
        private static MarkupTypeEnum ToMarkupTypeEnum(PdfName value)
        {
            foreach (KeyValuePair<MarkupTypeEnum, PdfName> markupType in MarkupTypeEnumCodes)
            {
                if (markupType.Value.Equals(value))
                    return markupType.Key;
            }
            throw new Exception("Invalid markup type.");
        }
        #endregion
        #endregion
        #endregion

        #region static
        #region fields
        private static readonly PdfName HighlightExtGStateName = new PdfName("highlight");
        private List<Quad> markupBoxes;
        #endregion

        #region interface
        private static float GetMarkupBoxMargin(float boxHeight)
        { return boxHeight * .25f; }
        #endregion
        #endregion

        #region dynamic
        #region constructors
        /**
          <summary>Creates a new text markup on the specified page, making it printable by default.
          </summary>
          <param name="page">Page to annotate.</param>
          <param name="markupBox">Quadrilateral encompassing a word or group of contiguous words in the
          text underlying the annotation.</param>
          <param name="text">Annotation text.</param>
          <param name="markupType">Markup type.</param>
        */
        public TextMarkup(Page page, Quad markupBox, string text, MarkupTypeEnum markupType)
            : this(page, new List<Quad>() { markupBox }, text, markupType)
        { }

        /**
          <summary>Creates a new text markup on the specified page, making it printable by default.
          </summary>
          <param name="page">Page to annotate.</param>
          <param name="markupBoxes">Quadrilaterals encompassing a word or group of contiguous words in
          the text underlying the annotation.</param>
          <param name="text">Annotation text.</param>
          <param name="markupType">Markup type.</param>
        */
        public TextMarkup(Page page, IList<Quad> markupBoxes, string text, MarkupTypeEnum markupType)
            : base(page, ToCode(markupType), markupBoxes[0].GetBounds(), text)
        {
            MarkupType = markupType;
            MarkupBoxes = markupBoxes;
            Printable = true;
        }

        internal TextMarkup(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public override DeviceColor Color
        {
            set
            {
                base.Color = value;
                if (Appearance.Normal[null] != null)
                { RefreshAppearance(); }
            }
        }

        /**
          <summary>Gets/Sets the quadrilaterals encompassing a word or group of contiguous words in the
          text underlying the annotation.</summary>
        */
        public IList<Quad> MarkupBoxes
        {
            get
            {
                markupBoxes = new List<Quad>();
                PdfArray quadPointsObject = (PdfArray)BaseDataObject[PdfName.QuadPoints];
                if (quadPointsObject != null)
                {
                    var length = quadPointsObject.Count;
                    if (markupBoxes.Count * 8 != length)
                    {
                        markupBoxes.Clear();
                        var pageMatrix = PageMatrix;

                        for (int index = 0; index < length; index += 8)
                        {
                            /*
                              NOTE: Despite the spec prescription, point 3 and point 4 MUST be inverted.
                            */
                            var quad = new Quad(
                                new SKPoint(
                                  ((IPdfNumber)quadPointsObject[index]).FloatValue,
                                  ((IPdfNumber)quadPointsObject[index + 1]).FloatValue),
                                new SKPoint(
                                  ((IPdfNumber)quadPointsObject[index + 2]).FloatValue,
                                  ((IPdfNumber)quadPointsObject[index + 3]).FloatValue),
                                new SKPoint(
                                  ((IPdfNumber)quadPointsObject[index + 6]).FloatValue,
                                  ((IPdfNumber)quadPointsObject[index + 7]).FloatValue),
                                new SKPoint(
                                  ((IPdfNumber)quadPointsObject[index + 4]).FloatValue,
                                  ((IPdfNumber)quadPointsObject[index + 5]).FloatValue));
                            quad.Transform(ref pageMatrix);
                            markupBoxes.Add(quad);
                        }
                    }
                }
                return markupBoxes;
            }
            set
            {
                PdfArray quadPointsObject = new PdfArray();
                var pageMatrix = InvertPageMatrix;
                markupBoxes.Clear();
                SKRect box = SKRect.Empty;
                foreach (Quad markupBox in value)
                {
                    markupBoxes.Add(markupBox);
                    /*
                      NOTE: Despite the spec prescription, point 3 and point 4 MUST be inverted.
                    */
                    SKPoint[] markupBoxPoints = pageMatrix.MapPoints(markupBox.GetPoints());

                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[0].X)); // x1.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[0].Y)); // y1.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[1].X)); // x2.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[1].Y)); // y2.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[3].X)); // x4.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[3].Y)); // y4.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[2].X)); // x3.
                    quadPointsObject.Add(PdfReal.Get(markupBoxPoints[2].Y)); // y3.
                    if (box.IsEmpty)
                    { box = markupBox.GetBounds(); }
                    else
                    { box = SKRect.Union(box, markupBox.GetBounds()); }
                }
                BaseDataObject[PdfName.QuadPoints] = quadPointsObject;

                /*
                  NOTE: Box width is expanded to make room for end decorations (e.g. rounded highlight caps).
                */
                float markupBoxMargin = GetMarkupBoxMargin(box.Height);
                box.Inflate(markupBoxMargin, 0);
                Box = box;

                RefreshAppearance();
            }
        }

        /**
          <summary>Gets/Sets the markup type.</summary>
        */
        public MarkupTypeEnum MarkupType
        {
            get => ToMarkupTypeEnum((PdfName)BaseDataObject[PdfName.Subtype]);
            set
            {
                BaseDataObject[PdfName.Subtype] = ToCode(value);
                switch (value)
                {
                    case MarkupTypeEnum.Highlight:
                        Color = new DeviceRGBColor(1, 1, 0);
                        break;
                    case MarkupTypeEnum.Squiggly:
                        Color = new DeviceRGBColor(1, 0, 0);
                        break;
                    default:
                        Color = new DeviceRGBColor(0, 0, 0);
                        break;
                }
            }
        }
        #endregion

        #region private
        /*
          TODO: refresh should happen just before serialization, on document event (e.g. OnWrite())
        */
        private void RefreshAppearance()
        {
            FormXObject normalAppearance;
            SKRect box = Wrap<Objects.Rectangle>(BaseDataObject[PdfName.Rect]).ToRectangleF();
            {
                AppearanceStates normalAppearances = Appearance.Normal;
                normalAppearance = normalAppearances[null];
                if (normalAppearance != null)
                {
                    normalAppearance.Box = box;
                    normalAppearance.BaseDataObject.Body.SetLength(0);
                }
                else
                { normalAppearances[null] = normalAppearance = new FormXObject(Document, box); }
            }

            PrimitiveComposer composer = new PrimitiveComposer(normalAppearance);
            {
                float yOffset = box.Height - Page.Box.Height;
                MarkupTypeEnum markupType = MarkupType;
                switch (markupType)
                {
                    case MarkupTypeEnum.Highlight:
                        {
                            ExtGState defaultExtGState;
                            {
                                ExtGStateResources extGStates = normalAppearance.Resources.ExtGStates;
                                defaultExtGState = extGStates[HighlightExtGStateName];
                                if (defaultExtGState == null)
                                {
                                    if (extGStates.Count > 0)
                                    { extGStates.Clear(); }

                                    extGStates[HighlightExtGStateName] = defaultExtGState = new ExtGState(Document);
                                    defaultExtGState.AlphaShape = false;
                                    defaultExtGState.BlendMode = new List<BlendModeEnum>(new BlendModeEnum[] { BlendModeEnum.Multiply });
                                }
                            }

                            composer.ApplyState(defaultExtGState);
                            composer.SetFillColor(Color);
                            {
                                foreach (Quad markupBox in MarkupBoxes)
                                {
                                    SKPoint[] points = markupBox.GetPoints();
                                    float markupBoxHeight = points[3].Y - points[0].Y;
                                    float markupBoxMargin = GetMarkupBoxMargin(markupBoxHeight);
                                    composer.DrawCurve(
                                      new SKPoint(points[3].X, points[3].Y + yOffset),
                                      new SKPoint(points[0].X, points[0].Y + yOffset),
                                      new SKPoint(points[3].X - markupBoxMargin, points[3].Y - markupBoxMargin + yOffset),
                                      new SKPoint(points[0].X - markupBoxMargin, points[0].Y + markupBoxMargin + yOffset)
                                      );
                                    composer.DrawLine(
                                      new SKPoint(points[1].X, points[1].Y + yOffset)
                                      );
                                    composer.DrawCurve(
                                      new SKPoint(points[2].X, points[2].Y + yOffset),
                                      new SKPoint(points[1].X + markupBoxMargin, points[1].Y + markupBoxMargin + yOffset),
                                      new SKPoint(points[2].X + markupBoxMargin, points[2].Y - markupBoxMargin + yOffset)
                                      );
                                    composer.Fill();
                                }
                            }
                        }
                        break;
                    case MarkupTypeEnum.Squiggly:
                        {
                            composer.SetStrokeColor(Color);
                            composer.SetLineCap(LineCapEnum.Round);
                            composer.SetLineJoin(LineJoinEnum.Round);
                            {
                                foreach (Quad markupBox in MarkupBoxes)
                                {
                                    SKPoint[] points = markupBox.GetPoints();
                                    float markupBoxHeight = points[3].Y - points[0].Y;
                                    float lineWidth = markupBoxHeight * .05f;
                                    float step = markupBoxHeight * .125f;
                                    float boxXOffset = points[3].X;
                                    float boxYOffset = points[3].Y + yOffset - lineWidth;
                                    bool phase = false;
                                    composer.SetLineWidth(lineWidth);
                                    for (float x = 0, xEnd = points[2].X - boxXOffset; x < xEnd || !phase; x += step)
                                    {
                                        SKPoint point = new SKPoint(x + boxXOffset, (phase ? -step : 0) + boxYOffset);
                                        if (x == 0)
                                        { composer.StartPath(point); }
                                        else
                                        { composer.DrawLine(point); }
                                        phase = !phase;
                                    }
                                }
                                composer.Stroke();
                            }
                        }
                        break;
                    case MarkupTypeEnum.StrikeOut:
                    case MarkupTypeEnum.Underline:
                        {
                            composer.SetStrokeColor(Color);
                            {
                                float lineYRatio = 0;
                                switch (markupType)
                                {
                                    case MarkupTypeEnum.StrikeOut:
                                        lineYRatio = .5f;
                                        break;
                                    case MarkupTypeEnum.Underline:
                                        lineYRatio = .9f;
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                                foreach (Quad markupBox in MarkupBoxes)
                                {
                                    SKPoint[] points = markupBox.GetPoints();
                                    float markupBoxHeight = points[3].Y - points[0].Y;
                                    float boxYOffset = markupBoxHeight * lineYRatio + yOffset;
                                    composer.SetLineWidth(markupBoxHeight * .065);
                                    composer.DrawLine(
                                      new SKPoint(points[3].X, points[0].Y + boxYOffset),
                                      new SKPoint(points[2].X, points[1].Y + boxYOffset)
                                      );
                                }
                                composer.Stroke();
                            }
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            composer.Flush();
        }

        #endregion
        #endregion
        #endregion
    }
}