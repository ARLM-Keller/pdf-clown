/*
  Copyright 2007-2011 Stefano Chizzolini. http://www.pdfclown.org

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
using Shadings = PdfClown.Documents.Contents.Patterns.Shadings;
using PdfClown.Objects;

using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Paint the shape and color shading' operation [PDF:1.6:4.6.3].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class PaintShading : Operation, IResourceReference<Shadings::Shading>
    {
        public static readonly string OperatorKeyword = "sh";

        public PaintShading(PdfName name) : base(OperatorKeyword, name)
        { }

        public PaintShading(IList<PdfDirectObject> operands) : base(OperatorKeyword, operands)
        { }

        /**
          <summary>Gets the <see cref="colorSpaces::Shading">shading</see> resource to be painted.
          </summary>
          <param name="context">Content context.</param>
        */
        public Shadings::Shading GetShading(ContentScanner scanner) => GetResource(scanner);

        public Shadings::Shading GetResource(ContentScanner scanner)
        {
            var pscanner = scanner;
            Shadings::Shading shading;
            while ((shading = pscanner.ContentContext.Resources.Shadings[Name]) == null
                && (pscanner = pscanner.ParentLevel) != null)
            { }
            return shading;
        }

        public PdfName Name
        {
            get => (PdfName)operands[0];
            set => operands[0] = value;
        }

        public override void Scan(GraphicsState state)
        {
            var scanner = state.Scanner;

            if (scanner.RenderContext != null)
            {
                var shading = GetShading(scanner);
                var paint = state.FillColorSpace?.GetPaint(state.FillColor, state.FillAlpha);
                if (shading.Background != null)
                {
                    var color = shading.BackgroundColor;
                    paint.Color = shading.ColorSpace.GetSKColor(color);
                    scanner.RenderContext.DrawPaint(paint);
                }
                var matrix = shading.CalculateMatrix(SKMatrix.Identity, state);
                paint.Shader = shading?.GetShader(matrix, state);
                scanner.RenderContext.DrawPaint(paint);
            }
        }

        private static SKMatrix GenerateMatrix(ContentScanner scanner, Shadings.Shading shading)
        {
            var matrix = SKMatrix.Identity;
            if (scanner.RenderContext.GetLocalClipBounds(out var clipBound))
            {
                matrix = SKMatrix.CreateScale(shading.Box.Width == 0 ? 1 : clipBound.Width / shading.Box.Width,
                                                  shading.Box.Height == 0 ? 1 : clipBound.Width / shading.Box.Height);
            }

            return matrix;
        }
    }
}