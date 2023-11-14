/*
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
using PdfClown.Documents.Contents.Fonts.AFM;
using PdfClown.Documents.Contents.Fonts.Type1;
using PdfClown.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
     * Embedded PDType1Font builder. Helper class to populate a PDType1Font from a PFB and AFM.
     *
     * @author Michael Niedermair
     */
    internal class FontType1Embedder
    {
        private readonly Encoding fontEncoding;
        private readonly Type1Font type1;

        /**
		 * This will load a PFB to be embedded into a document.
		 *
		 * @param doc The PDF document that will hold the embedded font.
		 * @param dict The Font dictionary to write to.
		 * @param pfbStream The pfb input.
		 * @throws IOException If there is an error loading the data.
		 */
        public FontType1Embedder(Document doc, PdfDictionary dict, Bytes.IInputStream pfbStream, Encoding encoding)
        {
            dict[PdfName.Subtype] = PdfName.Type1;

            // read the pfb
            var pfbBytes = pfbStream.AsMemory();
            var pfbParser = new PfbParser(pfbBytes);
            type1 = Type1Font.CreateWithPFB(pfbBytes, pfbParser);

            if (encoding == null)
            {
                fontEncoding = Type1Encoding.FromFontBox(type1.Encoding);
            }
            else
            {
                fontEncoding = encoding;
            }

            // build font descriptor
            var fd = BuildFontDescriptor(type1);

            var fontStream = new PdfStream(pfbParser.GetInputStream());
            fontStream.Header.SetInt(PdfName.Length, pfbParser.Size);
            for (int i = 0; i < pfbParser.Lengths.Length; i++)
            {
                fontStream.Header[new PdfName("Length" + (i + 1))] = PdfInteger.Get(pfbParser.Lengths[i]);
            }
            fd.FontFile = new FontFile(doc, fontStream);

            // set the values
            dict[PdfName.FontDescriptor] = fd.BaseObject;
            dict[PdfName.BaseFont] = PdfName.Get(type1.Name);

            // widths
            List<int> widths = new List<int>(256);
            for (int code = 0; code <= 255; code++)
            {
                string name = fontEncoding.GetName(code);
                int width = (int)Math.Round(type1.GetWidth(name));
                widths.Add(width);
            }

            dict[PdfName.FirstChar] = PdfInteger.Get(0);
            dict[PdfName.LastChar] = PdfInteger.Get(255);
            dict[PdfName.Widths] = PdfArray.FromInts(widths);
            dict[PdfName.Encoding] = encoding.GetPdfObject();
        }

        /**
		 * Returns a FontDescriptor for the given PFB.
		 */
        public static FontDescriptor BuildFontDescriptor(Type1Font type1)
        {
            bool isSymbolic = type1.Encoding is BuiltInEncoding;

            FontDescriptor fd = new FontDescriptor
            {
                FontName = type1.Name,
                FontFamily = type1.FamilyName,
                NonSymbolic = !isSymbolic,
                Symbolic = isSymbolic,
                FontBBox = new Rectangle(type1.FontBBox),
                ItalicAngle = type1.ItalicAngle,
                Ascent = type1.FontBBox.Top,
                Descent = type1.FontBBox.Bottom,
                CapHeight = type1.BlueValues[2],
                StemV = 0 // for PDF/A
            };
            return fd;
        }


        /**
		 * Returns a FontDescriptor for the given AFM. Used only for Standard 14 fonts.
		 *
		 * @param metrics AFM
		 */
        public static FontDescriptor BuildFontDescriptor(FontMetrics metrics)
        {
            bool isSymbolic = metrics.EncodingScheme.Equals("FontSpecific", StringComparison.Ordinal);

            FontDescriptor fd = new FontDescriptor
            {
                FontName = metrics.FontName,
                FontFamily = metrics.FamilyName,
                NonSymbolic = !isSymbolic,
                Symbolic = isSymbolic,
                FontBBox = new Rectangle(metrics.FontBBox),
                ItalicAngle = metrics.ItalicAngle,
                Ascent = metrics.Ascender,
                Descent = metrics.Descender,
                CapHeight = metrics.CapHeight,
                XHeight = metrics.XHeight,
                AvgWidth = metrics.GetAverageCharacterWidth(),
                CharSet = metrics.CharacterSet,
                StemV = 0 // for PDF/A
            };
            return fd;
        }

        /**
		 * Returns the font's encoding.
		 */
        public Encoding FontEncoding
        {
            get => fontEncoding;
        }

        /**
		 * Returns the font's glyph list.
		 */
        public GlyphMapping GlyphList
        {
            get => GlyphMapping.Default;
        }

        /**
		 * Returns the Type 1 font.
		 */
        public Type1Font Type1Font
        {
            get => type1;
        }
    }
}
