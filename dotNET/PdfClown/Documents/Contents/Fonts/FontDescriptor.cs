/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)
    * Manuel Guilbault (code contributor [FIX:27], manuel.guilbault at gmail.com)

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


namespace PdfClown.Documents.Contents.Fonts
{
    public class FontDescriptor : PdfObjectWrapper<PdfDictionary>
    {
        public FontDescriptor(PdfDirectObject baseObject) : base(baseObject)
        { }

        public FontDescriptor() : this(null, new PdfDictionary())
        { }

        public FontDescriptor(Document document, PdfDictionary baseObject) : base(document, baseObject)
        { }

        public string FontName
        {
            get => Dictionary.GetString(PdfName.FontName);
            set => Dictionary.SetName(PdfName.FontName, value);
        }

        public string FontFamily
        {
            get => Dictionary.GetString(PdfName.FontFamily);
            set => Dictionary.SetString(PdfName.FontFamily, value);
        }

        public float? FontStretch
        {
            get => Dictionary.GetFloat(PdfName.FontStretch);
            set => Dictionary.SetFloat(PdfName.FontStretch, value);
        }

        public float? FontWeight
        {
            get => Dictionary.GetFloat(PdfName.FontWeight);
            set => Dictionary.SetFloat(PdfName.FontWeight, value);
        }

        public FlagsEnum Flags
        {
            get => (FlagsEnum)Dictionary.GetInt(PdfName.Flags);
            set => Dictionary.SetInt(PdfName.Flags, (int)value);
        }

        public bool HasFlags => Dictionary.ContainsKey(PdfName.Flags);

        public bool NonSymbolic
        {
            get => (Flags & FlagsEnum.Nonsymbolic) == FlagsEnum.Nonsymbolic;
            set
            {
                if (value)
                    Flags |= FlagsEnum.Nonsymbolic;
                else
                    Flags &= ~FlagsEnum.Nonsymbolic;
            }
        }
        public bool Symbolic
        {
            get => (Flags & FlagsEnum.Symbolic) == FlagsEnum.Symbolic;
            set
            {
                if (value)
                    Flags |= FlagsEnum.Symbolic;
                else
                    Flags &= ~FlagsEnum.Symbolic;
            }
        }

        public Rectangle FontBBox
        {
            get => Wrap<Rectangle>((PdfArray)Dictionary.Resolve(PdfName.FontBBox));
            set => Dictionary[PdfName.FontBBox] = value?.BaseObject;
        }

        public float ItalicAngle
        {
            get => Dictionary.GetFloat(PdfName.ItalicAngle, 0F);
            set => Dictionary[PdfName.ItalicAngle] = new PdfReal(value);
        }

        public float Ascent
        {
            get => Dictionary.GetFloat(PdfName.Ascent, 750F);
            set => Dictionary[PdfName.Ascent] = new PdfReal(value);
        }

        public float Descent
        {
            get => Dictionary.GetFloat(PdfName.Descent, -250F);
            set => Dictionary.SetFloat(PdfName.Descent, value);
        }

        public float? Leading
        {
            get => Dictionary.GetNFloat(PdfName.Leading);
            set => Dictionary.SetFloat(PdfName.Leading, value);
        }

        public float? CapHeight
        {
            get => Dictionary.GetNFloat(PdfName.CapHeight);
            set => Dictionary.SetFloat(PdfName.CapHeight, value);
        }

        public float? XHeight
        {
            get => Dictionary.GetNFloat(PdfName.XHeight);
            set => Dictionary.SetFloat(PdfName.XHeight, value);
        }

        public float StemV
        {
            get => Dictionary.GetFloat(PdfName.StemV, 0F);
            set => Dictionary.SetFloat(PdfName.StemV, value);
        }

        public float StemH
        {
            get => Dictionary.GetFloat(PdfName.StemH, 0F);
            set => Dictionary.SetFloat(PdfName.StemH, value);
        }

        public float? AvgWidth
        {
            get => Dictionary.GetNFloat(PdfName.AvgWidth);
            set => Dictionary.SetFloat(PdfName.AvgWidth, value);
        }

        public float? MaxWidth
        {
            get => Dictionary.GetNFloat(PdfName.MaxWidth);
            set => Dictionary.SetFloat(PdfName.MaxWidth, value);
        }

        public float? MissingWidth
        {
            get => Dictionary.GetNFloat(PdfName.MissingWidth);
            set => Dictionary.SetFloat(PdfName.MissingWidth, value);
        }

        public FontFile FontFile
        {
            get => Wrap<FontFile>((PdfDirectObject)Dictionary[PdfName.FontFile]);
            set => Dictionary[PdfName.FontFile] = value?.BaseObject;
        }

        public FontFile FontFile2
        {
            get => Wrap<FontFile>((PdfDirectObject)Dictionary[PdfName.FontFile2]);
            set => Dictionary[PdfName.FontFile2] = value?.BaseObject;
        }

        public FontFile FontFile3
        {
            get => Wrap<FontFile>((PdfDirectObject)Dictionary[PdfName.FontFile3]);
            set => Dictionary[PdfName.FontFile3] = value?.BaseObject;
        }

        public string CharSet
        {
            get => Dictionary.GetString(PdfName.CharSet);
            set => Dictionary.SetString(PdfName.CharSet, value);
        }

        //CID Font Specific
        public string Lang
        {
            get => Dictionary.GetString(PdfName.Lang);
            set => Dictionary.SetName(PdfName.Lang, value);
        }

        public FontStyle Style
        {
            get => Wrap<FontStyle>((PdfDirectObject)Dictionary.Resolve(PdfName.Style));
            set => Dictionary[PdfName.Style] = value?.BaseObject;
        }

        public PdfDictionary FD
        {
            get => (PdfDictionary)Dictionary.Resolve(PdfName.FD);
            set => Dictionary[PdfName.FD] = value?.Reference;
        }

        public PdfStream CIDSet
        {
            get => (PdfStream)Dictionary.Resolve(PdfName.CIDSet);
            set => Dictionary[PdfName.CIDSet] = value?.Reference;
        }

    }
}