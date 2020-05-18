/*
  Copyright 2007-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Contents.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Text;
using PdfClown.Util.Math.Geom;

namespace PdfClown.Documents.Contents.Scanner
{
    /**
      <summary>Text string information.</summary>
    */
    public sealed class TextStringWrapper : GraphicsObjectWrapper<ShowText>, ITextString
    {
        public class ShowTextScanner : ShowText.IScanner
        {
            TextStringWrapper wrapper;

            internal ShowTextScanner(TextStringWrapper wrapper)
            { this.wrapper = wrapper; }

            public void ScanChar(char textChar, Quad textCharQuad)
            {
                wrapper.textChars.Add(
                  new TextChar(textChar, textCharQuad, wrapper, false));
            }
        }

        private TextStyle style;
        private List<TextChar> textChars;
        private string text;
        private Quad? quad;

        internal TextStringWrapper(ContentScanner scanner, bool scan = true) : base((ShowText)scanner.Current)
        {
            Context = scanner.ContentContext;
            Context.Strings.Add(this);

            textChars = new List<TextChar>();
            {
                GraphicsState state = scanner.State;
                style = new TextStyle(
                  state.Font,
                  state.FontSize * state.TextState.Tm.ScaleY,
                  state.RenderMode,
                  state.StrokeColor,
                  state.StrokeColorSpace,
                  state.FillColor,
                  state.FillColorSpace,
                  state.Scale * state.TextState.Tm.ScaleX,
                  state.TextState.Tm.ScaleY
                  );
                if (scan)
                {
                    BaseDataObject.Scan(
                      state,
                      new ShowTextScanner(this)
                      );
                }
            }
        }

        public override SKRect? Box => Quad?.GetBounds();

        public Quad? Quad
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
                        { quad = Util.Math.Geom.Quad.Union(quad.Value, textChar.Quad); }
                    }
                }
                return quad;
            }
        }

        /**
          <summary>Gets the text style.</summary>
        */
        public TextStyle Style => style;

        public String Text
        {
            get
            {
                if (text == null)
                {
                    StringBuilder textBuilder = new StringBuilder();
                    foreach (TextChar textChar in textChars)
                    { textBuilder.Append(textChar); }
                    text = textBuilder.ToString();
                }
                return text;
            }
        }

        public List<TextChar> TextChars => textChars;

        public IContentContext Context { get; }

        public override string ToString()
        { return Text; }
    }
}