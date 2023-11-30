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

using PdfClown.Objects;
using PdfClown.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
      <summary>Simple font [PDF:1.6:5.5].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public abstract class FontSimple : Font
    {
        protected Encoding encoding;
        protected GlyphMapping glyphList;
        private bool? isSymbolic;

        protected FontSimple(Document context) : base(context)
        { }

        public FontSimple(Document context, FontName baseFont)
            : base(context, Standard14Fonts.FontNames[baseFont])
        {
            AssignGlyphList(baseFont);
        }

        protected FontSimple(PdfDirectObject baseObject)
            : base(baseObject)
        { }

        /**
        * Returns the Encoding vector.
        */
        public Encoding Encoding
        {
            get => encoding;
        }

        /**
         * Returns the Encoding vector.
         */
        public GlyphMapping GlyphList
        {
            get => glyphList;
        }

        public override bool IsStandard14
        {
            get
            {
                // this logic is based on Acrobat's behaviour, see PDFBOX-2372
                // the Encoding entry cannot have Differences if we want "standard 14" font handling
                if (Encoding is DictionaryEncoding dictionary)
                {
                    if (dictionary.Differences.Count > 0)
                    {
                        // we also require that the differences are actually different, see PDFBOX-1900 with
                        // the file from PDFBOX-2192 on Windows
                        Encoding baseEncoding = dictionary.BaseEncoding;
                        foreach (var entry in dictionary.Differences)
                        {
                            if (!entry.Value.Equals(baseEncoding.GetName(entry.Key), StringComparison.Ordinal))
                            {
                                return false;
                            }
                        }
                    }
                }
                return base.IsStandard14;
            }
        }

        /**
         * Returns true the font is a symbolic (that is, it does not use the Adobe Standard Roman
         * character set).
         */
        public override bool Symbolic
        {
            get
            {
                if (isSymbolic == null)
                {
                    isSymbolic = FontSymbolic ?? false;
                }
                return isSymbolic.Value;
            }
        }

        /**
         * Internal implementation of isSymbolic, allowing for the fact that the result may be
         * indeterminate.
         */
        protected virtual bool? FontSymbolic
        {
            get
            {
                bool? result = SymbolicFlag;
                if (result != null)
                {
                    return result;
                }
                else if (IsStandard14)
                {
                    var mappedName = Standard14Fonts.GetMappedFontName(Name);
                    return mappedName == FontName.Symbol || mappedName == FontName.ZapfDingbats;
                }
                else
                {
                    if (encoding == null)
                    {
                        // sanity check, should never happen
                        if (!(this is FontTrueType))
                        {
                            throw new InvalidOperationException("PDFBox bug: encoding should not be null!");
                        }

                        // TTF without its non-symbolic flag set must be symbolic
                        return true;
                    }
                    else if (encoding is WinAnsiEncoding ||
                             encoding is MacRomanEncoding ||
                             encoding is StandardEncoding)
                    {
                        return false;
                    }
                    else if (encoding is DictionaryEncoding)
                    {
                        // each name in Differences array must also be in the latin character set
                        foreach (string name in ((DictionaryEncoding)encoding).Differences.Values)
                        {
                            if (".notdef".Equals(name, StringComparison.Ordinal))
                            {
                                // skip
                            }
                            else if (!(WinAnsiEncoding.Instance.Contains(name) &&
                                       MacRomanEncoding.Instance.Contains(name) &&
                                       StandardEncoding.Instance.Contains(name)))
                            {
                                return true;
                            }

                        }
                        return false;
                    }
                    else
                    {
                        // we don't know
                        return null;
                    }
                }
            }
        }

        /**
        * Returns the value of the symbolic flag,  allowing for the fact that the result may be
        * indeterminate.
*/
        protected bool? SymbolicFlag
        {
            get => FontDescriptor != null && FontDescriptor.HasFlags
                ? (FontDescriptor.Flags & FlagsEnum.Symbolic) == FlagsEnum.Symbolic
                : null;
        }

        public override int? ToUnicode(int code)
        {
            return ToUnicode(code, GlyphMapping.Default);
        }

        public override int? ToUnicode(int code, GlyphMapping customGlyphList)
        {
            // first try to use a ToUnicode CMap
            var unicode = base.ToUnicode(code);
            if (unicode != null)
            {
                return unicode;
            }
            // if the font is a "simple font" and uses MacRoman/MacExpert/WinAnsi[Encoding]
            // or has Differences with names from only Adobe Standard and/or Symbol, then:
            //
            //    a) Dictionary the character codes to names
            //    b) Look up the name in the Adobe Glyph List to obtain the Unicode value

            string name = null;
            if (encoding != null)
            {
                name = encoding.GetName(code);

                // allow the glyph list to be overridden for the purpose of extracting Unicode
                // we only do this when the font's glyph list is the AGL, to avoid breaking Zapf Dingbats
                GlyphMapping unicodeGlyphList;
                if (this.glyphList == GlyphMapping.Default)
                {
                    unicodeGlyphList = customGlyphList;
                }
                else
                {
                    unicodeGlyphList = this.glyphList;
                }

                var temp = unicodeGlyphList.ToUnicode(name);
                if (temp != null)
                {
                    return temp;
                }
            }

            return null;
        }


        public override bool IsVertical
        {
            get => false;
        }

        protected override float GetStandard14Width(int code)
        {
            if (Standard14AFM != null)
            {
                string nameInAFM = Encoding.GetName(code);
                // the Adobe AFMs don't include .notdef, but Acrobat uses 250, test with PDFBOX-2334
                if (string.Equals(".notdef", nameInAFM, StringComparison.Ordinal))
                {
                    return 250f;
                }
                if (string.Equals("nbspace", nameInAFM, StringComparison.Ordinal))
                {
                    // PDFBOX-4944: nbspace is missing in AFM files,
                    // but PDF specification tells "it is typographically the same as SPACE"
                    nameInAFM = "space";
                }
                else if (string.Equals("sfthyphen", nameInAFM, StringComparison.Ordinal))
                {
                    // PDFBOX-5115: sfthyphen is missing in AFM files,
                    // but PDF specification tells "it shall be typographically the same as hyphen"
                    nameInAFM = "hyphen";
                }
                // the Adobe AFMs don't include .notdef, but Acrobat uses 250, test with PDFBOX-2334
                if (nameInAFM == null || ".notdef".Equals(nameInAFM, StringComparison.Ordinal))
                {
                    return 250f;
                }

                return Standard14AFM.GetCharacterWidth(nameInAFM);
            }
            throw new InvalidOperationException("No AFM");
        }


        /**
         * Returns the path for the character with the given name. For some fonts, GIDs may be used
         * instead of names when calling this method.
         *
         * @return glyph path
         * @throws IOException if the path could not be read
         */
        public abstract SKPath GetPath(string name);

        /**
         * Returns true if the font contains the character with the given name.
         *
         * @throws IOException if the path could not be read
         */
        public abstract bool HasGlyph(string name);

        /**
         * Returns the embedded or system font used for rendering. This is never null.
         */
        public abstract BaseFont Font { get; }


        public override void AddToSubset(int codePoint)
        {
            throw new NotSupportedException();
        }

        public override void Subset()
        {
            // only TTF subsetting via PDType0Font is currently supported
            throw new NotSupportedException();
        }

        public override bool WillBeSubset
        {
            get => false;
        }

        public override bool HasExplicitWidth(int code)
        {
            if (Dictionary.ContainsKey(PdfName.Widths))
            {
                //Widths
                int firstChar = FirstChar ?? -1;
                if (code >= firstChar && code - firstChar < Widths.Count)
                {
                    return true;
                }
            }
            return false;
        }

        /**
        * Reads the Encoding from the Font dictionary or the embedded or substituted font file.
        * Must be called at the end of any subclass constructors.
        *
        * @throws IOException if the font file could not be read
*/
        protected virtual void ReadEncoding()
        {
            var encodingData = EncodingData;
            if (encodingData is PdfName pdfName)
            {
                if (string.Equals(Standard14Fonts.FontNames[FontName.ZapfDingbats], Name, StringComparison.Ordinal)
                    && !IsEmbedded)
                {
                    // PDFBOX- and PDF.js issue 16464: ignore other encodings
                    // this segment will work only if readEncoding() is called after the data
                    // for getName() and isEmbedded() is available
                    this.encoding = ZapfDingbatsEncoding.Instance;
                }
                else
                {
                    this.encoding = Encoding.Get(pdfName);

                    if (this.encoding == null)
                    {
                        Debug.WriteLine("warn: Unknown encoding: " + pdfName.StringValue);
                        this.encoding = ReadEncodingFromFont(); // fallback
                    }
                }
            }
            else if (encodingData is PdfDictionary encodingDict)
            {
                Encoding builtIn = null;
                bool? symbolic = SymbolicFlag;

                var baseEncoding = encodingDict.GetName(PdfName.BaseEncoding);

                bool hasValidBaseEncoding = baseEncoding != null && Encoding.Get(baseEncoding) != null;

                if (!hasValidBaseEncoding && symbolic == true)
                {
                    builtIn = ReadEncodingFromFont();
                }

                this.encoding = new DictionaryEncoding(encodingDict, !(symbolic ?? false), builtIn);
            }
            else
            {
                this.encoding = ReadEncodingFromFont();
            }

            // normalise the standard 14 name, e.g "Symbol,Italic" -> "Symbol"
            AssignGlyphList(Standard14Fonts.GetMappedFontName(Name));
        }

        /**
         * Called by readEncoding() if the encoding needs to be extracted from the font file.
         *
         * @throws IOException if the font file could not be read.
         */
        protected abstract Encoding ReadEncodingFromFont();


        private void AssignGlyphList(FontName baseFont)
        {
            // assign the glyph list based on the font
            if (baseFont == FontName.ZapfDingbats)
            {
                glyphList = GlyphMapping.ZapfDingbats;
            }
            else
            {
                glyphList = GlyphMapping.Default;
            }
        }


    }
}