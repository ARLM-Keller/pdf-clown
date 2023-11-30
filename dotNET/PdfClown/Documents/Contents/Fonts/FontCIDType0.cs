/*
 * https://github.com/apache/pdfbox
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using PdfClown.Bytes;
using PdfClown.Documents.Contents.Fonts.CCF;
using PdfClown.Documents.Contents.Fonts.Type1;
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
     * Type 0 CIDFont (CFF).
     * 
     * @author Ben Litchfield
     * @author John Hewson
     */
    public class FontCIDType0 : FontCID
    {
        private readonly CFFCIDFont cidFont;  // Top DICT that uses CIDFont operators
        private readonly BaseFont t1Font; // Top DICT that does not use CIDFont operators

        private readonly Dictionary<int, float> glyphHeights = new Dictionary<int, float>();
        private readonly bool isEmbedded;
        private readonly bool isDamaged;
        private readonly SKMatrix fontMatrixTransform;
        private float? avgWidth = null;
        private SKMatrix? fontMatrix;
        private SKRect? fontBBox;
        private int[] cid2gid = null;

        public FontCIDType0(Document document, PdfDictionary fontObject) : base(document, fontObject)
        {
        }

        internal FontCIDType0(PdfDirectObject fontObject)
            : this((PdfDictionary)fontObject, null)
        {
        }
        /**
         * Constructor.
         * 
         * @param fontDictionary The font dictionary according to the PDF specification.
         * @param parent The parent font.
         */
        public FontCIDType0(PdfDirectObject fontDictionary, FontType0 parent)
            : base(fontDictionary, parent)
        {
            FontDescriptor fd = FontDescriptor;
            bool fontIsDamaged = false;
            CFFFont cffFont = null;
            if (fd != null)
            {
                var fontFile = fd.FontFile3;
                if (fontFile != null)
                {
                    using var input = fontFile.BaseDataObject.ExtractBody(true);
                    //try
                    //{
                    if (input != null && input.Length > 0 && (input.PeekByte() & 0xff) == '%')
                    {
                        // PDFBOX-2642 contains a corrupt PFB font instead of a CFF
                        Debug.WriteLine("warn: Found PFB but expected embedded CFF font " + fd.FontName);
                        fontIsDamaged = true;
                    }
                    else
                    {
                        var cffParser = new CFFParser();
                        cffFont = cffParser.Parse(input).FirstOrDefault();
                    }
                    //}
                    //catch (IOException e)
                    //{
                    //    Debug.WriteLine("error: Can't read the embedded CFF font " + fd.FontName, e);
                    //    fontIsDamaged = true;
                    //}
                }
            }

            if (cffFont != null)
            {
                // embedded
                if (cffFont is CFFCIDFont cFFCIDFont)
                {
                    cidFont = cFFCIDFont;
                    t1Font = null;
                }
                else
                {
                    cidFont = null;
                    t1Font = cffFont;
                }
                cid2gid = ReadCIDToGIDMap();
                isEmbedded = true;
                isDamaged = false;
            }
            else
            {
                // find font or substitute
                CIDFontMapping mapping = FontMappers.Instance.GetCIDFont(BaseFont, FontDescriptor, CIDSystemInfo);
                BaseFont font;
                if (mapping.IsCIDFont)
                {
                    cffFont = mapping.Font.CFF.Font;
                    if (cffFont is CFFCIDFont cFFCIDFont)
                    {
                        font = cidFont = cFFCIDFont;
                        t1Font = null;
                    }
                    else
                    {
                        // PDFBOX-3515: OpenType fonts are loaded as CFFType1Font
                        CFFType1Font f = (CFFType1Font)cffFont;
                        cidFont = null;
                        font = t1Font = f;
                    }
                }
                else
                {
                    cidFont = null;
                    font = t1Font = mapping.TrueTypeFont;
                }

                if (mapping.IsFallback)
                {
                    Debug.WriteLine($"warning: Using fallback {font.Name} for CID-keyed font {BaseFont}");
                }
                isEmbedded = false;
                isDamaged = fontIsDamaged;
            }
            fontMatrixTransform = FontMatrix;
            fontMatrixTransform = fontMatrixTransform.PostConcat(SKMatrix.CreateScale(1000, 1000));
        }

        public override BaseFont GenericFont => cidFont ?? t1Font;

        public override SKMatrix FontMatrix
        {
            get
            {
                if (fontMatrix == null)
                {
                    List<float> numbers;
                    if (cidFont != null)
                    {
                        numbers = cidFont.FontMatrix;
                    }
                    else
                    {
                        try
                        {
                            numbers = t1Font.FontMatrix;
                        }
                        catch (IOException e)
                        {
                            Debug.WriteLine("debug:Couldn't get font matrix - returning default value", e);
                            return (fontMatrix = new SKMatrix(0.001f, 0, 0, 0, 0.001f, 0, 0, 0, 1)).Value;
                        }
                    }

                    if (numbers != null && numbers.Count == 6)
                    {
                        fontMatrix = new SKMatrix(numbers[0], numbers[1], numbers[4],
                                                numbers[2], numbers[3], numbers[5],
                                                0, 0, 1);
                    }
                    else
                    {
                        fontMatrix = new SKMatrix(0.001f, 0, 0, 0, 0.001f, 0, 0, 0, 1f);
                    }
                }
                return (SKMatrix)fontMatrix;
            }
        }

        public override SKRect BoundingBox
        {
            get => fontBBox ?? (fontBBox = GenerateBoundingBox()).Value;
        }

        private SKRect GenerateBoundingBox()
        {
            if (FontDescriptor != null)
            {
                var bbox = FontDescriptor.FontBBox;
                if (bbox != null && (
                    bbox.Left.CompareTo(0) != 0 ||
                    bbox.Bottom.CompareTo(0) != 0 ||
                    bbox.Right.CompareTo(0) != 0 ||
                    bbox.Top.CompareTo(0) != 0))
                {
                    return bbox.ToRect();
                }
            }
            try
            {
                return cidFont?.FontBBox ?? t1Font?.FontBBox ?? SKRect.Empty;
            }
            catch (IOException e)
            {
                Debug.WriteLine("debug: Couldn't get font bounding box - returning default value " + e);
                return SKRect.Empty;
            }
        }

        /**
         * Returns the embedded CFF CIDFont, or null if the substitute is not a CFF font.
         */
        public CFFFont CFFFont
        {
            get
            {
                if (cidFont != null)
                {
                    return cidFont;
                }
                else if (t1Font is CFFType1Font)
                {
                    return (CFFType1Font)t1Font;
                }
                else
                {
                    return null;
                }
            }
        }

        /**
         * Returns the embedded or substituted font.
         */
        public BaseFont Holder
        {
            get => cidFont ?? t1Font;
        }

        public override bool IsEmbedded
        {
            get => isEmbedded;
        }

        public override bool IsDamaged
        {
            get => isDamaged;
        }

        public override float AverageFontWidth
        {
            get
            {
                if (avgWidth == null)
                {
                    avgWidth = GetAverageCharacterWidth();
                }
                return (float)avgWidth;
            }
        }

        /**
         * Returns the Type 2 charstring for the given CID, or null if the substituted font does not
         * contain Type 2 charstrings.
         *
         * @param cid CID
         * @throws IOException if the charstring could not be read
         */
        public Type2CharString GetType2CharString(int cid)
        {
            if (cidFont != null)
            {
                return cidFont.GetType2CharString(cid);
            }
            else if (t1Font is CFFType1Font cffFont)
            {
                return cffFont.GetType2CharString(cid);
            }
            else
            {
                return null;
            }
        }

        /**
         * Returns the name of the glyph with the given character code. This is done by looking up the
         * code in the parent font's ToUnicode map and generating a glyph name from that.
         */
        private string GetGlyphName(int code)
        {
            var unicodes = parent.ToUnicode(code);
            if (unicodes == null)
            {
                return ".notdef";
            }
            return UniUtil.GetUniNameOfCodePoint((int)unicodes);
        }

        public override SKPath GetPath(int code)
        {
            int cid = CodeToCID(code);
            if (cid2gid != null && isEmbedded)
            {
                // PDFBOX-4093: despite being a type 0 font, there is a CIDToGIDMap
                cid = cid2gid[cid];
            }
            var charstring = GetType2CharString(cid);
            if (charstring != null)
            {
                return charstring.Path;
            }
            else if (isEmbedded && t1Font is CFFType1Font fFType1Font)
            {
                return fFType1Font.GetType2CharString(cid).Path;
            }
            else
            {
                return t1Font.GetPath(GetGlyphName(code));
            }
        }

        public override SKPath GetNormalizedPath(int code)
        {
            if (!cacheGlyphs.TryGetValue(code, out SKPath path))
            {
                cacheGlyphs[code] = path = GetPath(code);
            }
            return path;
        }

        public override bool HasGlyph(int code)
        {
            int cid = CodeToCID(code);
            Type2CharString charstring = GetType2CharString(cid);
            if (charstring != null)
            {
                return charstring.GID != 0;
            }
            else if (isEmbedded && t1Font is CFFType1Font fFType1Font)
            {
                return fFType1Font.GetType2CharString(cid).GID != 0;
            }
            else
            {
                return t1Font.HasGlyph(GetGlyphName(code));
            }
        }

        /**
         * Returns the CID for the given character code. If not found then CID 0 is returned.
         *
         * @param code character code
         * @return CID
         */
        override public int CodeToCID(int code)
        {
            return parent.CMap.ToCID(code) ?? -1;
        }

        override public int CodeToGID(int code)
        {
            int cid = CodeToCID(code);
            if (cidFont != null)
            {
                // The CIDs shall be used to determine the GID value for the glyph procedure using the
                // charset table in the CFF program
                return cidFont.Charset.GetGIDForCID(cid);
            }
            else
            {
                // The CIDs shall be used directly as GID values
                return cid;
            }
        }

        public override int ReadCode(IInputStream input, out ReadOnlySpan<byte> bytes)
        {
            return parent.ReadCode(input, out bytes);
        }

        public override int ReadCode(ReadOnlySpan<byte> bytes)
        {
            return parent.ReadCode(bytes);
        }

        public override int GetBytesCount(int code) => 1;

        public override void Encode(Span<byte> bytes, int unicode)
        {
            // todo: we can use a known character collection CMap for a CIDFont
            //       and an Encoding for Type 1-equivalent
            throw new NotSupportedException();
        }

        public override void EncodeGlyphId(Span<byte> bytes, int glyphId)
        {
            throw new NotSupportedException();
        }

        public override float GetWidthFromFont(int code)
        {
            int cid = CodeToCID(code);
            float width;
            if (cidFont != null)
            {
                width = GetType2CharString(cid).Width;
            }
            else if (isEmbedded && t1Font is CFFType1Font fFType1Font)
            {
                width = fFType1Font.GetType2CharString(cid).Width;
            }
            else
            {
                width = t1Font.GetWidth(GetGlyphName(code));
            }

            SKPoint p = new SKPoint(width, 0);
            p = fontMatrixTransform.MapPoint(p);
            return p.X;
        }

        public override float GetHeight(int code)
        {
            int cid = CodeToCID(code);

            if (!glyphHeights.TryGetValue(cid, out float height))
            {
                height = (float)GetType2CharString(cid).Bounds.Height;
                glyphHeights[cid] = height;
            }
            return height;
        }

        // todo: this is a replacement for FontMetrics method
        private float GetAverageCharacterWidth()
        {
            // todo: not implemented, highly suspect
            return 500;
        }
    }
}
