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
using System.Collections.Generic;
using SkiaSharp;
using System.Text;

namespace PdfClown.Documents.Contents.Scanner
{
    /**
      <summary>Text information.</summary>
    */
    public sealed class TextWrapper : GraphicsObjectWrapper<Text>
    {
        private List<TextStringWrapper> textStrings;

        internal TextWrapper(ContentScanner scanner) : base((Text)scanner.Current)
        {
            textStrings = new List<TextStringWrapper>();
            Extract(scanner.ChildLevel);
        }

        public override SKRect? Box
        {
            get
            {
                if (box == null)
                {
                    foreach (TextStringWrapper textString in textStrings)
                    {
                        if (!box.HasValue)
                        { box = textString.Box; }
                        else
                        { box = SKRect.Union(box.Value, textString.Box.Value); }
                    }
                }
                return box;
            }
        }

        public string Text
        {
            get
            {
                var textBuilder = new StringBuilder();
                foreach (TextStringWrapper textString in textStrings)
                {
                    textBuilder.Append(textString.Text);
                }
                return textBuilder.ToString();
            }
        }

        /**
          <summary>Gets the text strings.</summary>
        */
        public List<TextStringWrapper> TextStrings => textStrings;

        public override string ToString()
        { return Text; }

        private void Extract(ContentScanner level)
        {
            if (level == null)
                return;
            level.MoveStart();
            while (level.MoveNext())
            {
                ContentObject content = level.Current;
                if (content is ShowText)
                { textStrings.Add((TextStringWrapper)level.CurrentWrapper); }
                else if (content is ContainerObject)
                { Extract(level.ChildLevel); }
            }
        }
    }
}