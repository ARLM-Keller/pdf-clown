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
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfClown.Documents.Contents.Fonts
{

    /**
     * A CIDFont. A CIDFont is a PDF object that contains information about a CIDFont program. Although
     * its Type value is Font, a CIDFont is not actually a font.
     *
     * <p>It is not usually necessary to use this class directly, prefer {@link PDType0Font}.
     *
     * @author Ben Litchfield
     */
    public abstract class FontCID : Font// PDFontLike, PDVectorFont
    {
        public static FontCID WrapFont(PdfDirectObject baseObject, FontType0 parent)
        {
            if (baseObject == null)
                return null;

            if (baseObject.Wrapper is FontCID cidFont)
            {
                return cidFont;
            }
            if (baseObject is PdfReference pdfReference
               && pdfReference.DataObject?.Wrapper is FontCID referenceFont)
            {
                baseObject.Wrapper = referenceFont;
                return referenceFont;
            }

            var dictionary = (PdfDictionary)baseObject.Resolve();
            PdfName type = dictionary.GetName(PdfName.Type, PdfName.Font);
            if (!PdfName.Font.Equals(type))
            {
                throw new IOException("Expected 'Font' dictionary but found '" + type.StringValue + "'");
            }

            PdfName subType = dictionary.GetName(PdfName.Subtype);
            if (PdfName.CIDFontType0.Equals(subType))
            {
                try
                {
                    return new FontCIDType0(dictionary, parent);
                }
                catch
                {
                    return new FontCIDType2(dictionary, parent);
                }
            }
            else if (PdfName.CIDFontType2.Equals(subType))
            {
                return new FontCIDType2(dictionary, parent);
            }
            else
            {
                throw new IOException("Invalid font type: " + type);
            }
        }

        protected readonly FontType0 parent;

        private new Dictionary<int, float> widths;
        private int? defaultWidth;
        private float averageWidth;

        private readonly Dictionary<int, float> verticalDisplacementY = new Dictionary<int, float>(); // w1y
        private readonly Dictionary<int, SKPoint> positionVectors = new Dictionary<int, SKPoint>();     // v
        private float[] dw2 = new float[] { 880, -1000 };

        public FontCID(Document document)
            : this(document, new PdfDictionary { { PdfName.Type, PdfName.Font } })
        { }

        public FontCID(Document document, PdfDictionary fontObject)
            : base(document, fontObject)
        {
        }

        internal FontCID(PdfDirectObject fontObject)
            : base(fontObject)
        {
            ReadWidths();
            ReadVerticalDisplacements();
        }

        /**
         * Constructor.
         *
         * @param fontDictionary The font dictionary according to the PDF specification.
         */
        public FontCID(PdfDirectObject fontObject, FontType0 parent)
            : base(fontObject)
        {
            this.parent = parent;
            ReadWidths();
            ReadVerticalDisplacements();
        }

        /**
         * The PostScript name of the font.
         *
         * @return The postscript name of the font.
         */

        public CIDSystemInfo CIDSystemInfo
        {
            get => Wrap<CIDSystemInfo>(Dictionary[PdfName.CIDSystemInfo]);
            set => Dictionary[PdfName.BaseFont] = value?.BaseObject;
        }

        public int DefaultWidth
        {
            get => defaultWidth ?? (defaultWidth = Dictionary.GetInt(PdfName.DW, 1000)).Value;
            set => Dictionary[PdfName.DW] = new PdfInteger(value);
        }

        public override PdfArray Widths
        {
            get => (PdfArray)Dictionary.Resolve(PdfName.W);
            set => Dictionary[PdfName.W] = value;
        }

        public PdfArray VerticalDefaultWidth
        {
            get => (PdfArray)Dictionary.Resolve(PdfName.DW2);
            set => Dictionary[PdfName.DW2] = value;
        }

        public PdfArray VerticaltWidths
        {
            get => (PdfArray)Dictionary.Resolve(PdfName.W2);
            set => Dictionary[PdfName.W2] = value;
        }

        public PdfDataObject CIDToGIDMap
        {
            get => Dictionary.Resolve(PdfName.CIDToGIDMap);
            set => Dictionary[PdfName.CIDToGIDMap] = value.Reference;
        }

        /**
         * Returns the Type 0 font which is the parent of this font.
         *
         * @return parent Type 0 font
         */
        public virtual FontType0 Parent
        {
            get => parent;
        }

        public override bool WillBeSubset => false;

        public abstract BaseFont GenericFont { get; }

        private void ReadWidths()
        {
            widths = new Dictionary<int, float>();
            var wArray = Widths;
            if (wArray != null)
            {
                int size = wArray.Count;
                int counter = 0;
                while (counter < size)
                {
                    var startRangeNullable = wArray.GetNInt(counter++);
                    if (startRangeNullable is not int startRange)
                    {
                        Debug.WriteLine($"warn: Expected a number array member, got {wArray[counter - 1]}");
                        continue;
                    }
                    var next = wArray.Resolve(counter++);
                    if (next is PdfArray array)
                    {
                        int arraySize = array.Count;
                        for (int i = 0; i < arraySize; i++)
                        {
                            var floatNullable = array.GetNFloat(i);
                            if (floatNullable is not float floatValue)
                            {
                                Debug.WriteLine($"warn: Expected a number array member, got {array[i]}");
                                continue;
                            }
                            widths[startRange + i] = floatValue;
                        }
                    }
                    else if (next is IPdfNumber number
                        && counter < size
                        && wArray.GetNFloat(counter++) is float width)
                    {
                        int endRange = number.IntValue;
                        for (int i = startRange; i <= endRange; i++)
                        {
                            widths[i] = width;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"warn: Expected two numbers, got {next} and {wArray[counter - 1]}");
                    }
                }
            }
        }

        private void ReadVerticalDisplacements()
        {
            // default position vector and vertical displacement vector
            var dw2Array = VerticalDefaultWidth;
            if (dw2Array != null)
            {
                PdfObject base0 = dw2Array.Resolve(0);
                PdfObject base1 = dw2Array.Resolve(1);
                if (base0 is IPdfNumber number0 && base1 is IPdfNumber number1)
                {
                    dw2[0] = number0.FloatValue;
                    dw2[1] = number1.FloatValue;
                }
            }

            // vertical metrics for individual CIDs.
            var w2Array = VerticaltWidths;
            if (w2Array != null)
            {
                for (int i = 0; i < w2Array.Count; i++)
                {
                    var c = w2Array.GetInt(i);
                    PdfObject next = w2Array.Resolve(++i);
                    if (next is PdfArray array)
                    {
                        for (int j = 0; j < array.Count; j++)
                        {
                            int cid = c + j / 3;
                            var w1y = array.GetFloat(j);
                            var v1x = array.GetFloat(++j);
                            var v1y = array.GetFloat(++j);
                            verticalDisplacementY[cid] = w1y;
                            positionVectors[cid] = new SKPoint(v1x, v1y);
                        }
                    }
                    else
                    {
                        int first = c;
                        int last = ((IPdfNumber)next).IntValue;
                        var w1y = w2Array.GetFloat(++i);
                        var v1x = w2Array.GetFloat(++i);
                        var v1y = w2Array.GetFloat(++i);
                        for (int cid = first; cid <= last; cid++)
                        {
                            verticalDisplacementY[cid] = w1y;
                            positionVectors[cid] = new SKPoint(v1x, v1y);
                        }
                    }
                }
            }
        }

        /**
         * Returns the default position vector (v).
         *
         * @param cid CID
         */
        public virtual SKPoint GetDefaultPositionVector(int cid)
        {
            return new SKPoint(GetWidthForCID(cid) / 2, dw2[0]);
        }

        public float GetWidthForCID(int cid)
        {
            if (widths.TryGetValue(cid, out var width))
                return width;
            return DefaultWidth;
        }

        public override bool HasExplicitWidth(int code)
        {
            var cid = CodeToCID(code);
            return widths.TryGetValue(cid, out _);
        }

        public override SKPoint GetPositionVector(int code)
        {
            int cid = CodeToCID(code);
            if (positionVectors.TryGetValue(cid, out var position))
                return position;
            return GetDefaultPositionVector(cid);
        }

        /**
         * Returns the y-component of the vertical displacement vector (w1).
         *
         * @param code character code
         * @return w1y
         */
        public virtual float GetVerticalDisplacementVectorY(int code)
        {
            int cid = CodeToCID(code);
            if (verticalDisplacementY.TryGetValue(cid, out var w1y))
            {
                return w1y;
            }
            return dw2[1];
        }

        public override float GetWidth(int code)
        {
            // these widths are supposed to be consistent with the actual widths given in the CIDFont
            // program, but PDFBOX-563 shows that when they are not, Acrobat overrides the embedded
            // font widths with the widths given in the font dictionary
            return GetWidthForCID(CodeToCID(code));
        }

        // todo: this method is highly suspicious, the average glyph width is not usually a good metric
        public override float AverageFontWidth
        {
            get
            {
                if (averageWidth == 0)
                {
                    float totalWidths = 0.0f;
                    int characterCount = 0;
                    if (widths != null)
                    {
                        foreach (float width in widths.Values)
                        {
                            if (width > 0)
                            {
                                totalWidths += width;
                                ++characterCount;
                            }
                        }
                    }
                    if (characterCount != 0)
                    {
                        averageWidth = totalWidths / characterCount;
                    }
                    if (averageWidth <= 0 || averageWidth == float.NaN)
                    {
                        averageWidth = DefaultWidth;
                    }
                }
                return averageWidth;
            }
        }

        /**
         * Returns the CID for the given character code. If not found then CID 0 is returned.
         *
         * @param code character code
         * @return CID
         */
        public abstract int CodeToCID(int code);

        /**
         * Returns the GID for the given character code.
         *
         * @param code character code
         * @return GID
         * @throws java.io.IOException
         */
        public abstract int CodeToGID(int code);

        public abstract void EncodeGlyphId(Span<byte> bytes, int glyphId);

        /**
         * Encodes the given Unicode code point for use in a PDF content stream.
         * Content streams use a multi-byte encoding with 1 to 4 bytes.
         *
         * <p>This method is called when embedding text in PDFs and when filling in fields.
         *
         * @param unicode Unicode code point.
         * @return Array of 1 to 4 PDF content stream bytes.
         * @throws IOException If the text could not be encoded.
         */

        public virtual int[] ReadCIDToGIDMap()
        {
            int[] cid2gid = null;
            if (CIDToGIDMap is PdfStream stream)
            {
                var body = stream.GetBody(false);
                using var input = body.Extract(stream.Filter, stream.Parameters, stream.Header);
                var mapAsBytes = input.AsMemory().Span;
                var length = (int)input.Length;
                int numberOfInts = length / 2;
                cid2gid = new int[numberOfInts];
                int offset = 0;
                for (int index = 0; index < numberOfInts; index++)
                {
                    int gid = (mapAsBytes[offset] & 0xff) << 8 | mapAsBytes[offset + 1] & 0xff;
                    cid2gid[index] = gid;
                    offset += 2;
                }
            }
            return cid2gid;
        }

        public override void AddToSubset(int codePoint)
        {
            throw new NotImplementedException();
        }

        public override void Subset()
        {
            throw new NotImplementedException();
        }

        protected override float GetStandard14Width(int code)
        {
            throw new NotImplementedException();
        }
    }
}