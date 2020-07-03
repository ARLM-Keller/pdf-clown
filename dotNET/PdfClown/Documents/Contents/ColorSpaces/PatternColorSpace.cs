/*
  Copyright 2010-2011 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents;
using PdfClown.Objects;
using PdfClown.Util;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>Pattern color space [PDF:1.6:4.5.5].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class PatternColorSpace : SpecialColorSpace
    {
        #region static
        #region fields
        /*
          NOTE: In case of no parameters, it may be specified directly (i.e. without being defined
          in the ColorSpace subdictionary of the contextual resource dictionary) [PDF:1.6:4.5.7].
        */
        //TODO:verify parameters!!!
        public static readonly PatternColorSpace Default = new PatternColorSpace(null);
        #endregion
        #endregion

        #region dynamic
        #region constructors
        //TODO:IMPL new element constructor!

        internal PatternColorSpace(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 0;

        public override Color DefaultColor => Pattern.Default;

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        {
            //TODO

            Pattern pattern = context.Resources.Patterns[(PdfName)components[components.Count - 1]];
            if (pattern is TilingPattern)
            {
                TilingPattern tilingPattern = (TilingPattern)pattern;
                if (tilingPattern.PaintType == TilingPattern.PaintTypeEnum.Uncolored)
                {
                    ColorSpace underlyingColorSpace = UnderlyingColorSpace;
                    if (underlyingColorSpace == null)
                        throw new ArgumentException("Uncolored tiling patterns not supported by this color space because no underlying color space has been defined.");

                    // Get the color to be used for colorizing the uncolored tiling pattern!
                    Color color = underlyingColorSpace.GetColor(components, context);
                    // Colorize the uncolored tiling pattern!
                    pattern = tilingPattern.Colorize(color);
                }
            }
            return pattern;
        }

        public override bool IsSpaceColor(Color color)
        {
            return color is Pattern;
        }

        public override SKColor GetSKColor(Color color, float? alpha = null)
        {
            // FIXME: Auto-generated method stub
            return SKColors.Black;
        }

        public override SKColor GetSKColor(float[] components, float? alpha = null)
        {
            // FIXME: Auto-generated method stub
            return SKColors.Black;
        }

        public override SKPaint GetPaint(Color color, float? alpha = null)
        {
            // FIXME: Auto-generated method stub
            if (color is TilingPattern pattern)
            {
                return new SKPaint
                {
                    Shader = pattern.GetShader(),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }
            if (color is ShadingPattern shading)
            {
                return new SKPaint
                {
                    Shader = shading.GetShader(),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
            }
            return new SKPaint
            {
                Color = GetSKColor(color),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
        }

        /**
          <summary>Gets the color space in which the actual color of the <see cref="Pattern">pattern</see> is to be specified.</summary>
          <remarks>This feature is applicable to <see cref="TilingPattern">uncolored tiling patterns</see> only.</remarks>
        */
        public ColorSpace UnderlyingColorSpace
        {
            get
            {
                PdfDirectObject baseDataObject = BaseDataObject;
                if (baseDataObject is PdfArray)
                {
                    PdfArray baseArrayObject = (PdfArray)baseDataObject;
                    if (baseArrayObject.Count > 1)
                        return ColorSpace.Wrap(baseArrayObject[1]);
                }
                return null;
            }
        }
        #endregion
        #endregion
        #endregion
    }
}