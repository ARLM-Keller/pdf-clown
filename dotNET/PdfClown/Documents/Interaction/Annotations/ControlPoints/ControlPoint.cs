﻿/*
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

using SkiaSharp;
//using System.Diagnostics;

namespace PdfClown.Documents.Interaction.Annotations.ControlPoints
{
    public abstract class ControlPoint
    {
        private const int r = 3;

        public Annotation Annotation { get; set; }

        public abstract SKPoint Point { get; set; }

        public SKPoint MappedPoint
        {
            get => Annotation.PageMatrix.MapPoint(Point);
            set => Point = Annotation.InvertPageMatrix.MapPoint(value);
        }

        public SKRect Bounds
        {
            get
            {
                var point = MappedPoint;
                return new SKRect(point.X - r, point.Y - r, point.X + r, point.Y + r);
            }
        }

        public virtual ControlPoint Clone(Annotation annotation)
        {
            var cloned = (ControlPoint)MemberwiseClone();
            cloned.Annotation = annotation;
            return cloned;
        }
    }

}