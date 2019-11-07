/*
  Copyright 2008-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Objects;

using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Begin a new subpath by moving the current point' operation [PDF:1.6:4.4.1].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class BeginSubpath
      : Operation
    {
        #region static
        #region fields
        public static readonly string OperatorKeyword = "m";
        #endregion
        #endregion

        #region dynamic
        #region constructors
        /**
          <param name="point">Current point.</param>
        */
        public BeginSubpath(SKPoint point) : this(point.X, point.Y)
        { }

        /**
          <param name="pointX">Current point X.</param>
          <param name="pointY">Current point Y.</param>
        */
        public BeginSubpath(double pointX, double pointY)
            : base(OperatorKeyword, new List<PdfDirectObject>(new PdfDirectObject[]
              {
                  PdfReal.Get(pointX),
                  PdfReal.Get(pointY)
              }
              )
            )
        { }

        public BeginSubpath(IList<PdfDirectObject> operands) : base(OperatorKeyword, operands)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the current point.</summary>
        */
        public SKPoint Point
        {
            get => new SKPoint(
                  ((IPdfNumber)operands[0]).FloatValue,
                  ((IPdfNumber)operands[1]).FloatValue
                  );
            set
            {
                operands[0] = PdfReal.Get(value.X);
                operands[1] = PdfReal.Get(value.Y);
            }
        }

        public override void Scan(ContentScanner.GraphicsState state)
        {
            var pathObject = state.Scanner.RenderObject;
            if (pathObject != null)
            {
                pathObject.MoveTo(Point);
            }
        }
        #endregion
        #endregion
        #endregion
    }
}