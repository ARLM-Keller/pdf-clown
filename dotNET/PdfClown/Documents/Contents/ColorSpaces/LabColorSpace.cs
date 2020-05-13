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
 * Mozilla Ppf.jz
 * LabCS: Based on "PDF Reference, Sixth Ed", p.250
 *
 * The default color is `new Float32Array([0, 0, 0])`.
 */

using PdfClown.Documents;
using PdfClown.Objects;
using PdfClown.Util.Math;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>CIE-based ABC double-transformation-stage color space, where A, B and C represent the
      L*, a* and b* components of a CIE 1976 L*a*b* space [PDF:1.6:4.5.4].</summary>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class LabColorSpace : CIEBasedColorSpace
    {
        #region dynamic
        private double[] blackPoint;
        private double[] whitePoint;
        private IList<Interval<double>> range;
        private double XW;
        private double YW;
        private double ZW;
        private double amin;
        private double amax;
        private double bmin;
        private double bmax;
        private double XB;
        private double YB;
        private double ZB;
        #region constructors
        //TODO:IMPL new element constructor!

        internal LabColorSpace(PdfDirectObject baseObject) : base(baseObject)
        {
            blackPoint = BlackPoint;
            whitePoint = WhitePoint;
            range = Ranges;// || [-100, 100, -100, 100];

            // Translate args to spec variables
            this.XW = whitePoint[0];
            this.YW = whitePoint[1];
            this.ZW = whitePoint[2];
            this.amin = range[0].Low;
            this.amax = range[0].High;
            this.bmin = range[1].Low;
            this.bmax = range[1].High;

            // These are here just for completeness - the spec doesn't offer any
            // formulas that use BlackPoint in Lab
            this.XB = blackPoint[0];
            this.YB = blackPoint[1];
            this.ZB = blackPoint[2];
        }
        #endregion

        #region interface
        #region public
        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 3;

        public override Color DefaultColor
        {
            get
            {
                IList<Interval<double>> ranges = Ranges;
                return new LabColor(ranges[0].Low, ranges[1].Low, ranges[2].Low);
            }
        }

        /**
        <summary>Gets the (inclusive) ranges of the color components.</summary>
        <remarks>Component values falling outside the specified range are adjusted
        to the nearest valid value.</remarks>
      */
        //TODO:generalize to all the color spaces!
        public IList<Interval<double>> Ranges
        {
            get
            {
                var ranges = new List<Interval<double>>();
                {
                    // 1. L* component.
                    ranges.Add(new Interval<double>(0d, 100d));

                    PdfArray rangesObject = (PdfArray)Dictionary[PdfName.Range];
                    if (rangesObject == null)
                    {
                        // 2. a* component.
                        ranges.Add(
                          new Interval<double>(-100d, 100d)
                          );
                        // 3. b* component.
                        ranges.Add(
                          new Interval<double>(-100d, 100d)
                          );
                    }
                    else
                    {
                        // 2/3. a*/b* components.
                        for (int index = 0, length = rangesObject.Count; index < length; index += 2)
                        {
                            ranges.Add(
                              new Interval<double>(
                                ((IPdfNumber)rangesObject[index]).RawValue,
                                ((IPdfNumber)rangesObject[index + 1]).RawValue
                                )
                              );
                        }
                    }
                }
                return ranges;
            }
        }

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        { return new LabColor(components); }

        public override bool IsSpaceColor(Color color)
        { return color is LabColor; }

        public override SKColor GetSKColor(Color color, double? alpha = null)
        {
            var labColor = (LabColor)color;
            return Calculate(labColor.L, labColor.A, labColor.B, null, alpha);
        }

        public override SKColor GetSKColor(double[] components, double? alpha = null)
        {
            return Calculate(components[0], components[1], components[2], null, alpha);
        }

        // Function g(x) from spec
        private double FnG(double x)
        {
            double result;
            if (x >= 6 / 29)
            {
                result = x * x * x;
            }
            else
            {
                result = (108 / 841) * (x - 4 / 29);
            }
            return result;
        }

        private double Decode(double value, double high1, double low2, double high2)
        {
            return low2 + (value * (high2 - low2)) / high1;
        }

        // If decoding is needed maxVal should be 2^bits per component - 1.
        public SKColor Calculate(double Ls, double As, double Bs, double? maxVal, double? alpha = null)
        {
            // XXX: Lab input is in the range of [0, 100], [amin, amax], [bmin, bmax]
            // not the usual [0, 1]. If a command like setFillColor is used the src
            // values will already be within the correct range. However, if we are
            // converting an image we have to map the values to the correct range given
            // above.
            // Ls,as,bs <---> L*,a*,b* in the spec
            if (maxVal != null)
            {
                Ls = Decode(Ls, (double)maxVal, 0, 100);
                As = Decode(As, (double)maxVal, amin, amax);
                Bs = Decode(Bs, (double)maxVal, bmin, bmax);
            }

            // Adjust limits of 'as' and 'bs'
            if (As > amax)
            {
                As = amax;
            }
            else if (As < amin)
            {
                As = amin;
            }
            if (Bs > bmax)
            {
                Bs = bmax;
            }
            else if (Bs < bmin)
            {
                Bs = bmin;
            }

            // Computes intermediate variables X,Y,Z as per spec
            var M = (Ls + 16) / 116;
            var L = M + As / 500;
            var N = M - Bs / 200;

            var X = XW * FnG(L);
            var Y = YW * FnG(M);
            var Z = ZW * FnG(N);

            double r, g, b;
            // Using different conversions for D50 and D65 white points,
            // per http://www.color.org/srgb.pdf
            if (ZW < 1)
            {
                // Assuming D50 (X=0.9642, Y=1.00, Z=0.8249)
                r = X * 3.1339 + Y * -1.617 + Z * -0.4906;
                g = X * -0.9785 + Y * 1.916 + Z * 0.0333;
                b = X * 0.072 + Y * -0.229 + Z * 1.4057;
            }
            else
            {
                // Assuming D65 (X=0.9505, Y=1.00, Z=1.0888)
                r = X * 3.2406 + Y * -1.5372 + Z * -0.4986;
                g = X * -0.9689 + Y * 1.8758 + Z * 0.0415;
                b = X * 0.0557 + Y * -0.204 + Z * 1.057;
            }
            // Convert the color values to the [0,255] range (clamping is automatic).
            var skColor = new SKColor(
                ToByte(Math.Sqrt(r) * 255),
                ToByte(Math.Sqrt(g) * 255),
                ToByte(Math.Sqrt(b) * 255));
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