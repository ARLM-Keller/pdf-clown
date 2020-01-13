/*
  Copyright 2011-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using System;
using SkiaSharp;

namespace PdfClown.Util.Math.Geom
{
    /**
      <summary>Quadrilateral shape.</summary>
    */
    public struct Quad
    {
        #region static
        #region interface
        #region public
        public static Quad Union(Quad value, Quad value2)
        {
            return new Quad(SKRect.Union(value.GetBounds(), value2.GetBounds()));
        }

        public static Quad Transform(Quad quad, SKMatrix matrix)
        {
            var temp = new Quad(quad);
            temp.Transform(ref matrix);
            return temp;
        }

        #endregion
        #endregion
        #endregion

        #region dynamic
        #region fields
        private SKPoint pointTopLeft;
        private SKPoint pointTopRight;
        private SKPoint pointBottomRight;
        private SKPoint pointBottomLeft;
        #endregion

        #region constructors
        public Quad(SKRect rectangle)
            : this(new SKPoint(rectangle.Left, rectangle.Top),
                  new SKPoint(rectangle.Right, rectangle.Top),
                  new SKPoint(rectangle.Right, rectangle.Bottom),
                  new SKPoint(rectangle.Left, rectangle.Bottom))
        { }

        public Quad(Quad quad)
            : this(quad.pointTopLeft,
                  quad.pointTopRight,
                  quad.pointBottomRight,
                  quad.pointBottomLeft)
        { }

        public Quad(SKPoint pointTopLeft, SKPoint pointTopRight, SKPoint pointBottomRight, SKPoint pointBottomLeft)
        {
            this.pointTopLeft = pointTopLeft;
            this.pointTopRight = pointTopRight;
            this.pointBottomRight = pointBottomRight;
            this.pointBottomLeft = pointBottomLeft;
        }

        #endregion

        #region interface
        #region public

        public float Width => SKPoint.Distance(pointTopLeft, pointTopRight);

        public float Height => SKPoint.Distance(pointTopRight, pointBottomRight);

        public SKPoint Location => pointTopLeft;

        public float Top => pointTopLeft.Y;

        public float Left => pointTopLeft.X;

        public float Right => pointBottomRight.X;

        public float Bottom => pointBottomRight.Y;

        public SKPoint[] GetPoints()
        {
            return new[] { pointTopLeft, pointTopRight, pointBottomRight, pointBottomLeft };
        }
        #endregion

        #region private
        public SKPath GetPath()
        {
            var path = new SKPath();//FillMode.Alternate
            path.AddPoly(GetPoints());
            return path;
        }

        public bool Contains(SKPoint point)
        {
            return GetBounds().Contains(point);
        }

        public bool Contains(float x, float y)
        {
            return GetBounds().Contains(x, y);
        }

        public SKRect GetBounds()
        {
            var rect = new SKRect(pointTopLeft.X, pointTopLeft.Y, 0, 0);
            rect.Add(pointTopRight);
            rect.Add(pointBottomRight);
            rect.Add(pointBottomLeft);
            return rect;
        }

        //public SKPathMeasure GetPathIterator()
        //{
        //    return new SKPathMeasure(Path);
        //}

        /**
          <summary>Expands the size of this quad stretching around its center.</summary>
          <param name="value">Expansion extent.</param>
          <returns>This quad.</returns>
        */
        public Quad Inflate(float value)
        {
            return Inflate(value, value);
        }

        /**
          <summary>Expands the size of this quad stretching around its center.</summary>
          <param name="valueX">Expansion's horizontal extent.</param>
          <param name="valueY">Expansion's vertical extent.</param>
          <returns>This quad.</returns>
        */
        public Quad Inflate(float valueX, float valueY)
        {
            SKRect oldBounds = GetBounds();
            var matrix = SKMatrix.MakeTranslation(oldBounds.MidX, oldBounds.MidY);
            SKMatrix.PreConcat(ref matrix, SKMatrix.MakeScale(1 + valueX * 2 / oldBounds.Width, 1 + valueY * 2 / oldBounds.Height));
            SKMatrix.PreConcat(ref matrix, SKMatrix.MakeTranslation(-(oldBounds.MidX), -(oldBounds.MidY)));
            pointTopLeft = matrix.MapPoint(pointTopLeft);
            pointTopRight = matrix.MapPoint(pointTopRight);
            pointBottomRight = matrix.MapPoint(pointBottomRight);
            pointBottomLeft = matrix.MapPoint(pointBottomLeft);
            return this;
        }

        public void Transform(ref SKMatrix matrix)
        {
            pointTopLeft = matrix.MapPoint(pointTopLeft);
            pointTopRight = matrix.MapPoint(pointTopRight);
            pointBottomRight = matrix.MapPoint(pointBottomRight);
            pointBottomLeft = matrix.MapPoint(pointBottomLeft);
        }

        public bool IntersectsWith(Quad value)
        {
            return GetBounds().IntersectsWith(value.GetBounds());
        }

        public bool Contains(Quad value)
        {
            return GetBounds().Contains(value.GetBounds());
        }

        #endregion
        #endregion
        #endregion
    }
}