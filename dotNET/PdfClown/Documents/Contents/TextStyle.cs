/*
  Copyright 2010-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Objects;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Text style.</summary>
    */
    public sealed class TextStyle
    {
        private readonly Color fillColor;
        private readonly ColorSpace fillColorSpace;
        private readonly Font font;
        private readonly float fontSize;
        private readonly TextRenderModeEnum renderMode;
        private readonly float scaleX;
        private readonly float scaleY;
        private readonly Color strokeColor;
        private readonly ColorSpace strokeColorSpace;

        public TextStyle(
          Font font,
          float fontSize,
          TextRenderModeEnum renderMode,
          Color strokeColor,
          ColorSpace strokeColorSpace,
          Color fillColor,
          ColorSpace fillColorSpace,
          float scaleX,
          float scaleY)
        {
            this.font = font;
            this.fontSize = fontSize;
            this.renderMode = renderMode;
            this.strokeColor = strokeColor;
            this.strokeColorSpace = strokeColorSpace;
            this.fillColor = fillColor;
            this.fillColorSpace = fillColorSpace;
            this.scaleX = scaleX;
            this.scaleY = scaleY;
        }

        public Color FillColor => fillColor;

        public ColorSpace FillColorSpace => fillColorSpace;

        public Font Font => font;

        public float FontSize => fontSize;

        /**
          <exception cref="EncodeException"/>
        */
        public float GetWidth(char textChar) => font.GetWidth(textChar, fontSize) * scaleX / scaleY;

        public TextRenderModeEnum RenderMode => renderMode;

        public float ScaleX => scaleX;

        public float ScaleY => scaleY;

        public Color StrokeColor => strokeColor;

        public ColorSpace StrokeColorSpace => strokeColorSpace;
    }
}