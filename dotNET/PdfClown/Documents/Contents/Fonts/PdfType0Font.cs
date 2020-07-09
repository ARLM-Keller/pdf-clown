/*
  Copyright 2009-2010 Stefano Chizzolini. http://www.pdfclown.org

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

using bytes = PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util;
using PdfClown.Util.IO;

using System;
using System.IO;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;
using System.Text;
using System.Diagnostics;
using PdfClown.Documents.Contents.Fonts.TTF.Model;
using PdfClown.Documents.Contents.Fonts.TTF;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
      <summary>Composite font associated to a Type 0 CIDFont,
      containing glyph descriptions based on the Adobe Type 1 font format [PDF:1.6:5.6.3].</summary>
    */
    /*
      NOTE: Type 0 CIDFonts encompass several formats:
      * CFF;
      * OpenFont/CFF (in case "CFF" table's Top DICT has CIDFont operators).
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class PdfType0Font : Font
    {
        #region constructors

        #endregion
        #region static
        #region interface
        #region public
        public static PdfType0Font Load(Document doc, System.Reflection.Assembly assembly, string resurceFile)
        {
            using (var stream = assembly.GetManifestResourceStream(resurceFile))
            {
                return new PdfType0Font(doc, new TTFParser().Parse(stream), true, true, false);
            }
        }

        public static PdfType0Font Load(Document doc, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Load(doc, stream);
            }
        }
        /**
         * Loads a TTF to be embedded and subset into a document as a Type 0 font. If you are loading a
         * font for AcroForm, then use the 3-parameter constructor instead.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param file A TrueType font.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font file.
         */
        public static PdfType0Font Load(Document doc, Stream file)
        {
            return new PdfType0Font(doc, new OTFParser().Parse(file), false, true, false);
        }

        /**
         * Loads a TTF to be embedded and subset into a document as a Type 0 font. If you are loading a
         * font for AcroForm, then use the 3-parameter constructor instead.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param input An input stream of a TrueType font. It will be closed before returning.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font Load(Document doc, bytes.IInputStream input)
        {
            return new PdfType0Font(doc, new TTFParser().Parse(input), false, true, false);
        }

        /**
         * Loads a TTF to be embedded into a document as a Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param input An input stream of a TrueType font. It will be closed before returning.
         * @param embedSubset True if the font will be subset before embedding. Set this to false when
         * creating a font for AcroForm.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font Load(Document doc, bytes.Buffer input, bool embedSubset)
        {
            return new PdfType0Font(doc, new TTFParser().Parse(input), embedSubset, true, false);
        }

        /**
         * Loads a TTF to be embedded into a document as a Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param ttf A TrueType font.
         * @param embedSubset True if the font will be subset before embedding. Set this to false when
         * creating a font for AcroForm.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font Load(Document doc, TrueTypeFont ttf, bool embedSubset)
        {
            return new PdfType0Font(doc, ttf, embedSubset, false, false);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param file A TrueType font.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font file.
         */
        public static PdfType0Font LoadVertical(Document doc, Stream file)
        {
            return new PdfType0Font(doc, new TTFParser().Parse(file), true, true, true);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param input A TrueType font.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font LoadVertical(Document doc, bytes.Buffer input)
        {
            return new PdfType0Font(doc, new TTFParser().Parse(input), true, true, true);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param input A TrueType font.
         * @param embedSubset True if the font will be subset before embedding
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font LoadVertical(Document doc, bytes.Buffer input, bool embedSubset)
        {
            return new PdfType0Font(doc, new TTFParser().Parse(input), embedSubset, true, true);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param ttf A TrueType font.
         * @param embedSubset True if the font will be subset before embedding
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static PdfType0Font LoadVertical(Document doc, TrueTypeFont ttf, bool embedSubset)
        {
            return new PdfType0Font(doc, ttf, embedSubset, false, true);
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region fields
        private bool isCMapPredefined;
        private bool isDescendantCJK;
        private CMap cMap;
        private CMap cMapUCS2;
        private PdfCIDFontType2Embedder embedder;
        private GsubData gsubData;
        private ICmapLookup cmapLookup;
        private TrueTypeFont ttf;
        private Encoding encoding;
        #endregion

        #region constructors       

        internal PdfType0Font(Document document, TrueTypeFont ttf, bool embedSubset, bool closeTTF, bool vertical)
            : base(document, new PdfDictionary())
        {
            if (vertical)
            {
                ttf.EnableVerticalSubstitutions();
            }

            gsubData = ttf.GsubData;
            cmapLookup = ttf.GetUnicodeCmapLookup();

            embedder = new PdfCIDFontType2Embedder(document, Dictionary, ttf, embedSubset, this, vertical);
            CIDFont = embedder.GetCIDFont();
            ReadEncoding();
            if (closeTTF)
            {
                if (embedSubset)
                {
                    this.ttf = ttf;
                    //TODO document.registerTrueTypeFontForClosing(ttf); 
                }
                else
                {
                    // the TTF is fully loaded and it is safe to close the underlying data source
                    ttf.Dispose();
                }
            }
        }

        internal PdfType0Font(PdfDirectObject baseObject) : base(baseObject)
        {
            gsubData = DefaultGsubData.NO_DATA_FOUND;
            cmapLookup = null;

            var fonts = Dictionary.Resolve(PdfName.DescendantFonts);
            if (!(fonts is PdfArray))
            {
                throw new IOException("Missing descendant font array");
            }
            var descendantFonts = (PdfArray)fonts;
            if (descendantFonts.Count == 0)
            {
                throw new IOException("Descendant font array is empty");
            }
            var descendantFontDictBase = descendantFonts.Resolve(0);
            if (!(descendantFontDictBase is PdfDictionary))
            {
                throw new IOException("Missing descendant font dictionary");
            }
            ReadEncoding();
        }
        #endregion

        #region interface
        #region protected

        public Encoding Encoding
        {
            get => encoding;
        }

        public PdfArray DescendantFonts
        {
            get => (PdfArray)BaseDataObject.Resolve(PdfName.DescendantFonts);
            set => BaseDataObject[PdfName.DescendantFonts] = value;
        }
        /**
          <summary>Gets the CIDFont dictionary that is the descendant of this composite font.</summary>
        */
        public CIDFont CIDFont
        {
            get => CIDFont.WrapFont(DescendantFonts[0], this);
            set
            {
                if (DescendantFonts == null)
                {
                    DescendantFonts = new PdfArray();
                }
                DescendantFonts[0] = value?.BaseObject;
            }
        }

        public override FontDescriptor FontDescriptor
        {
            get => fontDescriptor ?? (fontDescriptor = CIDFont.FontDescriptor);
            set => CIDFont.FontDescriptor = value;
        }

        public override SKMatrix FontMatrix
        {
            get => CIDFont.FontMatrix;
        }

        public override bool IsVertical
        {
            get => cMap.WMode == 1;
        }

        public override bool IsEmbedded
        {
            get => CIDFont.IsEmbedded;
        }

        public override SKRect BoundingBox
        {
            // Will be cached by underlying font
            get => CIDFont.BoundingBox;
        }
        public override float AverageFontWidth
        {
            get => CIDFont.AverageFontWidth;
        }

        public override float GetHeight(int code)
        {
            return CIDFont.GetHeight(code);
        }

        public override byte[] Encode(int unicode)
        {
            return CIDFont.Encode(unicode);
        }

        public override bool HasExplicitWidth(int code)
        {
            return CIDFont.HasExplicitWidth(code);
        }

        public override bool IsStandard14
        {
            get => false;
        }

        public override bool IsDamaged
        {
            get => CIDFont.IsDamaged;
        }

        public CMap CMapUCS2 => cMapUCS2;

        public CMap CMap => cMap;

        public GsubData GsubData
        {
            get => gsubData;
        }

        public override void AddToSubset(int codePoint)
        {
            if (!WillBeSubset)
            {
                throw new InvalidOperationException("This font was created with subsetting disabled");
            }
            embedder.AddToSubset(codePoint);
        }

        public void AddGlyphsToSubset(ISet<int> glyphIds)
        {
            if (!WillBeSubset)
            {
                throw new InvalidOperationException("This font was created with subsetting disabled");
            }
            embedder.AddGlyphIds(glyphIds);
        }


        public override void Subset()
        {
            if (!WillBeSubset)
            {
                throw new InvalidOperationException("This font was created with subsetting disabled");
            }
            embedder.Subset();
            if (ttf != null)
            {
                ttf.Dispose();
                ttf = null;
            }
        }

        public override bool WillBeSubset
        {
            get => embedder?.NeedsSubset ?? false;
        }

        public override SKPoint GetPositionVector(int code)
        {
            // units are always 1/1000 text space, font matrix is not used, see FOP-2252
            var vector = CIDFont.GetPositionVector(code);
            return vector.Scale(-1 / 1000f);
        }


        public override SKPoint GetDisplacement(int code)
        {
            if (IsVertical)
            {
                return new SKPoint(0, CIDFont.GetVerticalDisplacementVectorY(code) / 1000f);
            }
            else
            {
                return base.GetDisplacement(code);
            }
        }

        public override float GetWidth(int code)
        {
            return CIDFont.GetWidth(code);
        }

        protected override float GetStandard14Width(int code)
        {
            throw new NotSupportedException("not supported");
        }

        public override float GetWidthFromFont(int code)
        {
            return CIDFont.GetWidthFromFont(code);
        }

        public override int ToUnicode(int code)
        {
            // try to use a ToUnicode CMap
            var unicode = base.ToUnicode(code);
            if (unicode > -1)
            {
                return unicode;
            }

            if ((isCMapPredefined || isDescendantCJK) && cMapUCS2 != null)
            {
                // if the font is composite and uses a predefined cmap (excluding Identity-H/V) then
                // or if its descendant font uses Adobe-GB1/CNS1/Japan1/Korea1

                // a) Dictionary the character code to a character identifier (CID) according to the font?s CMap
                int cid = CodeToCID(code);

                // e) Dictionary the CID according to the CMap from step d), producing a Unicode value
                return cMapUCS2.ToUnicode(cid);
            }
            else
            {
                return -1;
            }
        }

        public override int ReadCode(Bytes.IInputStream input, out byte[] bytes)
        {
            return CMap.ReadCode(input, out bytes);
        }

        /**
         * Returns the CID for the given character code. If not found then CID 0 is returned.
         *
         * @param code character code
         * @return CID
         */
        public int CodeToCID(int code)
        {
            return CIDFont.CodeToCID(code);
        }

        /**
         * Returns the GID for the given character code.
         *
         * @param code character code
         * @return GID
         */
        public int CodeToGID(int code)
        {
            return CIDFont.CodeToGID(code);
        }

        public override SKPath GetPath(int code)
        {
            return CIDFont.GetPath(code);
        }

        public override SKPath GetNormalizedPath(int code)
        {
            return CIDFont.GetNormalizedPath(code);
        }

        public override bool HasGlyph(int code)
        {
            return CIDFont.HasGlyph(code);
        }

        public byte[] EncodeGlyphId(int glyphId)
        {
            return CIDFont.EncodeGlyphId(glyphId);
        }

        private void ReadEncoding()
        {
            var encoding = Dictionary.Resolve(PdfName.Encoding);
            if (encoding is PdfName encodingName)
            {
                // predefined CMap
                cMap = CMap.Get(encodingName);
                if (cMap != null)
                {
                    isCMapPredefined = true;
                }
                else
                {
                    throw new Exception("Missing required CMap");
                }
                this.encoding = Encoding.Get(encodingName);
            }
            else if (encoding != null)
            {
                cMap = CMap.Get(encoding);
                if (cMap == null)
                {
                    throw new IOException("Missing required CMap");
                }
                else if (!cMap.HasCIDMappings)
                {
                    Debug.WriteLine("warning Invalid Encoding CMap in font " + Name);
                }
                if (encoding is PdfDictionary dictionary)
                {
                    this.encoding = new DictionaryEncoding(dictionary);
                }
            }
            if (this.encoding == null)
            {
                if (CIDFont?.GenericFont is IEncodedFont encodedFont)
                {
                    this.encoding = Type1Encoding.FromFontBox(encodedFont.Encoding);
                }
                else
                {
                    // default (only happens with TTFs)
                    this.encoding = StandardEncoding.Instance;
                }
            }
            // check if the descendant font is CJK
            var ros = CIDFont.CIDSystemInfo;
            if (ros != null)
            {
                isDescendantCJK = "Adobe".Equals(ros.Registry, StringComparison.OrdinalIgnoreCase) &&
                        ("GB1".Equals(ros.Ordering, StringComparison.OrdinalIgnoreCase) ||
                         "CNS1".Equals(ros.Ordering, StringComparison.OrdinalIgnoreCase) ||
                         "Japan1".Equals(ros.Ordering, StringComparison.OrdinalIgnoreCase) ||
                         "Korea1".Equals(ros.Ordering, StringComparison.OrdinalIgnoreCase));
            }

            var name = encoding as PdfName;
            // if the font is composite and uses a predefined cmap (excluding Identity-H/V)
            // or whose descendant CIDFont uses the Adobe-GB1, Adobe-CNS1, Adobe-Japan1, or
            // Adobe-Korea1 character collection:
            if (isCMapPredefined
                && !(PdfName.IdentityH.Equals(name) || PdfName.IdentityV.Equals(name))
                || isDescendantCJK)
            {
                // a) Dictionary the character code to a CID using the font's CMap
                // b) Obtain the ROS from the font's CIDSystemInfo
                // c) Construct a second CMap name by concatenating the ROS in the format "R-O-UCS2"
                // d) Obtain the CMap with the constructed name
                // e) Dictionary the CID according to the CMap from step d), producing a Unicode value

                // todo: not sure how to interpret the PDF spec here, do we always override? or only when Identity-H/V?
                string strName = null;
                if (isDescendantCJK)
                {
                    strName = $"{ros.Registry}-{ros.Ordering}-{ros.Supplement}";
                }
                else if (name != null)
                {
                    strName = name.ToString();
                }

                // try to find the corresponding Unicode (UC2) CMap
                if (strName != null)
                {
                    CMap prdCMap = CMap.Get(strName);
                    string ucs2Name = prdCMap.Registry + "-" + prdCMap.Ordering + "-UCS2";
                    cMapUCS2 = CMap.Get(ucs2Name);
                }
            }
        }


        #endregion

        #region private



        #endregion
        #endregion
        #endregion
    }

    public static class SKPointExtendion
    {
        public static SKPoint Scale(this SKPoint point, float scale)
        {
            return point.Scale(scale, scale);
        }

        public static SKPoint Scale(this SKPoint point, float scaleX, float scaleY)
        {
            return new SKPoint(point.X * scaleX, point.Y * scaleY);
        }
    }
}