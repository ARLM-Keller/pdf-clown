/*
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

using PdfClown.Bytes;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Move to the next line and show a text string' operation [PDF:1.6:5.3.2].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class ShowTextToNextLine : ShowText
    {
        /**
          <summary>Specifies no text state parameter
          (just uses the current settings).</summary>
        */
        public static readonly string SimpleOperatorKeyword = "'";
        /**
          <summary>Specifies the word spacing and the character spacing
          (setting the corresponding parameters in the text state).</summary>
        */
        public static readonly string SpaceOperatorKeyword = "''";

        /**
          <param name="text">Text encoded using current font's encoding.</param>
        */
        public ShowTextToNextLine(byte[] text)
            : base(SimpleOperatorKeyword, new PdfByteString(text))
        { }

        /**
          <param name="text">Text encoded using current font's encoding.</param>
          <param name="wordSpace">Word spacing.</param>
          <param name="charSpace">Character spacing.</param>
        */
        public ShowTextToNextLine(byte[] text, double wordSpace, double charSpace)
            : base(SpaceOperatorKeyword, PdfReal.Get(wordSpace), PdfReal.Get(charSpace), new PdfByteString(text))
        { }

        public ShowTextToNextLine(string @operator, IList<PdfDirectObject> operands)
            : base(@operator, operands)
        { }

        /**
          <summary>Gets/Sets the character spacing.</summary>
        */
        public float? CharSpace
        {
            get
            {
                if (@operator.Equals(SimpleOperatorKeyword, StringComparison.Ordinal))
                    return null;
                else
                    return ((IPdfNumber)operands[1]).FloatValue;
            }
            set
            {
                EnsureSpaceOperation();
                operands[1] = PdfReal.Get(value.Value);
            }
        }

        private PdfString String
        {
            get => (PdfString)operands[@operator.Equals(SimpleOperatorKeyword, StringComparison.Ordinal) ? 0 : 2];
            set => operands[@operator.Equals(SimpleOperatorKeyword, StringComparison.Ordinal) ? 0 : 2] = value;
        }

        public override Memory<byte> Text
        {
            get => String.RawValue;
            set => String = new PdfByteString(value);
        }

        public override IEnumerable<PdfDirectObject> Value
        {
            get => Enumerable.Repeat(String, 1);
            set => String = value.FirstOrDefault() as PdfString;
        }

        /**
          <summary>Gets/Sets the word spacing.</summary>
        */
        public float? WordSpace
        {
            get
            {
                if (@operator.Equals(SimpleOperatorKeyword, StringComparison.Ordinal))
                    return null;
                else
                    return ((IPdfNumber)operands[0]).FloatValue;
            }
            set
            {
                EnsureSpaceOperation();
                operands[0] = PdfReal.Get(value.Value);
            }
        }

        private void EnsureSpaceOperation()
        {
            if (@operator.Equals(SimpleOperatorKeyword, StringComparison.Ordinal))
            {
                @operator = SpaceOperatorKeyword;
                operands.Insert(0, PdfReal.Get(0));
                operands.Insert(1, PdfReal.Get(0));
            }
        }
    }
}