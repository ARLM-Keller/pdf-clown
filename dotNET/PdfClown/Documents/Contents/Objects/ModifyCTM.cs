/*
  Copyright 2007-2012 Stefano Chizzolini. http://www.pdfclown.org

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
      <summary>'Modify the current transformation matrix (CTM) by concatenating the specified SKMatrix'
      operation [PDF:1.6:4.3.3].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class ModifyCTM : Operation
    {
        public static readonly string OperatorKeyword = "cm";

        public static ModifyCTM GetResetCTM(GraphicsState state)
        {
            state.Ctm.TryInvert(out var inverseCtm);
            return new ModifyCTM(
              inverseCtm
              // TODO: inverseCtm is a simplification which assumes an identity initial ctm!
              //        SquareMatrix.get(state.Ctm).solve(
              //          SquareMatrix.get(state.GetInitialCtm())
              //          ).toTransform()
              );
        }

        public ModifyCTM(SKMatrix value) : this(
            value.ScaleX,
            value.SkewY,
            value.SkewX,
            value.ScaleY,
            value.TransX,
            value.TransY
            )
        { }

        public ModifyCTM(double a, double b, double c, double d, double e, double f)
            : base(OperatorKeyword,
            new List<PdfDirectObject>(6)
              {
            PdfReal.Get(a),
            PdfReal.Get(b),
            PdfReal.Get(c),
            PdfReal.Get(d),
            PdfReal.Get(e),
            PdfReal.Get(f)
              })
        { }

        public ModifyCTM(IList<PdfDirectObject> operands) : base(OperatorKeyword, operands)
        { }

        public override void Scan(GraphicsState state)
        {
            var ctm = state.Ctm;
            ctm = ctm.PreConcat(Value);
            state.Ctm = ctm;

            var context = state.Scanner.RenderContext;
            if (context != null)
            {
                //var matrix = context.TotalMatrix;
                //SKMatrix.PreConcat(ref matrix, ctm);
                context.SetMatrix(state.Ctm);
            }
        }

        public SKMatrix Value => new SKMatrix
        {
            ScaleX = ((IPdfNumber)operands[0]).FloatValue, // a.
            SkewY = ((IPdfNumber)operands[1]).FloatValue, // b.
            SkewX = ((IPdfNumber)operands[2]).FloatValue, // e.                        
            ScaleY = ((IPdfNumber)operands[3]).FloatValue, // d.                       
            TransX = ((IPdfNumber)operands[4]).FloatValue, // e.
            TransY = ((IPdfNumber)operands[5]).FloatValue, // f.
            Persp2 = 1
        };
    }
}