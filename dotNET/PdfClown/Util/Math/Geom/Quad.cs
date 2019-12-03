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
    public class Quad
    {
        #region static
        #region interface
        #region public
        public static Quad Get(SKRect rectangle)
        { return new Quad(GetPoints(rectangle)); }

        public static SKPoint[] GetPoints(SKRect rectangle)
        {
            SKPoint[] points = new SKPoint[4];
            {
                points[0] = new SKPoint(rectangle.Left, rectangle.Top);
                points[1] = new SKPoint(rectangle.Right, rectangle.Top);
                points[2] = new SKPoint(rectangle.Right, rectangle.Bottom);
                points[3] = new SKPoint(rectangle.Left, rectangle.Bottom);
            }
            return points;
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region fields
        private SKPoint[] points;
        #endregion

        #region constructors
        public Quad(params SKPoint[] points)
        {
            Points = points;
        }
        #endregion

        #region interface
        #region public

        public SKPoint[] Points
        {
            get => points;
            set
            {
                if (value.Length != 4)
                    throw new ArgumentException("Cardinality MUST be 4.", "points");

                points = value;
            }
        }
        #endregion

        #region private
        public SKPath GetPath()
        {
            var path = new SKPath();//FillMode.Alternate
            path.AddPoly(points);
            return path;
        }

        public float Width => SKPoint.Distance(points[0], points[1]);

        public float Height => SKPoint.Distance(points[1], points[2]);

        public SKPoint Location => points[0];

        public bool Contains(SKPoint SKPoint)
        {
            return GetBounds().Contains(SKPoint.X, SKPoint.Y);
        }

        public bool Contains(float x, float y)
        {
            return GetBounds().Contains(x, y);
        }

        public SKRect GetBounds()
        {
            var rect = new SKRect();
            rect.Add(points);
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
            points = SKMatrix.MakeTranslation(-oldBounds.Left, -oldBounds.Top).MapPoints(points);
            points = SKMatrix.MakeScale(1 + valueX * 2 / oldBounds.Width, 1 + valueY * 2 / oldBounds.Height).MapPoints(points);
            SKRect newBounds = GetBounds();
            points = SKMatrix.MakeTranslation(oldBounds.Left - (newBounds.Width - oldBounds.Width) / 2, oldBounds.Top - (newBounds.Height - oldBounds.Height) / 2).MapPoints(points);
            return this;
        }

        public void Transform(SKMatrix matrix)
        {
            points = matrix.MapPoints(points);
        }

        #endregion
        #endregion
        #endregion
    }
}