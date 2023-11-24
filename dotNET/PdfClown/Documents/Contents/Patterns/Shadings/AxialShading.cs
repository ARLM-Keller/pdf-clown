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

using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Functions;
using PdfClown.Objects;
using SkiaSharp;
using System;

namespace PdfClown.Documents.Contents.Patterns.Shadings
{
    public class AxialShading : Shading
    {
        private SKPoint[] coords;
        private float[] domain;
        private bool[] extend;
        private SKRect? box;

        internal AxialShading(PdfDirectObject baseObject) : base(baseObject)
        { }
        public AxialShading()
        {
            ShadingType = 2;
        }

        public override SKRect Box
        {
            get => box ??= new SKRect(Coords[0].X, Coords[0].Y, Coords[1].X, Coords[1].Y).Standardized;
        }

        public SKPoint[] Coords
        {
            get => coords ??= Dictionary[PdfName.Coords] is PdfArray array
                ? new SKPoint[]
                {
                    new SKPoint(array.GetFloat(0), array.GetFloat(1)),
                    new SKPoint(array.GetFloat(2), array.GetFloat(3))
                }
                : null;
            set
            {
                coords = value;
                Dictionary[PdfName.Domain] = new PdfArray(4)
                {
                    new PdfReal(value[0].X), new PdfReal(value[0].Y),
                    new PdfReal(value[1].X), new PdfReal(value[1].Y)
                };
            }
        }

        public float[] Domain
        {
            get => domain ??= Dictionary.Resolve(PdfName.Domain) is PdfArray array
                    ? new float[] { array.GetFloat(0), array.GetFloat(1) }
                    : new float[] { 0F, 1F };
            set
            {
                domain = value;
                Dictionary[PdfName.Domain] = new PdfArray(2) { new PdfReal(value[0]), new PdfReal(value[1]) };
            }
        }

        public Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public bool[] Extend
        {
            get => extend ??= Dictionary.Resolve(PdfName.Extend) is PdfArray array
                    ? new bool[] { array.GetBool(0), array.GetBool(1) }
                : new bool[] { false, false };
            set
            {
                extend = value;
                Dictionary[PdfName.Domain] = new PdfArray(2)
                {
                    PdfBoolean.Get(value[0]),
                    PdfBoolean.Get(value[1])
                };
            }
        }

        public override SKShader GetShader(SKMatrix sKMatrix, GraphicsState state)
        {
            var coords = Coords;
            var colorSpace = ColorSpace;
            var compCount = colorSpace.ComponentCount;
            var colors = new SKColor[2];
            //var background = Background;
            var domain = Domain;
            Span<float> components = stackalloc float[compCount];
            for (int i = 0; i < domain.Length; i++)
            {
                components[0] = domain[i];
                var result = Function.Calculate(components);
                colors[i] = colorSpace.GetSKColor(result, null);
                components.Clear();
            }
            var mode = Extend[0] && Extend[1] ? SKShaderTileMode.Clamp
                : Extend[0] && !Extend[1] ? SKShaderTileMode.Mirror
                : !Extend[0] && Extend[1] ? SKShaderTileMode.Mirror
                : SKShaderTileMode.Decal;

            return SKShader.CreateLinearGradient(coords[0], coords[1], colors, domain, mode, sKMatrix);
        }

    }
}
