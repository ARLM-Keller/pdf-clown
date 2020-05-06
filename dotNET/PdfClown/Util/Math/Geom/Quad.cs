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
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Util.Math.Geom
{
    /**
      <summary>Quadrilateral shape.</summary>
    */
    public struct Quad
    {
        #region static
        public static readonly Quad Empty = new Quad(SKRect.Empty);
        #region interface
        #region public
        public static Quad Union(Quad value, Quad value2)
        {
            return value.Union(value2);
        }

        public static Quad Transform(Quad quad, ref SKMatrix matrix)
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
        public SKPoint TopLeft => pointTopLeft;

        public SKPoint TopRight => pointTopRight;

        public SKPoint BottomRight => pointBottomRight;

        public SKPoint BottomLeft => pointBottomLeft;

        public SKPoint? Middle => SKLine.FindIntersection(new SKLine(pointTopLeft, pointBottomRight), new SKLine(pointBottomLeft, pointTopRight), false);

        public float Width => SKPoint.Distance(pointTopLeft, pointTopRight);

        public float HorizontalLength => Right - Left;

        public float Height => SKPoint.Distance(pointTopRight, pointBottomRight);

        public float VerticalLenght => Bottom - Top;

        public float Top =>
            System.Math.Min(pointTopLeft.Y,
                System.Math.Min(pointTopRight.Y,
                    System.Math.Min(pointBottomRight.Y, pointBottomLeft.Y)));

        public float Left =>
            System.Math.Min(pointTopLeft.X,
                System.Math.Min(pointTopRight.X,
                    System.Math.Min(pointBottomRight.X, pointBottomLeft.X)));

        public float Right =>
            System.Math.Max(pointTopLeft.X,
                System.Math.Max(pointTopRight.X,
                    System.Math.Max(pointBottomRight.X, pointBottomLeft.X)));

        public float Bottom =>
            System.Math.Max(pointTopLeft.Y,
                System.Math.Max(pointTopRight.Y,
                    System.Math.Max(pointBottomRight.Y, pointBottomLeft.Y)));

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

        public bool Contains(SKPoint p)
        {
            return (p - pointTopLeft).Cross(pointTopRight - pointTopLeft) <= 0
                && (p - pointTopRight).Cross(pointBottomRight - pointTopRight) <= 0
                && (p - pointBottomRight).Cross(pointBottomLeft - pointBottomRight) <= 0
                && (p - pointBottomLeft).Cross(pointTopLeft - pointBottomLeft) <= 0;
        }

        public bool Contains(float x, float y)
        {
            return Contains(new SKPoint(x, y));
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
            matrix.PreConcat(SKMatrix.MakeScale(1 + valueX * 2 / oldBounds.Width, 1 + valueY * 2 / oldBounds.Height));
            matrix.PreConcat(SKMatrix.MakeTranslation(-(oldBounds.MidX), -(oldBounds.MidY)));
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
            return SKLine.FindIntersection(new SKLine(pointTopLeft, pointTopRight), value, true) != null
                || SKLine.FindIntersection(new SKLine(pointTopRight, pointBottomRight), value, true) != null
                || SKLine.FindIntersection(new SKLine(pointBottomRight, pointBottomLeft), value, true) != null
                || SKLine.FindIntersection(new SKLine(pointBottomLeft, pointTopLeft), value, true) != null;
        }

        public bool Contains(Quad value)
        {
            return Contains(value.pointTopLeft)
                && Contains(value.pointTopRight)
                && Contains(value.pointBottomRight)
                && Contains(value.pointBottomLeft);
        }

        public Quad Union(Quad value)
        {
            Add(value.GetPoints());
            return this;
        }

        public void Add(SKPoint[] points)
        {
            KeyValuePair<float, SKPoint>? maxTopLeft = null;
            KeyValuePair<float, SKPoint>? maxTopRight = null;
            KeyValuePair<float, SKPoint>? maxBottomRight = null;
            KeyValuePair<float, SKPoint>? maxBottomLeft = null;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                if (Contains(point))
                {
                    continue;
                }
                var lengthTopLeft = (point - pointTopLeft).Length;
                var lengthTopRight = (point - pointTopRight).Length;
                var lengthBottomLeft = (point - pointBottomLeft).Length;
                var lengthBottomRight = (point - pointBottomRight).Length;
                var min = System.Math.Min(lengthTopLeft, System.Math.Min(lengthTopRight, System.Math.Min(lengthBottomLeft, lengthBottomRight)));
                if (min == lengthTopLeft)
                {
                    if (maxTopLeft == null)
                        maxTopLeft = new KeyValuePair<float, SKPoint>(min, point);
                    else if (min > maxTopLeft?.Key)
                        maxTopLeft = new KeyValuePair<float, SKPoint>(min, point);
                }
                else if (min == lengthTopRight)
                {
                    if (maxTopRight == null)
                        maxTopRight = new KeyValuePair<float, SKPoint>(min, point);
                    else if (min > maxTopRight?.Key)
                        maxTopRight = new KeyValuePair<float, SKPoint>(min, point);
                }
                else if (min == lengthBottomLeft)
                {
                    if (maxBottomLeft == null)
                        maxBottomLeft = new KeyValuePair<float, SKPoint>(min, point);
                    else if (min > maxBottomLeft?.Key)
                        maxBottomLeft = new KeyValuePair<float, SKPoint>(min, point);
                }
                else if (min == lengthBottomRight)
                {
                    if (maxBottomRight == null)
                        maxBottomRight = new KeyValuePair<float, SKPoint>(min, point);
                    else if (min > maxBottomRight?.Key)
                        maxBottomRight = new KeyValuePair<float, SKPoint>(min, point);
                }
            }
            if (maxTopLeft != null)
            {
                pointTopLeft = maxTopLeft.Value.Value;
            }
            if (maxTopRight != null)
            {
                pointTopRight = maxTopRight.Value.Value;
            }
            if (maxBottomLeft != null)
            {
                pointBottomLeft = maxBottomLeft.Value.Value;
            }
            if (maxBottomRight != null)
            {
                pointBottomRight = maxBottomRight.Value.Value;
            }
        }

        public void Add(SKPoint point)
        {
            if (Contains(point))
                return;
            var lengthTopLeft = (point - pointTopLeft).Length;
            var lengthTopRight = (point - pointTopRight).Length;
            var lengthBottomLeft = (point - pointBottomLeft).Length;
            var lengthBottomRight = (point - pointBottomRight).Length;
            var min = System.Math.Min(lengthTopLeft, System.Math.Min(lengthTopRight, System.Math.Min(lengthBottomLeft, lengthBottomRight)));
            if (min == lengthTopLeft)
            {
                pointTopLeft = point;
            }
            else if (min == lengthTopRight)
            {
                pointTopRight = point;
            }
            else if (min == lengthBottomLeft)
            {
                pointBottomLeft = point;
            }
            else if (min == lengthBottomRight)
            {
                pointBottomRight = point;
            }
            //if (point.X < pointTopLeft.X || point.X < pointBottomLeft.X)
            //{
            //    var newLine = new SKLine(point, pointTopLeft - pointBottomLeft);
            //    var newTopLeft = SKLine.FindIntersection(newLine, new SKLine(pointTopLeft, pointTopRight), false);
            //    var newBottomLeft = SKLine.FindIntersection(newLine, new SKLine(pointBottomLeft, pointBottomRight), false);
            //}
        }

        #endregion
        #endregion
        #endregion
    }
}