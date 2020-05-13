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
 * CalRGBCS: Based on "PDF Reference, Sixth Ed", p.247
 *
 * The default color is `new Float32Array([0, 0, 0])`.
 */

using PdfClown.Documents;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Functions;
using System.Linq;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>CIE-based ABC single-transformation-stage color space, where A, B, and C represent
      calibrated red, green and blue color values [PDF:1.6:4.5.4].</summary>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class CalRGBColorSpace : CalColorSpace
    {
        // See http://www.brucelindbloom.com/index.html?Eqn_ChromAdapt.html for these
        // matrices.
        // prettier-ignore
        public static readonly double[] BRADFORD_SCALE_MATRIX = new double[]{
          0.8951, 0.2664, -0.1614,
          -0.7502, 1.7135, 0.0367,
          0.0389, -0.0685, 1.0296 };

        // prettier-ignore
        public static readonly double[] BRADFORD_SCALE_INVERSE_MATRIX = new double[]{
          0.9869929, -0.1470543, 0.1599627,
          0.4323053, 0.5183603, 0.0492912,
          -0.0085287, 0.0400428, 0.9684867 };

        // See http://www.brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html.
        // prettier-ignore
        public static readonly double[] SRGB_D65_XYZ_TO_RGB_MATRIX = new double[]{
          3.2404542, -1.5371385, -0.4985314,
          -0.9692660, 1.8760108, 0.0415560,
          0.0556434, -0.2040259, 1.0572252 };

        public static readonly double[] FLAT_WHITEPOINT_MATRIX = new double[] { 1, 1, 1 };

        public static readonly double DECODE_L_CONSTANT = Math.Pow((8 + 16) / 116, 3) / 8.0;

        static void MatrixProduct(double[] a, double[] b, double[] result)
        {
            result[0] = a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
            result[1] = a[3] * b[0] + a[4] * b[1] + a[5] * b[2];
            result[2] = a[6] * b[0] + a[7] * b[1] + a[8] * b[2];
        }

        static void ConvertToFlat(double[] sourceWhitePoint, double[] LMS, double[] result)
        {
            result[0] = (LMS[0] * 1) / sourceWhitePoint[0];
            result[1] = (LMS[1] * 1) / sourceWhitePoint[1];
            result[2] = (LMS[2] * 1) / sourceWhitePoint[2];
        }

        static void convertToD65(double[] sourceWhitePoint, double[] LMS, double[] result)
        {
            const double D65X = 0.95047;
            const double D65Y = 1;
            const double D65Z = 1.08883;

            result[0] = (LMS[0] * D65X) / sourceWhitePoint[0];
            result[1] = (LMS[1] * D65Y) / sourceWhitePoint[1];
            result[2] = (LMS[2] * D65Z) / sourceWhitePoint[2];
        }

        static double SRGBTransferFunction(double color)
        {
            // See http://en.wikipedia.org/wiki/SRGB.
            if (color <= 0.0031308)
            {
                return AdjustToRange(0, 1, 12.92 * color);
            }
            return AdjustToRange(0, 1, Math.Pow((1 + 0.055) * color, (1 / 2.4)) - 0.055);
        }

        static double AdjustToRange(double min, double max, double value)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        static double DecodeL(double L)
        {
            if (L < 0)
            {
                return -DecodeL(-L);
            }
            if (L > 8.0)
            {
                return Math.Pow((L + 16) / 116, 3);
            }
            return L * DECODE_L_CONSTANT;
        }

        static void CompensateBlackPoint(double[] sourceBlackPoint, double[] XYZ_Flat, double[] result)
        {
            // In case the blackPoint is already the default blackPoint then there is
            // no need to do compensation.
            if (
              sourceBlackPoint[0] == 0 &&
              sourceBlackPoint[1] == 0 &&
              sourceBlackPoint[2] == 0
            )
            {
                result[0] = XYZ_Flat[0];
                result[1] = XYZ_Flat[1];
                result[2] = XYZ_Flat[2];
                return;
            }

            // For the blackPoint calculation details, please see
            // http://www.adobe.com/content/dam/Adobe/en/devnet/photoshop/sdk/
            // AdobeBPC.pdf.
            // The destination blackPoint is the default blackPoint [0, 0, 0].
            var zeroDecodeL = DecodeL(0);

            var X_DST = zeroDecodeL;
            var X_SRC = DecodeL(sourceBlackPoint[0]);

            var Y_DST = zeroDecodeL;
            var Y_SRC = DecodeL(sourceBlackPoint[1]);

            var Z_DST = zeroDecodeL;
            var Z_SRC = DecodeL(sourceBlackPoint[2]);

            var X_Scale = (1 - X_DST) / (1 - X_SRC);
            var X_Offset = 1 - X_Scale;

            var Y_Scale = (1 - Y_DST) / (1 - Y_SRC);
            var Y_Offset = 1 - Y_Scale;

            var Z_Scale = (1 - Z_DST) / (1 - Z_SRC);
            var Z_Offset = 1 - Z_Scale;

            result[0] = XYZ_Flat[0] * X_Scale + X_Offset;
            result[1] = XYZ_Flat[1] * Y_Scale + Y_Offset;
            result[2] = XYZ_Flat[2] * Z_Scale + Z_Offset;
        }

        static void NormalizeWhitePointToFlat(double[] sourceWhitePoint, double[] XYZ_In, double[] result)
        {
            // In case the whitePoint is already flat then there is no need to do
            // normalization.
            if (sourceWhitePoint[0] == 1 && sourceWhitePoint[2] == 1)
            {
                result[0] = XYZ_In[0];
                result[1] = XYZ_In[1];
                result[2] = XYZ_In[2];
                return;
            }

            var LMS = result;
            MatrixProduct(BRADFORD_SCALE_MATRIX, XYZ_In, LMS);

            var LMS_Flat = new double[3];
            ConvertToFlat(sourceWhitePoint, LMS, LMS_Flat);

            MatrixProduct(BRADFORD_SCALE_INVERSE_MATRIX, LMS_Flat, result);
        }

        static void NormalizeWhitePointToD65(double[] sourceWhitePoint, double[] XYZ_In, double[] result)
        {
            var LMS = result;
            MatrixProduct(BRADFORD_SCALE_MATRIX, XYZ_In, LMS);

            var LMS_D65 = new double[3];
            convertToD65(sourceWhitePoint, LMS, LMS_D65);

            MatrixProduct(BRADFORD_SCALE_INVERSE_MATRIX, LMS_D65, result);
        }

        #region dynamic

        private double[] blackPoint;
        private double[] whitePoint;
        private double[] gamma;
        private double[] matrix;
        private double GR;
        private double GG;
        private double GB;
        private double MXA;
        private double MYA;
        private double MZA;
        private double MXB;
        private double MYB;
        private double MZB;
        private double MXC;
        private double MYC;
        private double MZC;

        #region constructors
        //TODO:IMPL new element constructor!

        internal CalRGBColorSpace(PdfDirectObject baseObject) : base(baseObject)
        {
            blackPoint = BlackPoint;
            whitePoint = WhitePoint;
            gamma = Gamma;
            var matrixData = (PdfArray)Dictionary.Resolve(PdfName.Matrix);
            matrix = matrixData?.Select(p => ((IPdfNumber)p).RawValue).ToArray() ?? new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            // Translate arguments to spec variables.
            this.GR = gamma[0];
            this.GG = gamma[1];
            this.GB = gamma[2];

            this.MXA = matrix[0];
            this.MYA = matrix[1];
            this.MZA = matrix[2];
            this.MXB = matrix[3];
            this.MYB = matrix[4];
            this.MZB = matrix[5];
            this.MXC = matrix[6];
            this.MYC = matrix[7];
            this.MZC = matrix[8];
        }
        #endregion

        #region interface
        #region public
        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 3;

        public override Color DefaultColor => CalRGBColor.Default;

        public override double[] Gamma
        {
            get
            {
                PdfArray gamma = (PdfArray)Dictionary[PdfName.Gamma];
                return (gamma == null
                  ? new double[] { 1, 1, 1 }
                  : new double[] { ((IPdfNumber)gamma[0]).RawValue, ((IPdfNumber)gamma[1]).RawValue, ((IPdfNumber)gamma[2]).RawValue }
                  );
            }
        }

        public SKMatrix Matrix
        {
            get
            {
                PdfArray matrix = (PdfArray)Dictionary.Resolve(PdfName.Matrix);
                if (matrix == null)
                    return SKMatrix.MakeIdentity();
                else
                    return new SKMatrix
                    {
                        ScaleX = ((IPdfNumber)matrix[0]).FloatValue,
                        SkewY = ((IPdfNumber)matrix[1]).FloatValue,
                        SkewX = ((IPdfNumber)matrix[2]).FloatValue,
                        ScaleY = ((IPdfNumber)matrix[3]).FloatValue,
                        TransX = ((IPdfNumber)matrix[4]).FloatValue,
                        TransY = ((IPdfNumber)matrix[5]).FloatValue,
                        Persp2 = 1
                    };
            }
            set => Dictionary[PdfName.Matrix] =
                 new PdfArray(
                    PdfReal.Get(value.ScaleX),
                    PdfReal.Get(value.SkewY),
                    PdfReal.Get(value.SkewX),
                    PdfReal.Get(value.ScaleY),
                    PdfReal.Get(value.TransX),
                    PdfReal.Get(value.TransY)
                    );
        }

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        { return new CalRGBColor(components); }

        public override bool IsSpaceColor(Color color)
        { return color is CalRGBColor; }

        public override SKColor GetSKColor(Color color, double? alpha = null)
        {
            var calColor = (CalRGBColor)color;
            // FIXME: temporary hack
            return Calculate(calColor.R, calColor.G, calColor.B, alpha);
        }

        public override SKColor GetSKColor(double[] components, double? alpha = null)
        {
            return Calculate(components[0], components[1], components[2], alpha);
        }

        public SKColor Calculate(double a, double b, double c, double? alpha = null)
        {
            // A, B and C represent a red, green and blue components of a calibrated
            // rgb space.
            var A = AdjustToRange(0, 1, a);
            var B = AdjustToRange(0, 1, b);
            var C = AdjustToRange(0, 1, c);

            // A <---> AGR in the spec
            // B <---> BGG in the spec
            // C <---> CGB in the spec
            var AGR = Math.Pow(A, GR);
            var BGG = Math.Pow(B, GG);
            var CGB = Math.Pow(C, GB);

            // Computes intermediate variables L, M, N as per spec.
            // To decode X, Y, Z values map L, M, N directly to them.
            var X = MXA * AGR + MXB * BGG + MXC * CGB;
            var Y = MYA * AGR + MYB * BGG + MYC * CGB;
            var Z = MZA * AGR + MZB * BGG + MZC * CGB;

            // The following calculations are based on this document:
            // http://www.adobe.com/content/dam/Adobe/en/devnet/photoshop/sdk/
            // AdobeBPC.pdf.
            var XYZ = new double[3];
            XYZ[0] = X;
            XYZ[1] = Y;
            XYZ[2] = Z;
            var XYZ_Flat = new double[3];

            NormalizeWhitePointToFlat(whitePoint, XYZ, XYZ_Flat);

            var XYZ_Black = new double[3];
            CompensateBlackPoint(blackPoint, XYZ_Flat, XYZ_Black);

            var XYZ_D65 = new double[3];
            NormalizeWhitePointToD65(FLAT_WHITEPOINT_MATRIX, XYZ_Black, XYZ_D65);

            var SRGB = new double[3];
            MatrixProduct(SRGB_D65_XYZ_TO_RGB_MATRIX, XYZ_D65, SRGB);

            // Convert the values to rgb range [0, 255].
            var skColor = new SKColor(ToByte(SRGBTransferFunction(SRGB[0]) * 255),
                ToByte(SRGBTransferFunction(SRGB[1]) * 255),
                ToByte(SRGBTransferFunction(SRGB[2]) * 255));
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