/*
  Copyright 2006-2011 Stefano Chizzolini. http://www.pdfclown.org

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
/**
 * Mozilla Pdf.js
 * CalGrayCS: Based on "PDF Reference, Sixth Ed", p.245
 *
 * The default color is `new Float32Array([0])`.
 */

using PdfClown.Documents;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>CIE-based A single-transformation-stage color space, where A represents a calibrated
      achromatic single-component color value [PDF:1.6:4.5.4].</summary>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class CalGrayColorSpace : CalColorSpace
    {
        private double XW;
        private double YW;
        private double ZW;
        private double XB;
        private double YB;
        private double ZB;
        private double G;
        #region dynamic
        #region constructors
        // TODO:IMPL new element constructor!

        internal CalGrayColorSpace(PdfDirectObject baseObject) : base(baseObject)
        {
            var gamma = Gamma[0];

            // Translate arguments to spec variables.
            var whitePoint = WhitePoint;
            this.XW = whitePoint[0];
            this.YW = whitePoint[1];
            this.ZW = whitePoint[2];

            var blackPoint = WhitePoint;
            this.XB = blackPoint[0];
            this.YB = blackPoint[1];
            this.ZB = blackPoint[2];

            this.G = gamma;
        }
        #endregion

        #region interface
        #region public
        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 1;

        public override Color DefaultColor => CalGrayColor.Default;

        public override float[] Gamma
        {
            get
            {
                IPdfNumber gammaObject = (IPdfNumber)Dictionary[PdfName.Gamma];
                return (gammaObject == null
                  ? new float[] { 1 }
                  : new float[] { gammaObject.FloatValue }
                  );
            }
        }

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        { return new CalGrayColor(components); }

        public override bool IsSpaceColor(Color color)
        { return color is CalGrayColor; }

        public override SKColor GetSKColor(Color color, float? alpha = null)
        {
            var grayColor = color as CalGrayColor;
            return Calculate(grayColor.G, alpha);
        }

        public override SKColor GetSKColor(float[] components, float? alpha = null)
        {
            return Calculate(components[0], alpha);
        }

        private SKColor Calculate(double A, float? alpha = null)
        {
            // A represents a gray component of a calibrated gray space.
            // A <---> AG in the spec
            var AG = Math.Pow(A, G);
            // Computes L as per spec. ( = cs.YW * AG )
            // Except if other than default BlackPoint values are used.
            var L = YW * AG;
            // http://www.poynton.com/notes/colour_and_gamma/ColorFAQ.html, Ch 4.
            // Convert values to rgb range [0, 255].
            var val = ToByte(Math.Max(295.8 * Math.Pow(L, 0.333333333333333333) - 40.8, 0));
            var skColor = new SKColor(val, val, val);

            if (alpha != null)
            {
                skColor = skColor.WithAlpha((byte)(alpha * 255));
            }
            return skColor;
        }



        #endregion
        #endregion
        #endregion
    }
}