﻿/*
  Copyright 2009-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Contents;
using System.Collections.Generic;
using System.Text;
using PdfClown.Util.Math.Geom;
using PdfClown.Documents.Contents.Scanner;
using SkiaSharp;

namespace PdfClown.Tools
{
    /**
         <summary>Text string.</summary>
         <remarks>This is typically used to assemble contiguous raw text strings
         laying on the same line.</remarks>
       */
    public class TextString : ITextString
    {
        public static TextString Transform(ITextString rawTextString, SKMatrix transform, IContentContext contentContext)
        {
            var textString = new TextString()
            {
                Context = contentContext,
                Style = rawTextString.Style
            };
            foreach (var textChar in rawTextString.TextChars)
            {
                var quad = textChar.Quad;
                quad.Transform(ref transform);
                textString.TextChars.Add(new TextChar(textChar.Value, quad, textString, true));
            }
            return textString;
        }

        private List<TextChar> textChars = new();
        private Quad? quad;
        private string text;

        public Quad Quad
        {
            get
            {
                if (quad == null)
                {
                    foreach (TextChar textChar in textChars)
                    {
                        if (!quad.HasValue)
                        { quad = textChar.Quad; }
                        else
                        { quad = Quad.Union(quad.Value, textChar.Quad); }
                    }
                }
                return quad.Value;
            }
        }

        public string Text
        {
            get
            {
                if (text == null)
                {
                    var textBuilder = new StringBuilder();
                    foreach (var textChar in textChars)
                    {
                        textBuilder.Append(textChar.Value);
                    }
                    text = textBuilder.ToString();
                }
                return text;
            }
        }

        public List<TextChar> TextChars => textChars;

        public IContentContext Context { get; set; }

        public TextStyle Style { get; set; }

        public override string ToString()
        { return Text; }

        public void Invalidate()
        {
            text = null;
            quad = null;
        }
    }
}
