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

using PdfClown.Objects;

using SkiaSharp;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Text character.</summary>
      <remarks>It describes a text element extracted from content streams.</remarks>
    */
    public sealed class TextChar
    {
        #region dynamic
        #region fields
        private readonly SKRect box;
        private readonly TextStyle style;
        private readonly char value;
        private readonly bool virtual_;
        #endregion

        #region constructors
        public TextChar(char value, SKRect box, TextStyle style, bool virtual_)
        {
            this.value = value;
            this.box = box;
            this.style = style;
            this.virtual_ = virtual_;
        }
        #endregion

        #region interface
        #region public
        public SKRect Box => box;

        public bool Contains(char value)
        { return this.value == value; }

        public TextStyle Style => style;

        public override string ToString()
        { return Value.ToString(); }

        public char Value => value;

        public bool Virtual => virtual_;
        #endregion
        #endregion
        #endregion
    }
}