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
using SkiaSharp;

namespace PdfClown.Documents.Contents.Patterns.Shadings
{
    public class FunctionBasedShading : Shading
    {
        private float[] domain;
        private SKMatrix? matrix;

        public FunctionBasedShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public FunctionBasedShading()
        {
            ShadingType = 1;
        }

        public float[] Domain
        {
            get
            {
                return domain ??= Dictionary.Resolve(PdfName.Domain) is PdfArray array
                    ? new float[]
                    {
                    array.GetFloat(0),
                    array.GetFloat(1),
                    array.GetFloat(2),
                    array.GetFloat(3)
                    }
                    : new float[] { 0F, 1F, 0F, 1F };
            }
            set
            {
                domain = value;
                Dictionary[PdfName.Domain] = new PdfArray(4)
                {
                    new PdfReal(value[0]),
                    new PdfReal(value[1]),
                    new PdfReal(value[2]),
                    new PdfReal(value[3])
                };
            }
        }

        public SKMatrix Matrix
        {
            get => matrix ??= Dictionary.Resolve(PdfName.Matrix) is PdfArray array
                   ? new SKMatrix
                   {
                       ScaleX = array.GetFloat(0),
                       SkewY = array.GetFloat(1),
                       SkewX = array.GetFloat(2),
                       ScaleY = array.GetFloat(3),
                       TransX = array.GetFloat(4),
                       TransY = array.GetFloat(5),
                       Persp2 = 1
                   }
                   : SKMatrix.Identity;
            set
            {
                matrix = value;
                Dictionary[PdfName.Matrix] =
                 new PdfArray(6)
                 {
                    PdfReal.Get(value.ScaleX),
                    PdfReal.Get(value.SkewY),
                    PdfReal.Get(value.SkewX),
                    PdfReal.Get(value.ScaleY),
                    PdfReal.Get(value.TransX),
                    PdfReal.Get(value.TransY)
                 };
            }
        }

        public Functions.Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public override SKShader GetShader(SKMatrix sKMatrix, GraphicsState state)
        {
            return null;
        }
    }
}
