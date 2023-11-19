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
using PdfClown.Bytes;
using System.Reflection;
using PdfClown.Documents.Contents.Fonts;
using System.Xml.Linq;

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
    public sealed class FontType0 : Font
    {

        public static FontType0 Load(Document doc, Assembly assembly, string resurceFile)
        {
            using var stream = assembly.GetManifestResourceStream(resurceFile);
            return new FontType0(doc, new TTFParser().Parse(stream), true, true, false);
        }

        public static FontType0 Load(Document doc, string fileName)
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Load(doc, stream);
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
        public static FontType0 Load(Document doc, Stream file)
        {
            return new FontType0(doc, new OTFParser().Parse(file), false, true, false);
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
        public static FontType0 Load(Document doc, IInputStream input) => Load(doc, input, false);

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
        public static FontType0 Load(Document doc, IInputStream input, bool embedSubset) => Load(doc, new TTFParser().Parse(input), embedSubset);

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
        public static FontType0 Load(Document doc, TrueTypeFont ttf, bool embedSubset)
        {
            return new FontType0(doc, ttf, embedSubset, false, false);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param file A TrueType font.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font file.
         */
        public static FontType0 LoadVertical(Document doc, Stream file)
        {
            return new FontType0(doc, new TTFParser().Parse(file), true, true, true);
        }

        /**
         * Loads a TTF to be embedded into a document as a vertical Type 0 font.
         *
         * @param doc The PDF document that will hold the embedded font.
         * @param input A TrueType font.
         * @return A Type0 font with a CIDFontType2 descendant.
         * @throws IOException If there is an error reading the font stream.
         */
        public static FontType0 LoadVertical(Document doc, IInputStream input)
        {
            return new FontType0(doc, new TTFParser().Parse(input), true, true, true);
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
        public static FontType0 LoadVertical(Document doc, IInputStream input, bool embedSubset)
        {
            return new FontType0(doc, new TTFParser().Parse(input), embedSubset, true, true);
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
        public static FontType0 LoadVertical(Document doc, TrueTypeFont ttf, bool embedSubset)
        {
            return new FontType0(doc, ttf, embedSubset, false, true);
        }

        private bool isCMapPredefined;
        private bool isDescendantCJK;
        private CMap cMap;
        private CMap cMapUCS2;
        private FontCIDType2Embedder embedder;
        private GsubData gsubData;
        private ICmapLookup cmapLookup;
        private TrueTypeFont ttf;
#if DEBUG
        private readonly HashSet<int> noUnicode = new ();
#endif

        internal FontType0(Document document, TrueTypeFont ttf, bool embedSubset, bool closeTTF, bool vertical)
            : base(document, new PdfDictionary(6))
        {
            if (vertical)
            {
                ttf.EnableVerticalSubstitutions();
            }

            gsubData = ttf.GsubData;
            cmapLookup = ttf.GetUnicodeCmapLookup();

            embedder = new FontCIDType2Embedder(document, Dictionary, ttf, embedSubset, this, vertical);
            DescendantFont = embedder.GetCIDFont();
            ReadEncoding();
            FetchCMapUCS2();
            if (closeTTF)
            {
                if (embedSubset)
                {
                    this.ttf = ttf;
                    document.RegisterTrueTypeFontForClosing(ttf);
                }
                else
                {
                    // the TTF is fully loaded and it is safe to close the underlying data source
                    ttf.Dispose();
                }
            }

        }

        internal FontType0(PdfDirectObject baseObject) : base(baseObject)
        {
            gsubData = DefaultGsubData.NO_DATA_FOUND;
            cmapLookup = null;

            var fonts = DescendantFonts;
            if (fonts == null)
            {
                throw new IOException("Missing descendant font array");
            }
            if (fonts.Count == 0)
            {
                throw new IOException("Descendant font array is empty");
            }
            var descendantFont = DescendantFont;
            if (descendantFont == null)
            {
                throw new IOException("Missing descendant font dictionary");
            }
            ReadEncoding();
            FetchCMapUCS2();
        }

        public PdfArray DescendantFonts
        {
            get => BaseDataObject.GetArray(PdfName.DescendantFonts);
            set => BaseDataObject[PdfName.DescendantFonts] = value;
        }
        /**
          <summary>Gets the CIDFont dictionary that is the descendant of this composite font.</summary>
        */
        public FontCID DescendantFont
        {
            get => FontCID.WrapFont(DescendantFonts?[0], this);
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
            get => fontDescriptor ??= DescendantFont.FontDescriptor;
            set => DescendantFont.FontDescriptor = value;
        }

        public override string Name
        {
            get => base.Name;
            set => base.Name = value;
        }

        public override SKMatrix FontMatrix
        {
            get => DescendantFont.FontMatrix;
        }

        public override bool IsVertical
        {
            get => CMap?.WMode == 1;
        }

        public override bool IsEmbedded
        {
            get => DescendantFont.IsEmbedded;
        }

        public override SKRect BoundingBox
        {
            get => DescendantFont.BoundingBox;
        }

        public override float AverageFontWidth
        {
            get => DescendantFont.AverageFontWidth;
        }

        public override float GetHeight(int code)
        {
            return DescendantFont.GetHeight(code);
        }

        public override int GetBytesCount(int code) => DescendantFont.GetBytesCount(code);

        public override void Encode(Span<byte> bytes, int unicode) => DescendantFont.Encode(bytes, unicode);

        public override bool HasExplicitWidth(int code) => DescendantFont.HasExplicitWidth(code);

        public override bool IsStandard14
        {
            get => false;
        }

        public override bool IsDamaged
        {
            get => DescendantFont.IsDamaged;
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
            var vector = DescendantFont.GetPositionVector(code);
            return vector.Scale(-1 / 1000f);
        }


        public override SKPoint GetDisplacement(int code)
        {
            if (IsVertical)
            {
                return new SKPoint(0, DescendantFont.GetVerticalDisplacementVectorY(code) / 1000f);
            }
            else
            {
                return base.GetDisplacement(code);
            }
        }

        public override float GetWidth(int code) => DescendantFont.GetWidth(code);

        protected override float GetStandard14Width(int code)
        {
            throw new NotSupportedException("not supported");
        }

        public override float GetWidthFromFont(int code) => DescendantFont.GetWidthFromFont(code);

        public override int? ToUnicode(int code)
        {
            // try to use a ToUnicode CMap
            var unicode = base.ToUnicode(code);
            if (unicode != null)
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
                return cMapUCS2.ToUnicode(cid) ?? -1;
            }

            // PDFBOX-5324: try to get unicode from font cmap
            if (DescendantFont is FontCIDType2 fontCIDType2)
            {
                var font = fontCIDType2.TrueTypeFont;
                if (font != null)
                {
                    try
                    {
                        if (font.GetUnicodeCmapLookup(false) is ICmapLookup cmap)
                        {
                            int gid = fontCIDType2.IsEmbedded
                                ? fontCIDType2.CodeToGID(code)
                                : fontCIDType2.CodeToCID(code);

                            var codes = cmap.GetCharCodes(gid);
                            if (codes != null && codes.Count > 0)
                            {
                                return codes[0];
                            }
                        }
                    }
                    catch (IOException e)
                    {
                        Debug.WriteLine("warn: get unicode from font cmap fail" + e);
                    }
                }
            }
#if DEBUG
            if (!noUnicode.Contains(code))
            {
                // if no value has been produced, there is no way to obtain Unicode for the character.
                String cid = "CID+" + CodeToCID(code);
                Debug.WriteLine($"warn: No Unicode mapping for {cid} ({code}) in font {Name}");
                // we keep track of which warnings have been issued, so we don't log multiple times
                noUnicode.Add(code);
            }
#endif
            return null;
        }

        public override int ReadCode(IInputStream input, out ReadOnlySpan<byte> bytes)
            => CMap?.ReadCode(input, out bytes)
            ?? throw new IOException("required cmap is null");

        public override int ReadCode(ReadOnlySpan<byte> bytes)
            => CMap?.ReadCode(bytes, out _)
            ?? throw new IOException("required cmap is null");
        /**
         * Returns the CID for the given character code. If not found then CID 0 is returned.
         *
         * @param code character code
         * @return CID
         */
        public int CodeToCID(int code) => DescendantFont.CodeToCID(code);

        /**
         * Returns the GID for the given character code.
         *
         * @param code character code
         * @return GID
         */
        public int CodeToGID(int code) => DescendantFont.CodeToGID(code);

        public override SKPath GetPath(int code) => DescendantFont.GetPath(code);

        public override SKPath GetNormalizedPath(int code) => DescendantFont.GetNormalizedPath(code);

        public override bool HasGlyph(int code) => DescendantFont.HasGlyph(code);

        public void EncodeGlyphId(Span<byte> bytes, int glyphId) => DescendantFont.EncodeGlyphId(bytes, glyphId);

        private void ReadEncoding()
        {
            var encoding = Dictionary.Resolve(PdfName.Encoding);
            if (encoding is PdfName encodingName)
            {
                // predefined CMap
                cMap = CMap.Get(encodingName);
                isCMapPredefined = true;
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
            }
            // check if the descendant font is CJK
            var ros = DescendantFont.CIDSystemInfo;
            if (ros != null)
            {
                var ordering = ros.Ordering;
                isDescendantCJK = "Adobe".Equals(ros.Registry) &&
                        ("GB1".Equals(ordering) ||
                         "CNS1".Equals(ordering) ||
                         "Japan1".Equals(ordering) ||
                         "Korea1".Equals(ordering));
            }
        }

        /**
     * Fetches the corresponding UCS2 CMap if the font's CMap is predefined.
     */
        private void FetchCMapUCS2()
        {
            // if the font is composite and uses a predefined cmap (excluding Identity-H/V)
            // or whose descendant CIDFont uses the Adobe-GB1, Adobe-CNS1, Adobe-Japan1, or
            // Adobe-Korea1 character collection:
            var name = Dictionary.GetName(PdfName.Encoding);
            if (isCMapPredefined && !(name.Equals(PdfName.IdentityH) || name.Equals(PdfName.IdentityV)) ||
                isDescendantCJK)
            {
                // a) Map the character code to a CID using the font's CMap
                // b) Obtain the ROS from the font's CIDSystemInfo
                // c) Construct a second CMap name by concatenating the ROS in the format "R-O-UCS2"
                // d) Obtain the CMap with the constructed name
                // e) Map the CID according to the CMap from step d), producing a Unicode value

                // todo: not sure how to interpret the PDF spec here, do we always override? or only when Identity-H/V?
                string strName = null;
                if (isDescendantCJK)
                {
                    var cidSystemInfo = DescendantFont.CIDSystemInfo;
                    if (cidSystemInfo != null)
                    {
                        strName = $"{cidSystemInfo.Registry}-{cidSystemInfo.Ordering}-{cidSystemInfo.Supplement}";
                    }
                }
                else if (name != null)
                {
                    strName = name.StringValue;
                }

                // try to find the corresponding Unicode (UC2) CMap
                if (strName != null)
                {
                    try
                    {
                        var prdCMap = CMap.Get(strName);
                        var ucs2Name = $"{prdCMap.Registry}-{prdCMap.Ordering}-UCS2";
                        cMapUCS2 = CMap.Get(ucs2Name);
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine($"warn: Could not get {strName} UC2 map for font " + Name, ex);
                    }
                }
            }
        }

        /**
     * Returns the CMap lookup table if present.
     * 
     * @return the CMap lookup table if present
     */
        public ICmapLookup CmapLookup
        {
            get => cmapLookup;
        }
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