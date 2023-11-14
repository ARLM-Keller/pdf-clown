/*
  Copyright 2010-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Functions;
using PdfClown.Objects;
using PdfClown.Util.Collections;
using PdfClown.Util.Math;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PdfClown.Documents.Contents.Patterns.Shadings
{
    public class FreeFormShading : Shading
    {
        private float[] decode;
        private IList<Interval<float>> decodes;
        private int numberOfColorComponents = -1;

        internal FreeFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public FreeFormShading()
        {
            ShadingType = 4;
        }

        public int BitsPerCoordinate
        {
            get => Dictionary.GetInt(PdfName.BitsPerCoordinate);
            set => Dictionary.SetInt(PdfName.BitsPerCoordinate, value);
        }

        public int BitsPerComponent
        {
            get => Dictionary.GetInt(PdfName.BitsPerComponent);
            set => Dictionary.SetInt(PdfName.BitsPerComponent, value);
        }

        public int BitsPerFlag
        {
            get => Dictionary.GetInt(PdfName.BitsPerFlag);
            set => Dictionary.SetInt(PdfName.BitsPerFlag, value);
        }

        public Function Function
        {
            get => Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public float[] Decode
        {
            get => decode ??= Dictionary.Resolve(PdfName.Decode) is PdfArray array
                        ? array.Select(p => ((IPdfNumber)p).FloatValue).ToArray()
                        : new float[0];
            set
            {
                decode = value;
                Dictionary[PdfName.Domain] = new PdfArray(value.Select(p => PdfReal.Get(p)));
            }
        }

        public IList<Interval<float>> Decodes => decodes ??= Decode.GetIntervals<float>();

        public int NumberOfColorComponents
        {
            get
            {
                if (numberOfColorComponents == -1)
                {
                    numberOfColorComponents = Function != null ? 1
                            : ColorSpace.ComponentCount;
                }
                return numberOfColorComponents;
            }
        }


        public virtual void Load()
        {

        }

        public override SKShader GetShader(SKMatrix sKMatrix, GraphicsState state)
        {
            (var points, var colors) = CollectTriangles(sKMatrix);
            var vertexes = SKVertices.CreateCopy(SKVertexMode.Triangles, points.ToArray(), colors.ToArray());
            var minX = points.Min(x => x.X);
            var minY = points.Min(x => x.Y);
            var maxX = points.Max(x => x.X);
            var maxY = points.Max(x => x.Y);
            var rect = new SKRect(minX, minY, maxX, maxY);
            using var recorder = SKSurface.Create(new SKImageInfo((int)rect.Width, (int)rect.Height));
            using var paint = new SKPaint { IsAntialias = true };
            recorder.Canvas.DrawVertices(vertexes, SKBlendMode.Overlay, paint);
            if (state?.Scanner?.RenderObject is SKPath path)
            {
                var pathRect = path.Bounds;
                var matrix = SKMatrix.CreateScale(pathRect.Width / rect.Width, pathRect.Height / rect.Height);
                return SKShader.CreateImage(recorder.Snapshot(), SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, matrix);
            }
            return SKShader.CreateImage(recorder.Snapshot(), SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        }

        /**
        * Calculate the interpolation, see p.345 pdf spec 1.7.
        *
        * @param src src value
        * @param srcMax max src value (2^bits-1)
        * @param dstMin min dst value
        * @param dstMax max dst value
        * @return interpolated value
        */
        protected float Interpolate(float src, long srcMax, float dstMin, float dstMax)
        {
            return dstMin + src * (dstMax - dstMin) / srcMax;
        }

        /**
        * Read a vertex from the bit input stream performs interpolations.
        *
        * @param input bit input stream
        * @param maxSrcCoord max value for source coordinate (2^bits-1)
        * @param maxSrcColor max value for source color (2^bits-1)
        * @param rangeX dest range for X
        * @param rangeY dest range for Y
        * @param colRangeTab dest range array for colors
        * @param matrix the pattern matrix concatenated with that of the parent content stream
        * @param xform the affine transformation
        * @return a new vertex with the flag and the interpolated values
        * @throws IOException if something went wrong
        */
        protected (SKPoint, SKColor) ReadVertex(IInputStream input, long maxSrcCoord, long maxSrcColor,
                                    Interval<float> rangeX, Interval<float> rangeY, Interval<float>[] colRangeTab,
                                    SKMatrix matrix)
        {
            Span<float> colorComponentTab = stackalloc float[NumberOfColorComponents];
            long x = input.ReadBits(BitsPerCoordinate);
            long y = input.ReadBits(BitsPerCoordinate);
            float dstX = Interpolate(x, maxSrcCoord, rangeX.Low, rangeX.High);
            float dstY = Interpolate(y, maxSrcCoord, rangeY.Low, rangeY.High);
            var p = matrix.MapPoint(dstX, dstY);

            for (int n = 0; n < colorComponentTab.Length; ++n)
            {
                int color = (int)input.ReadBits(BitsPerComponent);
                colorComponentTab[n] = Interpolate(color, maxSrcColor, colRangeTab[n].Low, colRangeTab[n].High);
            }

            // "Each set of vertex data shall occupy a whole number of bytes.
            // If the total number of bits required is not divisible by 8, the last data byte
            // for each vertex is padded at the end with extra bits, which shall be ignored."
            input.ByteAlign();
            var skColor = ColorSpace.GetSKColor(colorComponentTab);
            return (p, skColor);
        }

        protected virtual (List<SKPoint> points, List<SKColor> colors) CollectTriangles(SKMatrix matrix)
        {
            int bitsPerFlag = BitsPerFlag;
            var dict = BaseDataObject;
            if (!(dict is PdfStream stream))
            {
                return (null, null);
            }
            var rangeX = Decodes[0];
            var rangeY = Decodes[1];
            if (rangeX == null || rangeY == null ||
                rangeX.Low.CompareTo(rangeX.High) == 0 ||
                rangeY.Low.CompareTo(rangeY.High) == 0)
            {
                return (null, null);
            }
            var colRange = new Interval<float>[NumberOfColorComponents];
            for (int i = 0; i < colRange.Length; ++i)
            {
                colRange[i] = Decodes[2 + i];
                if (colRange[i] == null)
                {
                    throw new IOException("Range missing in shading /Decode entry");
                }
            }

            var points = new List<SKPoint>();
            var colors = new List<SKColor>();
            long maxSrcCoord = (long)Math.Pow(2, BitsPerCoordinate) - 1;
            long maxSrcColor = (long)Math.Pow(2, BitsPerComponent) - 1;
            var vertextBitLength = GetVertextBitLength();
            var triangleBitLength = GetTriangleBitLength(vertextBitLength);
            var vertextLength = vertextBitLength / 8;
            var triangleLength = triangleBitLength / 8;

            using (var input = stream.ExtractBody(true))
            {
                byte flag = 0;
                try
                {
                    flag = (byte)(input.ReadBits(bitsPerFlag) & 3);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                while (true)
                {
                    (SKPoint point, SKColor color) p0;
                    (SKPoint point, SKColor color) p1;
                    (SKPoint point, SKColor color) p2;
                    int index;
                    try
                    {
                        switch (flag)
                        {
                            case 0:
                                if (input.Position + triangleLength > input.Length)
                                    return (points, colors);
                                p0 = ReadVertex(input, maxSrcCoord, maxSrcColor, rangeX, rangeY, colRange, matrix);
                                flag = (byte)(input.ReadBits(bitsPerFlag) & 3);
                                if (flag != 0)
                                {
                                    Debug.WriteLine($"error: bad triangle: {flag}");
                                }
                                p1 = ReadVertex(input, maxSrcCoord, maxSrcColor, rangeX, rangeY, colRange, matrix);
                                flag = (byte)(input.ReadBits(bitsPerFlag) & 3);
                                if (flag != 0)
                                {
                                    Debug.WriteLine($"error: bad triangle: {flag}");
                                }
                                p2 = ReadVertex(input, maxSrcCoord, maxSrcColor, rangeX, rangeY, colRange, matrix);

                                break;
                            case 1:
                            case 2:
                                index = flag == 1 ? points.Count - 2 : points.Count - 3;
                                if (index < 0)
                                {
                                    Debug.WriteLine($"error: broken data stream");
                                    return (points, colors);
                                }
                                else
                                {
                                    if (input.Position + vertextLength > input.Length)
                                        return (points, colors);

                                    p0 = (points[index], colors[index]);
                                    p1 = (points[points.Count - 1], colors[points.Count - 1]);
                                    p2 = ReadVertex(input, maxSrcCoord, maxSrcColor, rangeX, rangeY, colRange, matrix);
                                }
                                break;
                            default:
                                Debug.WriteLine($"warn: bad flag: {flag}");
                                return (points, colors);
                        }
                        points.Add(p0.point);
                        colors.Add(p0.color);
                        points.Add(p1.point);
                        colors.Add(p1.color);
                        points.Add(p2.point);
                        colors.Add(p2.color);
                        if (input.Position >= input.Length)
                            break;
                        flag = (byte)(input.ReadBits(bitsPerFlag) & 3);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            return (points, colors);
        }

        private int GetTriangleBitLength(int vertextBitLength)
        {
            return vertextBitLength * 3 + 2 * BitsPerFlag;
        }

        protected int GetVertextBitLength()
        {
            return 2 * BitsPerCoordinate + NumberOfColorComponents * BitsPerComponent;
        }
    }
}
