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

using PdfClown.Objects;
using PdfClown.Util.Math;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfClown.Documents.Contents.Patterns.Shadings
{
    public class LatticeFormShading : FreeFormShading
    {
        internal LatticeFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public LatticeFormShading()
        {
            ShadingType = 5;
        }

        /**
     * The vertices per row of this shading. This will return -1 if one has not
     * been set.
     *
     * @return the number of vertices per row
     */
        public int VerticesPerRow
        {
            get => Dictionary.GetInt(PdfName.VerticesPerRow, -1);
            set => Dictionary.SetInt(PdfName.VerticesPerRow, value);
        }

        protected override (List<SKPoint> points, List<SKColor> colors) CollectTriangles(SKMatrix matrix)
        {
            var dict = BaseDataObject;
            if (dict is not PdfStream stream)
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

            int numPerRow = VerticesPerRow;
            var colRange = new Interval<float>[NumberOfColorComponents];
            for (int i = 0; i < colRange.Length; ++i)
            {
                colRange[i] = Decodes[2 + i];
                if (colRange[i] == null)
                {
                    throw new IOException("Range missing in shading /Decode entry");
                }
            }
            var vlist = new List<(SKPoint, SKColor)>();
            long maxSrcCoord = (long)Math.Pow(2, BitsPerCoordinate) - 1;
            long maxSrcColor = (long)Math.Pow(2, BitsPerComponent) - 1;
            var vertextLength = GetVertextBitLength() / 8;
            using (var mciis = stream.ExtractBody(true))
            {
                while (mciis.Position + vertextLength <= mciis.Length)
                {
                    try
                    {
                        var p = ReadVertex(mciis, maxSrcCoord, maxSrcColor, rangeX, rangeY, colRange, matrix);
                        vlist.Add(p);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }
            int rowNum = vlist.Count / numPerRow;
            if (rowNum < 2)
            {
                // must have at least two rows; if not, return empty list
                return (null, null);
            }
            var latticeArray = new (SKPoint, SKColor)[rowNum, numPerRow];
            for (int i = 0; i < rowNum; i++)
            {
                for (int j = 0; j < numPerRow; j++)
                {
                    latticeArray[i, j] = vlist[i * numPerRow + j];
                }
            }

            return CreateShadedTriangleList(rowNum, numPerRow, latticeArray);
        }

        private (List<SKPoint> points, List<SKColor> colors) CreateShadedTriangleList(int rowNum, int numPerRow, (SKPoint point, SKColor color)[,] latticeArray)
        {
            var ps = new List<SKPoint>(); // array will be shallow-cloned in ShadedTriangle constructor
            var cs = new List<SKColor>();
            for (int i = 0; i < rowNum - 1; i++)
            {
                for (int j = 0; j < numPerRow - 1; j++)
                {
                    ps.Add(latticeArray[i, j].point);
                    cs.Add(latticeArray[i, j].color);
                    ps.Add(latticeArray[i, j + 1].point);
                    cs.Add(latticeArray[i, j + 1].color);
                    ps.Add(latticeArray[i + 1, j].point);
                    cs.Add(latticeArray[i + 1, j].color);

                    ps.Add(latticeArray[i, j + 1].point);
                    cs.Add(latticeArray[i, j + 1].color);
                    ps.Add(latticeArray[i + 1, j].point);
                    cs.Add(latticeArray[i + 1, j].color);
                    ps.Add(latticeArray[i + 1, j + 1].point);
                    cs.Add(latticeArray[i + 1, j + 1].color);
                }
            }
            return (ps, cs);
        }
    }
}
