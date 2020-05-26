/* Copyright 2012 Mozilla Foundation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using PdfClown.Bytes.Filters.CCITT;
using PdfClown.Tokens;
using PdfClown.Util.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PdfClown.Bytes.Filters.JBig
{
    //import { BaseException, shadow}from "../shared/util.js";
    //import { log2, readInt8, readUint16, readUint32 } from "./core_utils.js";
    //import { ArithmeticDecoder } from "./arithmetic_decoder.js";
    //import { CCITTFaxDecoder } from "./ccitt.js";

    internal class Jbig2Error : Exception
    {
        public Jbig2Error(string msg) : base($"JBIG2 error: {msg}")
        {

        }
    }

    internal class Jbig2Image
    {
        // 7.3 Segment types
        static readonly string[] SegmentTypes = new string[]{
          "SymbolDictionary",
          null,
          null,
          null,
          "IntermediateTextRegion",
          null,
          "ImmediateTextRegion",
          "ImmediateLosslessTextRegion",
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          "PatternDictionary",
          null,
          null,
          null,
          "IntermediateHalftoneRegion",
          null,
          "ImmediateHalftoneRegion",
          "ImmediateLosslessHalftoneRegion",
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          "IntermediateGenericRegion",
          null,
          "ImmediateGenericRegion",
          "ImmediateLosslessGenericRegion",
          "IntermediateGenericRefinementRegion",
          null,
          "ImmediateGenericRefinementRegion",
          "ImmediateLosslessGenericRefinementRegion",
          null,
          null,
          null,
          null,
          "PageInformation",
          "EndOfPage",
          "EndOfStripe",
          "EndOfFile",
          "Profiles",
          "Tables",
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          null,
          "Extension"
        };

        static readonly List<Point[]> CodingTemplates = new List<Point[]>{
            new Point[]{
                new Point(x: -1, y: -2),
                new Point(x: 0, y: -2),
                new Point(x: 1, y: -2),
                new Point(x: -2, y: -1),
                new Point(x: -1, y: -1),
                new Point(x: 0, y: -1),
                new Point(x: 1, y: -1),
                new Point(x: 2, y: -1),
                new Point(x: -4, y: 0),
                new Point(x: -3, y: 0),
                new Point(x: -2, y: 0),
                new Point(x: -1, y: 0),
            },
            new Point[]{
                new Point(x: -1, y: -2),
                new Point(x: 0, y: -2),
                new Point(x: 1, y: -2),
                new Point(x: 2, y: -2),
                new Point(x: -2, y: -1),
                new Point(x: -1, y: -1),
                new Point(x: 0, y: -1),
                new Point(x: 1, y: -1),
                new Point(x: 2, y: -1),
                new Point(x: -3, y: 0),
                new Point(x: -2, y: 0),
                new Point(x: -1, y: 0),
            },
            new Point[]{
                new Point(x: -1, y: -2),
                new Point(x: 0, y: -2),
                new Point(x: 1, y: -2),
                new Point(x: -2, y: -1),
                new Point(x: -1, y: -1),
                new Point(x: 0, y: -1),
                new Point(x: 1, y: -1),
                new Point(x: -2, y: 0),
                new Point(x: -1, y: 0),
            },
            new Point[]{
                new Point(x: -3, y: -1),
                new Point(x: -2, y: -1),
                new Point(x: -1, y: -1),
                new Point(x: 0, y: -1),
                new Point(x: 1, y: -1),
                new Point(x: -4, y: 0),
                new Point(x: -3, y: 0),
                new Point(x: -2, y: 0),
                new Point(x: -1, y: 0)
            }
        };

        static readonly Refinement[] RefinementTemplates = new Refinement[]
        {
            new Refinement(
                coding: new Point[]{
                    new Point(x: 0, y: -1),
                    new Point(x: 1, y: -1),
                    new Point(x: -1, y: 0),
                },
                reference: new Point[]{
                    new Point(x: 0, y: -1),
                    new Point(x: 1, y: -1),
                    new Point(x: -1, y: 0),
                    new Point(x: 0, y: 0),
                    new Point(x: 1, y: 0),
                    new Point(x: -1, y: 1),
                    new Point(x: 0, y: 1),
                    new Point(x: 1, y: 1),
                }
                ),
            new Refinement(
                coding: new Point[]{
                    new Point(x: -1, y: -1),
                    new Point(x: 0, y: -1),
                    new Point(x: 1, y: -1),
                    new Point(x: -1, y: 0),
                },
                reference: new Point[]{
                    new Point(x: 0, y: -1),
                    new Point(x: -1, y: 0),
                    new Point(x: 0, y: 0),
                    new Point(x: 1, y: 0),
                    new Point(x: 0, y: 1),
                    new Point(x: 1, y: 1),
                }
                ),
        };

        // See 6.2.5.7 Decoding the bitmap.
        static readonly int[] ReusedContexts = new int[]{
            0x9b25, // 10011 0110010 0101
            0x0795, // 0011 110010 101
            0x00e5, // 001 11001 01
            0x0195, // 011001 0101
        };

        static readonly int[] RefinementReusedContexts = new int[]{
            0x0020, // '000' + '0' (coding) + '00010000' + '0' (reference)
            0x0008, // '0000' + '001000'
        };

        static readonly Dictionary<int, HuffmanTable> standardTablesCache = new Dictionary<int, HuffmanTable>();
        static readonly int RegionSegmentInformationFieldLength = 17;
        private object width;
        private object height;

        // Utility data structures
        internal class ContextCache : Dictionary<string, sbyte[]>
        {
            public sbyte[] GetContexts(string id)
            {
                if (TryGetValue(id, out var context))
                    return context;

                return this[id] = new sbyte[1 << 16];
            }
        }

        internal class DecodingContext
        {
            ContextCache cache;
            ArithmeticDecoder decoder;
            internal byte[] data;
            internal int start;
            internal int end;

            public DecodingContext(byte[] data, int start, int end)
            {
                this.data = data;
                this.start = start;
                this.end = end;
            }

            public ArithmeticDecoder Decoder => decoder ?? (decoder = new ArithmeticDecoder(this.data, this.start, this.end));

            public ContextCache ContextCache => cache ?? (cache = new ContextCache());
        }

        // Annex A. Arithmetic Integer Decoding Procedure
        // A.2 Procedure for decoding values
        static int? DecodeInteger(ContextCache contextCache, string procedure, ArithmeticDecoder decoder)
        {
            var contexts = contextCache.GetContexts(procedure);
            var prev = 1;

            int readBits(int Length)
            {
                var v = 0;
                for (var i = 0; i < Length; i++)
                {
                    var bit = decoder.ReadBit(contexts, prev);
                    prev = prev < 256
                        ? (prev << 1) | bit
                        : (((prev << 1) | bit) & 511) | 256;
                    v = (v << 1) | bit;
                }
                return (int)((uint)v >> 0);
            }

            var sign = readBits(1);
            // prettier-ignore
            /* eslint-disable no-nested-ternary */
            var value = readBits(1) != 0 ?
                          (readBits(1) != 0 ?
                            (readBits(1) != 0 ?
                              (readBits(1) != 0 ?
                                (readBits(1) != 0 ?
                                  (readBits(32) + 4436) :
                                readBits(12) + 340) :
                              readBits(8) + 84) :
                            readBits(6) + 20) :
                          readBits(4) + 4) :
                        readBits(2);
            /* eslint-enable no-nested-ternary */
            if (sign == 0)
            {
                return value;
            }
            else if (value > 0)
            {
                return -value;
            }
            return null;
        }

        // A.3 The IAID decoding procedure
        static int DecodeIAID(ContextCache contextCache, ArithmeticDecoder decoder, int codeLength)
        {
            var contexts = contextCache.GetContexts("IAID");

            var prev = 1;
            for (var i = 0; i < codeLength; i++)
            {
                var bit = decoder.ReadBit(contexts, prev);
                prev = (prev << 1) | bit;
            }
            if (codeLength < 31)
            {
                return prev & ((1 << codeLength) - 1);
            }
            return prev & 0x7fffffff;
        }

        static Dictionary<int, byte[]> DecodeBitmapTemplate0(int width, int height, DecodingContext decodingContext)
        {
            var decoder = decodingContext.Decoder;
            var contexts = decodingContext.ContextCache.GetContexts("GB");
            int contextLabel;
            int i, j, pixel;
            byte[] row, row1, row2;
            var bitmap = new Dictionary<int, byte[]>();

            // ...ooooo....
            // ..ooooooo... Context template for current pixel (X)
            // .ooooX...... (concatenate values of 'o'-pixels to get contextLabel)
            var OLD_PIXEL_MASK = 0x7bf7; // 01111 0111111 0111

            for (i = 0; i < height; i++)
            {
                row = bitmap[i] = new byte[width];
                row1 = i < 1 ? row : bitmap[i - 1];
                row2 = i < 2 ? row : bitmap[i - 2];

                // At the beginning of each row:
                // Fill contextLabel with pixels that are above/right of (X)
                contextLabel =
                  (row2[0] << 13) |
                  (row2[1] << 12) |
                  (row2[2] << 11) |
                  (row1[0] << 7) |
                  (row1[1] << 6) |
                  (row1[2] << 5) |
                  (row1[3] << 4);

                for (j = 0; j < width; j++)
                {
                    pixel = decoder.ReadBit(contexts, contextLabel);
                    row[j] = FiltersExtension.ToByte(pixel);
                    // At each pixel: Clear contextLabel pixels that are shifted
                    // out of the context, then add new ones.
                    contextLabel =
                      ((contextLabel & OLD_PIXEL_MASK) << 1) |
                      (j + 3 < width ? row2[j + 3] << 11 : 0) |
                      (j + 4 < width ? row1[j + 4] << 4 : 0) |
                      pixel;
                }
            }

            return bitmap;
        }

        // 6.2 Generic Region Decoding Procedure
        static Dictionary<int, byte[]> DecodeBitmap(bool mmr, int width, int height,
          int templateIndex, bool prediction, Dictionary<int, byte[]> skip, List<Point> at,
          DecodingContext decodingContext)
        {
            if (mmr)
            {
                var input = new Bytes.Buffer(decodingContext.data, decodingContext.start, decodingContext.end);
                return DecodeMMRBitmap(input, width, height, false);
            }

            // Use optimized version for the most common case
            if (
              templateIndex == 0 &&
              skip == null &&
              !prediction &&
              at.Count == 4 &&
              at[0].x == 3 &&
              at[0].y == -1 &&
              at[1].x == -3 &&
              at[1].y == -1 &&
              at[2].x == 2 &&
              at[2].y == -2 &&
              at[3].x == -2 &&
              at[3].y == -2
            )
            {
                return DecodeBitmapTemplate0(width, height, decodingContext);
            }

            var useskip = skip != null;
            var template = CodingTemplates[templateIndex].Concat(at).ToList();

            // Sorting is non-standard, and it is not required. But sorting increases
            // the number of template bits that can be reused from the previous
            // contextLabel in the main loop.
            template.Sort((a, b) =>
            {
                var y = a.y.CompareTo(b.y);
                return y == 0 ? a.x.CompareTo(b.x) : y;
            });

            var templateLength = template.Count;
            var templateX = new sbyte[templateLength];
            var templateY = new sbyte[templateLength];
            var changingTemplateEntries = new List<int>();
            int reuseMask = 0, minX = 0, maxX = 0, minY = 0;
            int c, k;

            for (k = 0; k < templateLength; k++)
            {
                templateX[k] = template[k].x;
                templateY[k] = template[k].y;
                minX = Math.Min(minX, template[k].x);
                maxX = Math.Max(maxX, template[k].x);
                minY = Math.Min(minY, template[k].y);
                // Check if the template pixel appears in two consecutive context labels,
                // so it can be reused. Otherwise, we add it to the list of changing
                // template entries.
                if (k < templateLength - 1 &&
                    template[k].y == template[k + 1].y &&
                    template[k].x == template[k + 1].x - 1)
                {
                    reuseMask |= 1 << (templateLength - 1 - k);
                }
                else
                {
                    changingTemplateEntries.Add(k);
                }
            }
            var changingEntriesLength = changingTemplateEntries.Count;

            var changingTemplateX = new sbyte[changingEntriesLength];
            var changingTemplateY = new sbyte[changingEntriesLength];
            var changingTemplateBit = new ushort[changingEntriesLength];
            for (c = 0; c < changingEntriesLength; c++)
            {
                k = changingTemplateEntries[c];
                changingTemplateX[c] = template[k].x;
                changingTemplateY[c] = template[k].y;
                changingTemplateBit[c] = (ushort)(1 << (templateLength - 1 - k));
            }

            // Get the safe bounding box edges from the width, height, minX, maxX, minY
            var sbb_left = -minX;
            var sbb_top = -minY;
            var sbb_right = width - maxX;

            var pseudoPixelContext = ReusedContexts[templateIndex];
            var row = new byte[width];
            var bitmap = new Dictionary<int, byte[]>();

            var decoder = decodingContext.Decoder;
            var contexts = decodingContext.ContextCache.GetContexts("GB");

            int ltp = 0, j, i0, j0, contextLabel = 0, bit, shift;
            for (var i = 0; i < height; i++)
            {
                if (prediction)
                {
                    var sltp = decoder.ReadBit(contexts, pseudoPixelContext);
                    ltp ^= sltp;
                    if (ltp != 0)
                    {
                        bitmap.Add(bitmap.Count, row); // duplicate previous row
                        continue;
                    }
                }
                row = (byte[])row.Clone();
                bitmap.Add(bitmap.Count, row);
                for (j = 0; j < width; j++)
                {
                    if (useskip && skip[i][j] != 0)
                    {
                        row[j] = 0;
                        continue;
                    }
                    // Are we in the middle of a scanline, so we can reuse contextLabel
                    // bits?
                    if (j >= sbb_left && j < sbb_right && i >= sbb_top)
                    {
                        // If yes, we can just shift the bits that are reusable and only
                        // fetch the remaining ones.
                        contextLabel = (contextLabel << 1) & reuseMask;
                        for (k = 0; k < changingEntriesLength; k++)
                        {
                            i0 = i + changingTemplateY[k];
                            j0 = j + changingTemplateX[k];

                            if (bitmap.TryGetValue(i0, out var bits) && j0 < bits.Length && (bit = bits[j0]) != 0)
                            {
                                bit = changingTemplateBit[k];
                                contextLabel |= bit;
                            }
                        }
                    }
                    else
                    {
                        // compute the contextLabel from scratch
                        contextLabel = 0;
                        shift = templateLength - 1;
                        for (k = 0; k < templateLength; k++, shift--)
                        {
                            j0 = j + templateX[k];
                            if (j0 >= 0 && j0 < width)
                            {
                                i0 = i + templateY[k];
                                if (i0 >= 0)
                                {
                                    if (bitmap.TryGetValue(i0, out var bits) && j0 < bits.Length && (bit = bits[j0]) != 0)
                                    {
                                        contextLabel |= bit << shift;
                                    }
                                }
                            }
                        }
                    }
                    var pixel = decoder.ReadBit(contexts, contextLabel);
                    row[j] = FiltersExtension.ToByte(pixel);
                }
            }
            return bitmap;
        }

        // 6.3.2 Generic Refinement Region Decoding Procedure
        static Dictionary<int, byte[]> DecodeRefinement(int width, int height, int templateIndex,
           Dictionary<int, byte[]> referenceBitmap, int offsetX, int offsetY,
           bool prediction, List<Point> at, DecodingContext decodingContext)
        {
            var codingTemplate = RefinementTemplates[templateIndex].coding;
            if (templateIndex == 0)
            {
                codingTemplate = codingTemplate.Concat(new Point[] { at[0] }).ToArray();
            }
            var codingTemplateLength = codingTemplate.Length;
            var codingTemplateX = new int[codingTemplateLength];
            var codingTemplateY = new int[codingTemplateLength];
            int k;
            for (k = 0; k < codingTemplateLength; k++)
            {
                codingTemplateX[k] = codingTemplate[k].x;
                codingTemplateY[k] = codingTemplate[k].y;
            }

            var referenceTemplate = RefinementTemplates[templateIndex].reference;
            if (templateIndex == 0)
            {
                referenceTemplate = referenceTemplate.Concat(new Point[] { at[1] }).ToArray();
            }
            var referenceTemplateLength = referenceTemplate.Length;
            var referenceTemplateX = new int[referenceTemplateLength];
            var referenceTemplateY = new int[referenceTemplateLength];
            for (k = 0; k < referenceTemplateLength; k++)
            {
                referenceTemplateX[k] = referenceTemplate[k].x;
                referenceTemplateY[k] = referenceTemplate[k].y;
            }
            var referenceWidth = referenceBitmap[0].Length;
            var referenceHeight = referenceBitmap.Count;

            var pseudoPixelContext = RefinementReusedContexts[templateIndex];
            var bitmap = new Dictionary<int, byte[]>();

            var decoder = decodingContext.Decoder;
            var contexts = decodingContext.ContextCache.GetContexts("GR");

            var ltp = 0;
            for (var i = 0; i < height; i++)
            {
                if (prediction)
                {
                    var sltp = decoder.ReadBit(contexts, pseudoPixelContext);
                    ltp ^= sltp;
                    if (ltp != 0)
                    {
                        throw new Jbig2Error("prediction is not supported");
                    }
                }
                var row = new byte[width];
                bitmap.Add(bitmap.Count, row);
                for (var j = 0; j < width; j++)
                {
                    int i0, j0;
                    var contextLabel = 0;
                    for (k = 0; k < codingTemplateLength; k++)
                    {
                        i0 = i + codingTemplateY[k];
                        j0 = j + codingTemplateX[k];
                        if (i0 < 0 || j0 < 0 || j0 >= width)
                        {
                            contextLabel <<= 1; // out of bound pixel
                        }
                        else
                        {
                            contextLabel = (contextLabel << 1) | bitmap[i0][j0];
                        }
                    }
                    for (k = 0; k < referenceTemplateLength; k++)
                    {
                        i0 = i + referenceTemplateY[k] - offsetY;
                        j0 = j + referenceTemplateX[k] - offsetX;
                        if (
                          i0 < 0 ||
                          i0 >= referenceHeight ||
                          j0 < 0 ||
                          j0 >= referenceWidth
                        )
                        {
                            contextLabel <<= 1; // out of bound pixel
                        }
                        else
                        {
                            contextLabel = (contextLabel << 1) | referenceBitmap[i0][j0];
                        }
                    }
                    var pixel = decoder.ReadBit(contexts, contextLabel);
                    row[j] = FiltersExtension.ToByte(pixel);
                }
            }

            return bitmap;
        }

        // 6.5.5 Decoding the symbol dictionary
        static List<Dictionary<int, byte[]>> DecodeSymbolDictionary(bool huffman, bool refinement, List<Dictionary<int, byte[]>> symbols, int numberOfNewSymbols,
           int numberOfExportedSymbols, HuffmanTables huffmanTables, int templateIndex, List<Point> at, int refinementTemplateIndex,
           List<Point> refinementAt, DecodingContext decodingContext, Bytes.Buffer huffmanInput)
        {
            if (huffman && refinement)
            {
                throw new Jbig2Error("symbol refinement with Huffman is not supported");
            }

            var newSymbols = new List<Dictionary<int, byte[]>>();
            var currentHeight = 0;
            var symbolCodeLength = FiltersExtension.Log2(symbols.Count + numberOfNewSymbols);
            int i = 0;
            var decoder = decodingContext.Decoder;
            var contextCache = decodingContext.ContextCache;
            HuffmanTable tableB1 = null;
            List<int> symbolWidths = null;
            if (huffman)
            {
                tableB1 = GetStandardTable(1); // standard table B.1
                symbolWidths = new List<int>();
                symbolCodeLength = Math.Max(symbolCodeLength, 1); // 6.5.8.2.3
            }

            while (newSymbols.Count < numberOfNewSymbols)
            {
                var deltaHeight = (int)(huffman
                  ? huffmanTables.tableDeltaHeight.Decode(huffmanInput)
                  : DecodeInteger(contextCache, "IADH", decoder)); // 6.5.6
                currentHeight += deltaHeight;
                int currentWidth = 0,
                  totalWidth = 0;
                var firstSymbol = huffman ? symbolWidths.Count : 0;
                while (true)
                {
                    var deltaWidth = huffman
                      ? huffmanTables.tableDeltaWidth.Decode(huffmanInput)
                      : DecodeInteger(contextCache, "IADW", decoder); // 6.5.7
                    if (deltaWidth == null)
                    {
                        break; // OOB
                    }
                    currentWidth += (int)deltaWidth;
                    totalWidth += currentWidth;
                    Dictionary<int, byte[]> bitmap;
                    if (refinement)
                    {
                        // 6.5.8.2 Refinement/aggregate-coded symbol bitmap
                        var numberOfInstances = DecodeInteger(contextCache, "IAAI", decoder);
                        if (numberOfInstances > 1)
                        {
                            bitmap = DecodeTextRegion(
                              huffman,
                              refinement,
                              currentWidth,
                              currentHeight,
                              0,
                              numberOfInstances,
                              1, // strip size
                              symbols.Concat(newSymbols).ToList(),
                              symbolCodeLength,
                              false, // transposed
                              0, // ds offset
                              1, // top left 7.4.3.1.1
                              0, // OR operator
                              huffmanTables,
                              refinementTemplateIndex,
                              refinementAt,
                              decodingContext,
                              0,
                              huffmanInput
                            );
                        }
                        else
                        {
                            var symbolId = DecodeIAID(contextCache, decoder, symbolCodeLength);
                            var rdx = (int)DecodeInteger(contextCache, "IARDX", decoder); // 6.4.11.3
                            var rdy = (int)DecodeInteger(contextCache, "IARDY", decoder); // 6.4.11.4
                            var symbol =
                              symbolId < symbols.Count
                                ? symbols[symbolId]
                                : newSymbols[symbolId - symbols.Count];
                            bitmap = DecodeRefinement(currentWidth, currentHeight, refinementTemplateIndex, symbol, rdx, rdy, false, refinementAt, decodingContext);
                        }
                        newSymbols.Add(bitmap);
                    }
                    else if (huffman)
                    {
                        // Store only symbol width and decode a collective bitmap when the
                        // height class is done.
                        symbolWidths.Add(currentWidth);
                    }
                    else
                    {
                        // 6.5.8.1 Direct-coded symbol bitmap
                        bitmap = DecodeBitmap(false, currentWidth, currentHeight, templateIndex, false, null, at, decodingContext);
                        newSymbols.Add(bitmap);
                    }
                }
                if (huffman && !refinement)
                {
                    // 6.5.9 Height class collective bitmap
                    var bitmapSize = huffmanTables.tableBitmapSize.Decode(huffmanInput);
                    huffmanInput.ByteAlign();
                    Dictionary<int, byte[]> collectiveBitmap;
                    if (bitmapSize == null || bitmapSize == 0)
                    {
                        // Uncompressed collective bitmap
                        collectiveBitmap = ReadUncompressedBitmap(huffmanInput, totalWidth, currentHeight);
                    }
                    else
                    {
                        // MMR collective bitmap
                        var originalL = (int)huffmanInput.Length;
                        var bitmapL = (int)bitmapSize;
                        huffmanInput.SetLength(bitmapL);
                        collectiveBitmap = DecodeMMRBitmap(huffmanInput, totalWidth, currentHeight, false);
                        huffmanInput.SetLength(originalL);
                        huffmanInput.Position += bitmapL;
                    }
                    var numberOfSymbolsDecoded = symbolWidths.Count;
                    if (firstSymbol == numberOfSymbolsDecoded - 1)
                    {
                        // collectiveBitmap is a single symbol.
                        newSymbols.Add(collectiveBitmap);
                    }
                    else
                    {
                        // Divide collectiveBitmap into symbols.
                        int y, xMin = 0, xMax, bitmapWidth;
                        Dictionary<int, byte[]> symbolBitmap = null;
                        for (i = firstSymbol; i < numberOfSymbolsDecoded; i++)
                        {
                            bitmapWidth = symbolWidths[i];
                            xMax = xMin + bitmapWidth;
                            symbolBitmap = new Dictionary<int, byte[]>();
                            for (y = 0; y < currentHeight; y++)
                            {
                                symbolBitmap.Add(symbolBitmap.Count, collectiveBitmap[y].SubArray(xMin, xMax));
                            }
                            newSymbols.Add(symbolBitmap);
                            xMin = xMax;
                        }
                    }
                }
            }

            // 6.5.10 Exported symbols
            var exportedSymbols = new List<Dictionary<int, byte[]>>();
            var flags = new List<bool>();
            var currentFlag = false;
            var totalSymbolsLength = symbols.Count + numberOfNewSymbols;
            while (flags.Count < totalSymbolsLength)
            {
                var runLength = (int)(huffman
                  ? tableB1.Decode(huffmanInput)
                  : DecodeInteger(contextCache, "IAEX", decoder));
                while (runLength-- != 0)
                {
                    flags.Add(currentFlag);
                }
                currentFlag = !currentFlag;
            }

            for (var ii = symbols.Count; i < ii; i++)
            {
                if (flags[i])
                {
                    exportedSymbols.Add(symbols[i]);
                }
            }
            for (var j = 0; j < numberOfNewSymbols; i++, j++)
            {
                if (flags[i])
                {
                    exportedSymbols.Add(newSymbols[j]);
                }
            }
            return exportedSymbols;
        }

        static Dictionary<int, byte[]> DecodeTextRegion(bool huffman, bool refinement, int width, int height,
          byte defaultPixelValue, int? numberOfSymbolInstances, int stripSize, List<Dictionary<int, byte[]>> inputSymbols, int symbolCodeLength,
          bool transposed, int dsOffset, int referenceCorner, int combinationOperator, HuffmanTables huffmanTables,
          int refinementTemplateIndex, List<Point> refinementAt, DecodingContext decodingContext, int logStripSize, Bytes.Buffer huffmanInput)
        {
            if (huffman && refinement)
            {
                throw new Jbig2Error("refinement with Huffman is not supported");
            }

            // Prepare bitmap
            var bitmap = new Dictionary<int, byte[]>();
            int i;
            byte[] row;
            for (i = 0; i < height; i++)
            {
                row = new byte[width];
                if (defaultPixelValue != 0)
                {
                    for (var j = 0; j < width; j++)
                    {
                        row[j] = defaultPixelValue;
                    }
                }
                bitmap.Add(bitmap.Count, row);
            }

            var decoder = decodingContext.Decoder;
            var contextCache = decodingContext.ContextCache;

            var stripT = (int)(huffman
              ? -huffmanTables.tableDeltaT.Decode(huffmanInput)
              : -DecodeInteger(contextCache, "IADT", decoder)); // 6.4.6
            var firstS = 0;
            i = 0;
            while (i < numberOfSymbolInstances)
            {
                var deltaT = (int)(huffman
                  ? huffmanTables.tableDeltaT.Decode(huffmanInput)
                  : DecodeInteger(contextCache, "IADT", decoder)); // 6.4.6
                stripT += deltaT;

                var deltaFirstS = (int)(huffman
                  ? huffmanTables.tableFirstS.Decode(huffmanInput)
                  : DecodeInteger(contextCache, "IAFS", decoder)); // 6.4.7
                firstS += deltaFirstS;
                var currentS = firstS;
                do
                {
                    var currentT = 0; // 6.4.9
                    if (stripSize > 1)
                    {
                        currentT = (int)(huffman
                          ? (int)huffmanInput.ReadBits(logStripSize)
                          : DecodeInteger(contextCache, "IAIT", decoder));
                    }
                    var t = stripSize * stripT + currentT;
                    var symbolId = (int)(huffman
                      ? huffmanTables.symbolIDTable.Decode(huffmanInput)
                      : DecodeIAID(contextCache, decoder, symbolCodeLength));
                    var applyRefinement =
                      refinement &&
                      (huffman
                        ? huffmanInput.ReadBit()
                        : DecodeInteger(contextCache, "IARI", decoder)) != 0;
                    var symbolBitmap = inputSymbols[symbolId];
                    var symbolWidth = symbolBitmap[0].Length;
                    var symbolHeight = symbolBitmap.Count;
                    if (applyRefinement)
                    {
                        var rdw = (int)DecodeInteger(contextCache, "IARDW", decoder); // 6.4.11.1
                        var rdh = (int)DecodeInteger(contextCache, "IARDH", decoder); // 6.4.11.2
                        var rdx = (int)DecodeInteger(contextCache, "IARDX", decoder); // 6.4.11.3
                        var rdy = (int)DecodeInteger(contextCache, "IARDY", decoder); // 6.4.11.4
                        symbolWidth += rdw;
                        symbolHeight += rdh;
                        symbolBitmap = DecodeRefinement(
                          symbolWidth,
                          symbolHeight,
                          refinementTemplateIndex,
                          symbolBitmap,
                          (rdw >> 1) + rdx,
                          (rdh >> 1) + rdy,
                          false,
                          refinementAt,
                          decodingContext
                        );
                    }
                    var offsetT = t - ((referenceCorner & 1) != 0 ? 0 : symbolHeight - 1);
                    var offsetS = currentS - ((referenceCorner & 2) != 0 ? symbolWidth - 1 : 0);
                    int s2, t2;
                    byte[] symbolRow = null;
                    if (transposed)
                    {
                        // Place Symbol Bitmap from T1,S1
                        for (s2 = 0; s2 < symbolHeight; s2++)
                        {
                            if (!bitmap.TryGetValue(offsetS + s2, out row))
                            {
                                continue;
                            }
                            symbolRow = symbolBitmap[s2];
                            // To ignore Parts of Symbol bitmap which goes
                            // outside bitmap region
                            var maxWidth = Math.Min(width - offsetT, symbolWidth);
                            switch (combinationOperator)
                            {
                                case 0: // OR
                                    for (t2 = 0; t2 < maxWidth; t2++)
                                    {
                                        row[offsetT + t2] |= symbolRow[t2];
                                    }
                                    break;
                                case 2: // XOR
                                    for (t2 = 0; t2 < maxWidth; t2++)
                                    {
                                        row[offsetT + t2] ^= symbolRow[t2];
                                    }
                                    break;
                                default:
                                    throw new Jbig2Error(
                                      "operator ${combinationOperator} is not supported"
                                    );
                            }
                        }
                        currentS += symbolHeight - 1;
                    }
                    else
                    {
                        for (t2 = 0; t2 < symbolHeight; t2++)
                        {
                            if (!bitmap.TryGetValue(offsetT + t2, out row))
                            {
                                continue;
                            }
                            symbolRow = symbolBitmap[t2];
                            switch (combinationOperator)
                            {
                                case 0: // OR
                                    for (s2 = 0; s2 < symbolWidth; s2++)
                                    {
                                        row[offsetS + s2] |= symbolRow[s2];
                                    }
                                    break;
                                case 2: // XOR
                                    for (s2 = 0; s2 < symbolWidth; s2++)
                                    {
                                        row[offsetS + s2] ^= symbolRow[s2];
                                    }
                                    break;
                                default:
                                    throw new Jbig2Error(
                                      "operator ${combinationOperator} is not supported"
                                    );
                            }
                        }
                        currentS += symbolWidth - 1;
                    }
                    i++;
                    var deltaS = huffman
                      ? huffmanTables.tableDeltaS.Decode(huffmanInput)
                      : DecodeInteger(contextCache, "IADS", decoder); // 6.4.8
                    if (deltaS == null)
                    {
                        break; // OOB
                    }
                    currentS += (int)deltaS + dsOffset;
                } while (true);
            }
            return bitmap;
        }

        static List<Dictionary<int, byte[]>> DecodePatternDictionary(bool mmr, int patternWidth, int patternHeight, int maxPatternIndex, int template, DecodingContext decodingContext)
        {
            var at = new List<Point>();
            if (!mmr)
            {
                at.Add(new Point(x: (sbyte)-patternWidth, y: 0));
                if (template == 0)
                {
                    at.Add(new Point(x: -3, y: -1));
                    at.Add(new Point(x: 2, y: -2));
                    at.Add(new Point(x: -2, y: -2));
                }
            }
            var collectiveWidth = (maxPatternIndex + 1) * patternWidth;
            var collectiveBitmap = DecodeBitmap(mmr, collectiveWidth, patternHeight, template, false, null, at, decodingContext);
            // Divide collective bitmap into patterns.
            var patterns = new List<Dictionary<int, byte[]>>();
            for (var i = 0; i <= maxPatternIndex; i++)
            {
                var patternBitmap = new Dictionary<int, byte[]>();
                var xMin = patternWidth * i;
                var xMax = xMin + patternWidth;
                for (var y = 0; y < patternHeight; y++)
                {
                    patternBitmap.Add(patternBitmap.Count, collectiveBitmap[y].SubArray(xMin, xMax));
                }
                patterns.Add(patternBitmap);
            }
            return patterns;
        }

        static Dictionary<int, byte[]> DecodeHalftoneRegion(bool mmr, List<Dictionary<int, byte[]>> patterns, int template, int regionWidth,
           int regionHeight, byte defaultPixelValue, bool enableSkip, int combinationOperator, int gridWidth,
           int gridHeight, int gridOffsetX, int gridOffsetY, int gridVectorX, int gridVectorY,
           DecodingContext decodingContext)
        {
            Dictionary<int, byte[]> skip = null;
            if (enableSkip)
            {
                throw new Jbig2Error("skip is not supported");
            }
            if (combinationOperator != 0)
            {
                throw new Jbig2Error(
                  "operator " +
                    combinationOperator +
                    " is not supported in halftone region"
                );
            }

            // Prepare bitmap.
            var regionBitmap = new Dictionary<int, byte[]>();
            int i, j;
            byte[] row;
            for (i = 0; i < regionHeight; i++)
            {
                row = new byte[regionWidth];
                if (defaultPixelValue != 0)
                {
                    for (j = 0; j < regionWidth; j++)
                    {
                        row[j] = defaultPixelValue;
                    }
                }
                regionBitmap.Add(regionBitmap.Count, row);
            }

            var numberOfPatterns = patterns.Count;
            var pattern0 = patterns[0];
            int patternWidth = pattern0[0].Length,
              patternHeight = pattern0.Count;
            var bitsPerValue = FiltersExtension.Log2(numberOfPatterns);
            var at = new List<Point>();
            if (!mmr)
            {
                at.Add(new Point(x: (sbyte)(template <= 1 ? 3 : 2), y: -1));
                if (template == 0)
                {
                    at.Add(new Point(x: -3, y: -1));
                    at.Add(new Point(x: 2, y: -2));
                    at.Add(new Point(x: -2, y: -2));
                }
            }
            // Annex C. Gray-scale Image Decoding Procedure.
            var grayScaleBitPlanes = new Dictionary<int, Dictionary<int, byte[]>>();
            Bytes.Buffer mmrInput = null;
            Dictionary<int, byte[]> bitmap;
            if (mmr)
            {
                // MMR bit planes are in one continuous stream. Only EOFB codes indicate
                // the end of each bitmap, so EOFBs must be decoded.
                mmrInput = new Bytes.Buffer(decodingContext.data, decodingContext.start, decodingContext.end);
            }
            for (i = bitsPerValue - 1; i >= 0; i--)
            {
                if (mmr)
                {
                    bitmap = DecodeMMRBitmap(mmrInput, gridWidth, gridHeight, true);
                }
                else
                {
                    bitmap = DecodeBitmap(false, gridWidth, gridHeight, template, false, skip, at, decodingContext);
                }
                grayScaleBitPlanes[i] = bitmap;
            }
            // 6.6.5.2 Rendering the patterns.
            int mg, ng, bit, patternIndex, x, y;
            byte[] patternRow, regionRow;
            Dictionary<int, byte[]> patternBitmap;
            for (mg = 0; mg < gridHeight; mg++)
            {
                for (ng = 0; ng < gridWidth; ng++)
                {
                    bit = 0;
                    patternIndex = 0;
                    for (j = bitsPerValue - 1; j >= 0; j--)
                    {
                        bit = grayScaleBitPlanes[j][mg][ng] ^ bit; // Gray decoding
                        patternIndex |= bit << j;
                    }
                    patternBitmap = patterns[patternIndex];
                    x = (gridOffsetX + mg * gridVectorY + ng * gridVectorX) >> 8;
                    y = (gridOffsetY + mg * gridVectorX - ng * gridVectorY) >> 8;
                    // Draw patternBitmap at (x, y).
                    if (x >= 0 &&
                      x + patternWidth <= regionWidth &&
                      y >= 0 &&
                      y + patternHeight <= regionHeight)
                    {
                        for (i = 0; i < patternHeight; i++)
                        {
                            regionRow = regionBitmap[y + i];
                            patternRow = patternBitmap[i];
                            for (j = 0; j < patternWidth; j++)
                            {
                                regionRow[x + j] |= patternRow[j];
                            }
                        }
                    }
                    else
                    {
                        int regionX, regionY;
                        for (i = 0; i < patternHeight; i++)
                        {
                            regionY = y + i;
                            if (regionY < 0 || regionY >= regionHeight)
                            {
                                continue;
                            }
                            regionRow = regionBitmap[regionY];
                            patternRow = patternBitmap[i];
                            for (j = 0; j < patternWidth; j++)
                            {
                                regionX = x + j;
                                if (regionX >= 0 && regionX < regionWidth)
                                {
                                    regionRow[regionX] |= patternRow[j];
                                }
                            }
                        }
                    }
                }
            }
            return regionBitmap;
        }

        static SegmentHeader ReadSegmentHeader(byte[] data, int start)
        {
            var segmentHeader = new SegmentHeader();
            segmentHeader.number = (int)data.ReadUint32(start);
            var flags = data[start + 4];
            var segmentType = flags & 0x3f;
            if (SegmentTypes[segmentType] == null)
            {
                throw new Jbig2Error("invalid segment type: " + segmentType);
            }
            segmentHeader.type = segmentType;
            segmentHeader.typeName = SegmentTypes[segmentType];
            segmentHeader.deferredNonRetain = (flags & 0x80) != 0;

            var pageAssociationFieldSize = (flags & 0x40) != 0;
            var referredFlags = data[start + 5];
            long referredToCount = (referredFlags >> 5) & 7;
            var retainBits = new List<int> { referredFlags & 31 };
            var position = start + 6;
            if (referredFlags == 7)
            {
                referredToCount = data.ReadUint32(position - 1) & 0x1fffffff;
                position += 3;
                var bytes = (referredToCount + 7) >> 3;
                retainBits[0] = data[position++];
                while (--bytes > 0)
                {
                    retainBits.Add(data[position++]);
                }
            }
            else if (referredFlags == 5 || referredFlags == 6)
            {
                throw new Jbig2Error("invalid referred-to flags");
            }

            segmentHeader.retainBits = retainBits;

            var referredToSegmentNumberSize = 4;
            if (segmentHeader.number <= 256)
            {
                referredToSegmentNumberSize = 1;
            }
            else if (segmentHeader.number <= 65536)
            {
                referredToSegmentNumberSize = 2;
            }
            var referredTo = new List<int>();
            int i, ii;
            for (i = 0; i < referredToCount; i++)
            {
                int number;
                if (referredToSegmentNumberSize == 1)
                {
                    number = data[position];
                }
                else if (referredToSegmentNumberSize == 2)
                {
                    number = data.ReadUint16(position);
                }
                else
                {
                    number = (int)data.ReadUint32(position);
                }
                referredTo.Add(number);
                position += referredToSegmentNumberSize;
            }
            segmentHeader.referredTo = referredTo;
            if (!pageAssociationFieldSize)
            {
                segmentHeader.pageAssociation = data[position++];
            }
            else
            {
                segmentHeader.pageAssociation = (int)data.ReadUint32(position);
                position += 4;
            }
            segmentHeader.Length = data.ReadUint32(position);
            position += 4;

            if (segmentHeader.Length == 0xffffffff)
            {
                // 7.2.7 Segment data Length, unknown segment Length
                if (segmentType == 38)
                {
                    // ImmediateGenericRegion
                    var genericRegionInfo = ReadRegionSegmentInformation(data, position);
                    var genericRegionSegmentFlags =
                      data[position + RegionSegmentInformationFieldLength];
                    var genericRegionMmr = (genericRegionSegmentFlags & 1) != 0;
                    // searching for the segment end
                    var searchPatternLength = 6;
                    var searchPattern = new byte[searchPatternLength];
                    if (!genericRegionMmr)
                    {
                        searchPattern[0] = 0xff;
                        searchPattern[1] = 0xac;
                    }
                    searchPattern[2] = FiltersExtension.ToByte((int)(((uint)genericRegionInfo.height >> 24) & 0xff));
                    searchPattern[3] = FiltersExtension.ToByte((genericRegionInfo.height >> 16) & 0xff);
                    searchPattern[4] = FiltersExtension.ToByte((genericRegionInfo.height >> 8) & 0xff);
                    searchPattern[5] = FiltersExtension.ToByte(genericRegionInfo.height & 0xff);
                    for (i = position, ii = data.Length; i < ii; i++)
                    {
                        var j = 0;
                        while (j < searchPatternLength && searchPattern[j] == data[i + j])
                        {
                            j++;
                        }
                        if (j == searchPatternLength)
                        {
                            segmentHeader.Length = (uint)(i + searchPatternLength);
                            break;
                        }
                    }
                    if (segmentHeader.Length == 0xffffffff)
                    {
                        throw new Jbig2Error("segment end was not found");
                    }
                }
                else
                {
                    throw new Jbig2Error("invalid unknown segment Length");
                }
            }
            segmentHeader.headerEnd = position;
            return segmentHeader;
        }

        static List<Segment> ReadSegments(SegmentHeader header, byte[] data, int start, int end)
        {
            var segments = new List<Segment>();
            var position = start;
            while (position < end)
            {
                var segmentHeader = ReadSegmentHeader(data, position);
                position = segmentHeader.headerEnd;
                var segment = new Segment(header: segmentHeader, data);
                if (!header.randomAccess)
                {
                    segment.start = position;
                    position += (int)segmentHeader.Length;
                    segment.end = position;
                }
                segments.Add(segment);
                if (segmentHeader.type == 51)
                {
                    break; // end of file is found
                }
            }
            if (header.randomAccess)
            {
                for (int i = 0, ii = segments.Count; i < ii; i++)
                {
                    segments[i].start = position;
                    position += (int)segments[i].header.Length;
                    segments[i].end = position;
                }
            }
            return segments;
        }

        // 7.4.1 Region segment information field
        static RegionSegmentInformation ReadRegionSegmentInformation(byte[] data, int start)
        {
            return new RegionSegmentInformation(
                width: data.ReadUint32(start),
                height: data.ReadUint32(start + 4),
                x: data.ReadUint32(start + 8),
                y: data.ReadUint32(start + 12),
                combinationOperator: data[start + 16] & 7
                );
        }


        static void ProcessSegment(Segment segment, SimpleSegmentVisitor visitor)
        {
            var header = segment.header;

            var data = segment.data;
            var position = segment.start;
            var end = segment.end;
            List<Point> at;
            int i, atLength;
            switch (header.type)
            {
                case 0: // SymbolDictionary
                        // 7.4.2 Symbol dictionary segment syntax
                    var dictionary = new SymbolDictionary();
                    var dictionaryFlags = data.ReadUint16(position); // 7.4.2.1.1
                    dictionary.huffman = (dictionaryFlags & 1) != 0;
                    dictionary.refinement = (dictionaryFlags & 2) != 0;
                    dictionary.huffmanDHSelector = (dictionaryFlags >> 2) & 3;
                    dictionary.huffmanDWSelector = (dictionaryFlags >> 4) & 3;
                    dictionary.bitmapSizeSelector = (dictionaryFlags >> 6) & 1;
                    dictionary.aggregationInstancesSelector = (dictionaryFlags >> 7) & 1;
                    dictionary.bitmapCodingContextUsed = (dictionaryFlags & 256) != 0;
                    dictionary.bitmapCodingContextRetained = (dictionaryFlags & 512) != 0;
                    dictionary.template = (dictionaryFlags >> 10) & 3;
                    dictionary.refinementTemplate = (dictionaryFlags >> 12) & 1;
                    position += 2;
                    if (!dictionary.huffman)
                    {
                        atLength = dictionary.template == 0 ? 4 : 1;
                        at = new List<Point>();
                        for (i = 0; i < atLength; i++)
                        {
                            at.Add(new Point(x: data.ReadInt8(position), y: data.ReadInt8(position + 1)));
                            position += 2;
                        }
                        dictionary.at = at;
                    }
                    if (dictionary.refinement && dictionary.refinementTemplate == 0)
                    {
                        at = new List<Point>();
                        for (i = 0; i < 2; i++)
                        {
                            at.Add(new Point(x: data.ReadInt8(position), y: data.ReadInt8(position + 1)));
                            position += 2;
                        }
                        dictionary.refinementAt = at;
                    }
                    dictionary.numberOfExportedSymbols = (int)data.ReadUint32(position);
                    position += 4;
                    dictionary.numberOfNewSymbols = (int)data.ReadUint32(position);
                    position += 4;
                    visitor.OnSymbolDictionary(dictionary, header.number, header.referredTo, data, position, end);
                    break;
                case 6: // ImmediateTextRegion
                case 7: // ImmediateLosslessTextRegion
                    var textRegion = new TextRegion();
                    textRegion.info = ReadRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var textRegionSegmentFlags = data.ReadUint16(position);
                    position += 2;
                    textRegion.huffman = (textRegionSegmentFlags & 1) != 0;
                    textRegion.refinement = (textRegionSegmentFlags & 2) != 0;
                    textRegion.logStripSize = (textRegionSegmentFlags >> 2) & 3;
                    textRegion.stripSize = 1 << textRegion.logStripSize;
                    textRegion.referenceCorner = (textRegionSegmentFlags >> 4) & 3;
                    textRegion.transposed = (textRegionSegmentFlags & 64) != 0;
                    textRegion.combinationOperator = (textRegionSegmentFlags >> 7) & 3;
                    textRegion.defaultPixelValue = FiltersExtension.ToByte((textRegionSegmentFlags >> 9) & 1);
                    textRegion.dsOffset = (textRegionSegmentFlags << 17) >> 27;
                    textRegion.refinementTemplate = (textRegionSegmentFlags >> 15) & 1;
                    if (textRegion.huffman)
                    {
                        var textRegionHuffmanFlags = data.ReadUint16(position);
                        position += 2;
                        textRegion.huffmanFS = textRegionHuffmanFlags & 3;
                        textRegion.huffmanDS = (textRegionHuffmanFlags >> 2) & 3;
                        textRegion.huffmanDT = (textRegionHuffmanFlags >> 4) & 3;
                        textRegion.huffmanRefinementDW = (textRegionHuffmanFlags >> 6) & 3;
                        textRegion.huffmanRefinementDH = (textRegionHuffmanFlags >> 8) & 3;
                        textRegion.huffmanRefinementDX = (textRegionHuffmanFlags >> 10) & 3;
                        textRegion.huffmanRefinementDY = (textRegionHuffmanFlags >> 12) & 3;
                        textRegion.huffmanRefinementSizeSelector = (textRegionHuffmanFlags & 0x4000) != 0;
                    }
                    if (textRegion.refinement && textRegion.refinementTemplate == 0)
                    {
                        at = new List<Point>();
                        for (i = 0; i < 2; i++)
                        {
                            at.Add(new Point(x: data.ReadInt8(position), y: data.ReadInt8(position + 1)));
                            position += 2;
                        }
                        textRegion.refinementAt = at;
                    }
                    textRegion.numberOfSymbolInstances = (int)data.ReadUint32(position);
                    position += 4;
                    visitor.OnImmediateTextRegion(textRegion, header.referredTo, data, position, end);

                    break;
                case 16: // PatternDictionary
                         // 7.4.4. Pattern dictionary segment syntax
                    var patternDictionary = new PatternDictionary();
                    var patternDictionaryFlags = data[position++];
                    patternDictionary.mmr = (patternDictionaryFlags & 1) != 0;
                    patternDictionary.template = (patternDictionaryFlags >> 1) & 3;
                    patternDictionary.patternWidth = data[position++];
                    patternDictionary.patternHeight = data[position++];
                    patternDictionary.maxPatternIndex = (int)data.ReadUint32(position);
                    position += 4;
                    visitor.OnPatternDictionary(patternDictionary, header.number, data, position, end);
                    break;
                case 22: // ImmediateHalftoneRegion
                case 23: // ImmediateLosslessHalftoneRegion
                         // 7.4.5 Halftone region segment syntax
                    var halftoneRegion = new HalftoneRegion();
                    halftoneRegion.info = ReadRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var halftoneRegionFlags = data[position++];
                    halftoneRegion.mmr = (halftoneRegionFlags & 1) != 0;
                    halftoneRegion.template = (halftoneRegionFlags >> 1) & 3;
                    halftoneRegion.enableSkip = (halftoneRegionFlags & 8) != 0;
                    halftoneRegion.combinationOperator = (halftoneRegionFlags >> 4) & 7;
                    halftoneRegion.defaultPixelValue = FiltersExtension.ToByte((halftoneRegionFlags >> 7) & 1);
                    halftoneRegion.gridWidth = (int)data.ReadUint32(position);
                    position += 4;
                    halftoneRegion.gridHeight = (int)data.ReadUint32(position);
                    position += 4;
                    halftoneRegion.gridOffsetX = (int)(data.ReadUint32(position) & 0xffffffff);
                    position += 4;
                    halftoneRegion.gridOffsetY = (int)(data.ReadUint32(position) & 0xffffffff);
                    position += 4;
                    halftoneRegion.gridVectorX = (int)data.ReadUint16(position);
                    position += 2;
                    halftoneRegion.gridVectorY = (int)data.ReadUint16(position);
                    position += 2;
                    visitor.OnImmediateHalftoneRegion(halftoneRegion, header.referredTo, data, position, end);
                    break;
                case 38: // ImmediateGenericRegion
                case 39: // ImmediateLosslessGenericRegion
                    var genericRegion = new GenericRegion();
                    genericRegion.info = ReadRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var genericRegionSegmentFlags = data[position++];
                    genericRegion.mmr = (genericRegionSegmentFlags & 1) != 0;
                    genericRegion.template = (genericRegionSegmentFlags >> 1) & 3;
                    genericRegion.prediction = (genericRegionSegmentFlags & 8) != 0;
                    if (!genericRegion.mmr)
                    {
                        atLength = genericRegion.template == 0 ? 4 : 1;
                        at = new List<Point>();
                        for (i = 0; i < atLength; i++)
                        {
                            at.Add(new Point(x: data.ReadInt8(position), y: data.ReadInt8(position + 1)));
                            position += 2;
                        }
                        genericRegion.at = at;
                    }
                    visitor.OnImmediateGenericRegion(genericRegion, data, position, end);
                    break;
                case 48: // PageInformation
                    var pageInfo = new PageInformation(
                        width: data.ReadUint32(position),
                        height: data.ReadUint32(position + 4),
                        resolutionX: data.ReadUint32(position + 8),
                        resolutionY: data.ReadUint32(position + 12));
                    if (pageInfo.height == 0xffffffff)
                    {
                        pageInfo.height = 0;
                        //????delete pageInfo.height;
                    }
                    var pageSegmentFlags = data[position + 16];
                    data.ReadUint16(position + 17); // pageStripingInformation
                    pageInfo.lossless = (pageSegmentFlags & 1) != 0;
                    pageInfo.refinement = (pageSegmentFlags & 2) != 0;
                    pageInfo.defaultPixelValue = (pageSegmentFlags >> 2) & 1;
                    pageInfo.combinationOperator = (pageSegmentFlags >> 3) & 3;
                    pageInfo.requiresBuffer = (pageSegmentFlags & 32) != 0;
                    pageInfo.combinationOperatorOverride = (pageSegmentFlags & 64) != 0;
                    visitor.OnPageInformation(pageInfo);
                    break;
                case 49: // EndOfPage
                    break;
                case 50: // EndOfStripe
                    break;
                case 51: // EndOfFile
                    break;
                case 53: // Tables
                    visitor.OnTables(header.number, data, position, end);
                    break;
                case 62: // 7.4.15 defines 2 extension types which
                         // are comments and can be ignored.
                    break;
                default:
                    throw new Jbig2Error($"segment type {header.typeName}({header.type}) is not implemented");
            }
        }

        static void ProcessSegments(List<Segment> segments, SimpleSegmentVisitor visitor)
        {
            for (int i = 0, ii = segments.Count; i < ii; i++)
            {
                ProcessSegment(segments[i], visitor);
            }
        }

        static byte[] ParseJbig2Chunks(List<ImageChunk> chunks)
        {
            var visitor = new SimpleSegmentVisitor();
            for (int i = 0, ii = chunks.Count; i < ii; i++)
            {
                var chunk = chunks[i];
                var segments = ReadSegments(new SegmentHeader(), chunk.data, chunk.start, chunk.end);
                ProcessSegments(segments, visitor);
            }
            return visitor.buffer;
        }

        static ImageData ParseJbig2(byte[] data)
        {
            var end = data.Length;
            var position = 0;

            if (
              data[position] != 0x97 ||
              data[position + 1] != 0x4a ||
              data[position + 2] != 0x42 ||
              data[position + 3] != 0x32 ||
              data[position + 4] != 0x0d ||
              data[position + 5] != 0x0a ||
              data[position + 6] != 0x1a ||
              data[position + 7] != 0x0a
            )
            {
                throw new Jbig2Error("parseJbig2 - invalid header.");
            }

            var header = new SegmentHeader();
            position += 8;
            var flags = data[position++];
            header.randomAccess = (flags & 1) == 0;
            if ((flags & 2) == 0)
            {
                header.numberOfPages = (int)data.ReadUint32(position);
                position += 4;
            }

            var segments = ReadSegments(header, data, position, end);
            var visitor = new SimpleSegmentVisitor();
            ProcessSegments(segments, visitor);

            var width = visitor.currentPageInfo.width;
            var height = visitor.currentPageInfo.height;
            var bitPacked = visitor.buffer;
            var imgData = new byte[width * height];
            int q = 0, k = 0;
            for (var i = 0; i < height; i++)
            {
                int mask = 0;
                byte buffer = 0;
                for (var j = 0; j < width; j++)
                {
                    if (mask == 0)
                    {
                        mask = 128;
                        buffer = bitPacked[k++];
                    }
                    imgData[q++] = (buffer & mask) != 0 ? (byte)0 : (byte)255;
                    mask >>= 1;
                }
            }

            return new ImageData(imgData, (int)width, (int)height);
        }

        internal class SimpleSegmentVisitor
        {
            internal Dictionary<int, HuffmanTable> customTables;
            internal PageInformation currentPageInfo;
            internal byte[] buffer;
            private Dictionary<int, List<Dictionary<int, byte[]>>> symbols;
            private Dictionary<int, List<Dictionary<int, byte[]>>> patterns;

            public void OnPageInformation(PageInformation info)
            {
                this.currentPageInfo = info;
                var rowSize = (info.width + 7) >> 3;
                var buffer = new byte[rowSize * info.height];
                // The contents of ArrayBuffers are initialized to 0.
                // Fill the buffer with 0xFF only if info.defaultPixelValue is set
                if (info.defaultPixelValue != 0)
                {
                    for (int i = 0, ii = buffer.Length; i < ii; i++)
                    {
                        buffer[i] = 0xff;
                    }
                }
                this.buffer = buffer;
            }

            void DrawBitmap(RegionSegmentInformation regionInfo, Dictionary<int, byte[]> bitmap)
            {
                var pageInfo = this.currentPageInfo;
                uint width = regionInfo.width,
                  height = regionInfo.height;
                var rowSize = (pageInfo.width + 7) >> 3;
                var combinationOperator = pageInfo.combinationOperatorOverride
                  ? regionInfo.combinationOperator
                  : pageInfo.combinationOperator;
                var buffer = this.buffer;
                var mask0 = 128 >> (int)(regionInfo.x & 7);
                var offset0 = (uint)(regionInfo.y * rowSize + (regionInfo.x >> 3));
                int i, j;
                int mask;
                uint offset;
                switch (combinationOperator)
                {
                    case 0: // OR
                        for (i = 0; i < height; i++)
                        {
                            mask = mask0;
                            offset = offset0;
                            var exist = bitmap.TryGetValue(i, out var bits);
                            for (j = 0; j < width; j++)
                            {
                                if (exist && j < bits.Length && bits[j] != 0)
                                {
                                    buffer[offset] = FiltersExtension.ToByte(buffer[offset] | mask);
                                }
                                mask >>= 1;
                                if (mask == 0)
                                {
                                    mask = 128;
                                    offset++;
                                }
                            }
                            offset0 += rowSize;
                        }
                        break;
                    case 2: // XOR
                        for (i = 0; i < height; i++)
                        {
                            mask = mask0;
                            offset = offset0;
                            var exist = bitmap.TryGetValue(i, out var bits);
                            for (j = 0; j < width; j++)
                            {
                                if (exist && j < bits.Length && bits[j] != 0)
                                {
                                    buffer[offset] = FiltersExtension.ToByte(buffer[offset] ^ mask);
                                }
                                mask >>= 1;
                                if (mask == 0)
                                {
                                    mask = 128;
                                    offset++;
                                }
                            }
                            offset0 += rowSize;
                        }
                        break;
                    default:
                        throw new Jbig2Error($"operator {combinationOperator} is not supported");
                }
            }

            public void OnImmediateGenericRegion(GenericRegion region, byte[] data, int start, int end)
            {
                var regionInfo = region.info;
                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = DecodeBitmap(
                  region.mmr,
                  (int)regionInfo.width,
                  (int)regionInfo.height,
                  region.template,
                  region.prediction,
                  null,
                  region.at,
                  decodingContext
                );
                this.DrawBitmap(regionInfo, bitmap);
            }

            public void OnSymbolDictionary(SymbolDictionary dictionary, int currentSegment, List<int> referredSegments, byte[] data, int start, int end)
            {
                HuffmanTables huffmanTables = null;
                Bytes.Buffer huffmanInput = null;
                if (dictionary.huffman)
                {
                    huffmanTables = GetSymbolDictionaryHuffmanTables(dictionary, referredSegments, this.customTables);
                    huffmanInput = new Bytes.Buffer(data, start, end);
                }

                // Combines exported symbols from all referred segments
                var symbols = this.symbols;
                if (symbols == null)
                {
                    this.symbols = symbols = new Dictionary<int, List<Dictionary<int, byte[]>>>();
                }

                var inputSymbols = new List<Dictionary<int, byte[]>>();
                for (int i = 0, ii = referredSegments.Count; i < ii; i++)
                {
                    // referredSymbols is undefined when we have a reference to a Tables
                    // segment instead of a SymbolDictionary.
                    if (symbols.TryGetValue(referredSegments[i], out var referredSymbols))
                    {
                        inputSymbols = inputSymbols.Concat(referredSymbols).ToList();
                    }
                }

                var decodingContext = new DecodingContext(data, start, end);
                symbols[currentSegment] = DecodeSymbolDictionary(dictionary.huffman, dictionary.refinement, inputSymbols,
                  dictionary.numberOfNewSymbols, dictionary.numberOfExportedSymbols, huffmanTables, dictionary.template,
                  dictionary.at, dictionary.refinementTemplate, dictionary.refinementAt, decodingContext,
                  huffmanInput
                );
            }

            public void OnImmediateTextRegion(TextRegion region, List<int> referredSegments, byte[] data, int start, int end)
            {
                var regionInfo = region.info;
                HuffmanTables huffmanTables = null;
                Bytes.Buffer huffmanInput = null;

                // Combines exported symbols from all referred segments
                var symbols = this.symbols;
                var inputSymbols = new List<Dictionary<int, byte[]>>();
                for (int i = 0, ii = referredSegments.Count; i < ii; i++)
                {
                    // referredSymbols is undefined when we have a reference to a Tables
                    // segment instead of a SymbolDictionary.
                    if (symbols.TryGetValue(referredSegments[i], out var referredSymbols))
                    {
                        inputSymbols = inputSymbols.Concat(referredSymbols).ToList();
                    }
                }
                var symbolCodeLength = FiltersExtension.Log2(inputSymbols.Count);
                if (region.huffman)
                {
                    huffmanInput = new Bytes.Buffer(data, start, end);
                    huffmanTables = GetTextRegionHuffmanTables(
                      region,
                      referredSegments,
                      this.customTables,
                      inputSymbols.Count,
                      huffmanInput
                    );
                }

                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = DecodeTextRegion(region.huffman, region.refinement, (int)regionInfo.width,
                  (int)regionInfo.height, region.defaultPixelValue, region.numberOfSymbolInstances, region.stripSize,
                  inputSymbols, symbolCodeLength, region.transposed, region.dsOffset,
                  region.referenceCorner, region.combinationOperator, huffmanTables, region.refinementTemplate,
                  region.refinementAt, decodingContext, region.logStripSize, huffmanInput
                );
                this.DrawBitmap(regionInfo, bitmap);
            }

            public void OnPatternDictionary(PatternDictionary dictionary, int currentSegment, byte[] data, int start, int end)
            {
                var patterns = this.patterns;
                if (patterns == null)
                {
                    this.patterns = patterns = new Dictionary<int, List<Dictionary<int, byte[]>>>();
                }
                var decodingContext = new DecodingContext(data, start, end);
                patterns[currentSegment] = DecodePatternDictionary(dictionary.mmr, dictionary.patternWidth, dictionary.patternHeight,
                    dictionary.maxPatternIndex, dictionary.template, decodingContext);
            }

            public void OnImmediateHalftoneRegion(HalftoneRegion region, List<int> referredSegments, byte[] data, int start, int end)
            {
                // HalftoneRegion refers to exactly one PatternDictionary.
                var patterns = this.patterns[referredSegments[0]];
                var regionInfo = region.info;
                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = DecodeHalftoneRegion(
                  region.mmr,
                  patterns,
                  region.template,
                  (int)regionInfo.width,
                  (int)regionInfo.height,
                  region.defaultPixelValue,
                  region.enableSkip,
                  region.combinationOperator,
                  region.gridWidth,
                  region.gridHeight,
                  region.gridOffsetX,
                  region.gridOffsetY,
                  region.gridVectorX,
                  region.gridVectorY,
                  decodingContext
                );
                this.DrawBitmap(regionInfo, bitmap);
            }

            public void OnTables(int currentSegment, byte[] data, int start, int end)
            {
                var customTables = this.customTables;
                if (customTables == null)
                {
                    this.customTables = customTables = new Dictionary<int, HuffmanTable> { };
                }
                customTables[currentSegment] = DecodeTablesSegment(data, start, end);
            }
        }

        internal class HuffmanLine
        {
            internal bool isOOB;
            internal int rangeLow;
            internal int prefixLength;
            internal int rangeLength;
            internal int prefixCode;
            internal bool isLowerRange;

            public HuffmanLine(int prefixLength, int prefixCode)
            {
                // OOB line.
                this.isOOB = true;
                this.rangeLow = 0;
                this.prefixLength = prefixLength;
                this.rangeLength = 0;
                this.prefixCode = prefixCode;
                this.isLowerRange = false;
            }
            public HuffmanLine(int rangeLow, int prefixLength, int rangeLength, int prefixCode, string name = null)
            {
                // Normal, upper range or lower range line.
                // Upper range lines are processed like normal lines.
                this.isOOB = false;
                this.rangeLow = rangeLow;
                this.prefixLength = prefixLength;
                this.rangeLength = rangeLength;
                this.prefixCode = prefixCode;
                this.isLowerRange = name == "lower";
            }
        }

        internal class HuffmanTreeNode
        {
            internal bool isLeaf;
            internal int rangeLength;
            internal int rangeLow;
            internal bool isLowerRange;
            internal bool isOOB;
            internal Dictionary<int, HuffmanTreeNode> children;

            public HuffmanTreeNode(HuffmanLine line)
            {
                this.children = new Dictionary<int, HuffmanTreeNode>();
                if (line != null)
                {
                    // Leaf node
                    this.isLeaf = true;
                    this.rangeLength = line.rangeLength;
                    this.rangeLow = line.rangeLow;
                    this.isLowerRange = line.isLowerRange;
                    this.isOOB = line.isOOB;
                }
                else
                {
                    // Intermediate or root node
                    this.isLeaf = false;
                }
            }


            public void BuildTree(HuffmanLine line, int shift)
            {
                var bit = (line.prefixCode >> shift) & 1;
                if (shift <= 0)
                {
                    // Create a leaf node.
                    this.children[bit] = new HuffmanTreeNode(line);
                }
                else
                {
                    // Create an intermediate node and continue recursively.
                    if (!children.TryGetValue(bit, out var node))
                    {
                        this.children[bit] = node = new HuffmanTreeNode(null);
                    }
                    node.BuildTree(line, shift - 1);
                }
            }

            public int? DecodeNode(Bytes.Buffer reader)
            {
                if (this.isLeaf)
                {
                    if (this.isOOB)
                    {
                        return null;
                    }
                    var htOffset = (int)reader.ReadBits(this.rangeLength);
                    return this.rangeLow + (this.isLowerRange ? -htOffset : htOffset);
                }
                var node = this.children[reader.ReadBit()];
                if (node == null)
                {
                    throw new Jbig2Error("invalid Huffman data");
                }
                return node.DecodeNode(reader);
            }
        }

        internal class HuffmanTable
        {
            private HuffmanTreeNode rootNode;

            public HuffmanTable(List<HuffmanLine> lines, bool prefixCodesDone)
            {
                if (!prefixCodesDone)
                {
                    this.AssignPrefixCodes(lines);
                }
                // Create Huffman tree.
                this.rootNode = new HuffmanTreeNode(null);
                for (int i = 0, ii = lines.Count; i < ii; i++)
                {
                    var line = lines[i];
                    if (line.prefixLength > 0)
                    {
                        this.rootNode.BuildTree(line, line.prefixLength - 1);
                    }
                }
            }

            public int? Decode(Bytes.Buffer reader)
            {
                return this.rootNode.DecodeNode(reader);
            }

            void AssignPrefixCodes(List<HuffmanLine> lines)
            {
                // Annex B.3 Assigning the prefix codes.
                var linesLength = lines.Count;
                var prefixLengthMax = 0;
                for (var i = 0; i < linesLength; i++)
                {
                    prefixLengthMax = Math.Max(prefixLengthMax, lines[i].prefixLength);
                }

                var histogram = new uint[prefixLengthMax + 1];
                for (var i = 0; i < linesLength; i++)
                {
                    histogram[lines[i].prefixLength]++;
                }
                int currentLength = 1,
                  firstCode = 0,
                  currentCode,
                  currentTemp;
                HuffmanLine line;
                histogram[0] = 0;

                while (currentLength <= prefixLengthMax)
                {
                    firstCode = (int)((firstCode + histogram[currentLength - 1]) << 1);
                    currentCode = firstCode;
                    currentTemp = 0;
                    while (currentTemp < linesLength)
                    {
                        line = lines[currentTemp];
                        if (line.prefixLength == currentLength)
                        {
                            line.prefixCode = currentCode;
                            currentCode++;
                        }
                        currentTemp++;
                    }
                    currentLength++;
                }
            }
        }

        static HuffmanTable DecodeTablesSegment(byte[] data, int start, int end)
        {
            // Decodes a Tables segment, i.e., a custom Huffman table.
            // Annex B.2 Code table structure.
            var flags = data[start];
            var lowestValue = data.ReadUint32(start + 1) & 0xffffffff;
            var highestValue = data.ReadUint32(start + 5) & 0xffffffff;
            var reader = new Bytes.Buffer(data, start + 9, end);

            var prefixSizeBits = ((flags >> 1) & 7) + 1;
            var rangeSizeBits = ((flags >> 4) & 7) + 1;
            var lines = new List<HuffmanLine>();
            int prefixLength,
              rangeLength,
              currentRangeLow = (int)lowestValue;

            // Normal table lines
            do
            {
                prefixLength = (int)reader.ReadBits(prefixSizeBits);
                rangeLength = (int)reader.ReadBits(rangeSizeBits);
                lines.Add(new HuffmanLine(currentRangeLow, prefixLength, rangeLength, 0));
                currentRangeLow += 1 << rangeLength;
            } while (currentRangeLow < highestValue);

            // Lower range table line
            prefixLength = (int)reader.ReadBits(prefixSizeBits);
            lines.Add(new HuffmanLine((int)lowestValue - 1, prefixLength, 32, 0, "lower"));

            // Upper range table line
            prefixLength = (int)reader.ReadBits(prefixSizeBits);
            lines.Add(new HuffmanLine((int)highestValue, prefixLength, 32, 0));

            if ((flags & 1) != 0)
            {
                // Out-of-band table line
                prefixLength = (int)reader.ReadBits(prefixSizeBits);
                lines.Add(new HuffmanLine(prefixLength, 0));
            }

            return new HuffmanTable(lines, false);
        }

        static HuffmanTable GetStandardTable(int number)
        {
            // Annex B.5 Standard Huffman tables.
            if (standardTablesCache.TryGetValue(number, out var table))
            {
                return table;
            }
            List<HuffmanLine> lines;
            switch (number)
            {
                case 1:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(0, 1, 4, 0x0 ),
                        new HuffmanLine(16, 2, 8, 0x2 ),
                        new HuffmanLine(272, 3, 16, 0x6 ),
                        new HuffmanLine(65808, 3, 32, 0x7 ) // upper
                    };
                    break;
                case 2:
                    lines = new List<HuffmanLine> {
                       new HuffmanLine(0, 1, 0, 0x0 ),
                       new HuffmanLine(1, 2, 0, 0x2 ),
                       new HuffmanLine(2, 3, 0, 0x6 ),
                       new HuffmanLine(3, 4, 3, 0xe ),
                       new HuffmanLine(11, 5, 6, 0x1e ),
                       new HuffmanLine(75, 6, 32, 0x3e ), // upper
                       new HuffmanLine(6, 0x3f ), // OOB
                      };
                    break;
                case 3:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-256, 8, 8, 0xfe ),
                        new HuffmanLine(0, 1, 0, 0x0 ),
                        new HuffmanLine(1, 2, 0, 0x2 ),
                        new HuffmanLine(2, 3, 0, 0x6 ),
                        new HuffmanLine(3, 4, 3, 0xe ),
                        new HuffmanLine(11, 5, 6, 0x1e ),
                        new HuffmanLine(-257, 8, 32, 0xff, "lower" ),
                        new HuffmanLine(75, 7, 32, 0x7e ), // upper
                        new HuffmanLine(6, 0x3e ), // OOB
                    };
                    break;
                case 4:
                    lines = new List<HuffmanLine> {
                      new HuffmanLine(1, 1, 0, 0x0 ),
                      new HuffmanLine(2, 2, 0, 0x2 ),
                      new HuffmanLine(3, 3, 0, 0x6 ),
                      new HuffmanLine(4, 4, 3, 0xe ),
                      new HuffmanLine(12, 5, 6, 0x1e ),
                      new HuffmanLine(76, 5, 32, 0x1f ), // upper
                    };
                    break;
                case 5:
                    lines = new List<HuffmanLine> {
                      new HuffmanLine(-255, 7, 8, 0x7e ),
                      new HuffmanLine(1, 1, 0, 0x0 ),
                      new HuffmanLine(2, 2, 0, 0x2 ),
                      new HuffmanLine(3, 3, 0, 0x6 ),
                      new HuffmanLine(4, 4, 3, 0xe ),
                      new HuffmanLine(12, 5, 6, 0x1e ),
                      new HuffmanLine(-256, 7, 32, 0x7f, "lower" ),
                      new HuffmanLine(76, 6, 32, 0x3e ), // upper
                    };
                    break;
                case 6:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-2048, 5, 10, 0x1c),
                        new HuffmanLine(-1024, 4, 9, 0x8),
                        new HuffmanLine(-512, 4, 8, 0x9),
                        new HuffmanLine(-256, 4, 7, 0xa),
                        new HuffmanLine(-128, 5, 6, 0x1d),
                        new HuffmanLine(-64, 5, 5, 0x1e),
                        new HuffmanLine(-32, 4, 5, 0xb),
                        new HuffmanLine(0, 2, 7, 0x0),
                        new HuffmanLine(128, 3, 7, 0x2),
                        new HuffmanLine(256, 3, 8, 0x3),
                        new HuffmanLine(512, 4, 9, 0xc),
                        new HuffmanLine(1024, 4, 10, 0xd),
                        new HuffmanLine(-2049, 6, 32, 0x3e, "lower"),
                        new HuffmanLine(2048, 6, 32, 0x3f), // upper
                    };
                    break;
                case 7:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine( -1024, 4, 9, 0x8),
                        new HuffmanLine(-512, 3, 8, 0x0),
                        new HuffmanLine(-256, 4, 7, 0x9),
                        new HuffmanLine(-128, 5, 6, 0x1a),
                        new HuffmanLine(-64, 5, 5, 0x1b),
                        new HuffmanLine(-32, 4, 5, 0xa),
                        new HuffmanLine(0, 4, 5, 0xb),
                        new HuffmanLine(32, 5, 5, 0x1c),
                        new HuffmanLine(64, 5, 6, 0x1d),
                        new HuffmanLine(128, 4, 7, 0xc),
                        new HuffmanLine(256, 3, 8, 0x1),
                        new HuffmanLine(512, 3, 9, 0x2),
                        new HuffmanLine(1024, 3, 10, 0x3),
                        new HuffmanLine(-1025, 5, 32, 0x1e, "lower"),
                        new HuffmanLine(2048, 5, 32, 0x1f), // upper
                    };
                    break;
                case 8:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-15, 8, 3, 0xfc),
                        new HuffmanLine(-7, 9, 1, 0x1fc),
                        new HuffmanLine(-5, 8, 1, 0xfd),
                        new HuffmanLine(-3, 9, 0, 0x1fd),
                        new HuffmanLine(-2, 7, 0, 0x7c),
                        new HuffmanLine(-1, 4, 0, 0xa),
                        new HuffmanLine(0, 2, 1, 0x0),
                        new HuffmanLine(2, 5, 0, 0x1a),
                        new HuffmanLine(3, 6, 0, 0x3a),
                        new HuffmanLine(4, 3, 4, 0x4),
                        new HuffmanLine(20, 6, 1, 0x3b),
                        new HuffmanLine(22, 4, 4, 0xb),
                        new HuffmanLine(38, 4, 5, 0xc),
                        new HuffmanLine(70, 5, 6, 0x1b),
                        new HuffmanLine(134, 5, 7, 0x1c),
                        new HuffmanLine(262, 6, 7, 0x3c),
                        new HuffmanLine(390, 7, 8, 0x7d),
                        new HuffmanLine(646, 6, 10, 0x3d),
                        new HuffmanLine(-16, 9, 32, 0x1fe, "lower"),
                        new HuffmanLine(1670, 9, 32, 0x1ff), // upper
                        new HuffmanLine(2, 0x1), // OOB
                    };
                    break;
                case 9:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-31, 8, 4, 0xfc),
                        new HuffmanLine(-15, 9, 2, 0x1fc),
                        new HuffmanLine(-11, 8, 2, 0xfd),
                        new HuffmanLine(-7, 9, 1, 0x1fd),
                        new HuffmanLine(-5, 7, 1, 0x7c),
                        new HuffmanLine(-3, 4, 1, 0xa),
                        new HuffmanLine(-1, 3, 1, 0x2),
                        new HuffmanLine(1, 3, 1, 0x3),
                        new HuffmanLine(3, 5, 1, 0x1a),
                        new HuffmanLine(5, 6, 1, 0x3a),
                        new HuffmanLine(7, 3, 5, 0x4),
                        new HuffmanLine(39, 6, 2, 0x3b),
                        new HuffmanLine(43, 4, 5, 0xb),
                        new HuffmanLine(75, 4, 6, 0xc),
                        new HuffmanLine(139, 5, 7, 0x1b),
                        new HuffmanLine(267, 5, 8, 0x1c),
                        new HuffmanLine(523, 6, 8, 0x3c),
                        new HuffmanLine(779, 7, 9, 0x7d),
                        new HuffmanLine(1291, 6, 11, 0x3d),
                        new HuffmanLine(-32, 9, 32, 0x1fe, "lower"),
                        new HuffmanLine(3339, 9, 32, 0x1ff), // upper
                        new HuffmanLine(2, 0x0), // OOB
                    };
                    break;
                case 10:
                    lines = new List<HuffmanLine> {
                      new HuffmanLine(-21, 7, 4, 0x7a),
                        new HuffmanLine(-5, 8, 0, 0xfc),
                        new HuffmanLine(-4, 7, 0, 0x7b),
                        new HuffmanLine(-3, 5, 0, 0x18),
                        new HuffmanLine(-2, 2, 2, 0x0),
                        new HuffmanLine(2, 5, 0, 0x19),
                        new HuffmanLine(3, 6, 0, 0x36),
                        new HuffmanLine(4, 7, 0, 0x7c),
                        new HuffmanLine(5, 8, 0, 0xfd),
                        new HuffmanLine(6, 2, 6, 0x1),
                        new HuffmanLine(70, 5, 5, 0x1a),
                        new HuffmanLine(102, 6, 5, 0x37),
                        new HuffmanLine(134, 6, 6, 0x38),
                        new HuffmanLine(198, 6, 7, 0x39),
                        new HuffmanLine(326, 6, 8, 0x3a),
                        new HuffmanLine(582, 6, 9, 0x3b),
                        new HuffmanLine(1094, 6, 10, 0x3c),
                        new HuffmanLine(2118, 7, 11, 0x7d),
                        new HuffmanLine(-22, 8, 32, 0xfe, "lower"),
                        new HuffmanLine(4166, 8, 32, 0xff), // upper
                        new HuffmanLine(2, 0x2), // OOB
                    };
                    break;
                case 11:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(1, 1, 0, 0x0),
                        new HuffmanLine(2, 2, 1, 0x2),
                        new HuffmanLine(4, 4, 0, 0xc),
                        new HuffmanLine(5, 4, 1, 0xd),
                        new HuffmanLine(7, 5, 1, 0x1c),
                        new HuffmanLine(9, 5, 2, 0x1d),
                        new HuffmanLine(13, 6, 2, 0x3c),
                        new HuffmanLine(17, 7, 2, 0x7a),
                        new HuffmanLine(21, 7, 3, 0x7b),
                        new HuffmanLine(29, 7, 4, 0x7c),
                        new HuffmanLine(45, 7, 5, 0x7d),
                        new HuffmanLine(77, 7, 6, 0x7e),
                        new HuffmanLine(141, 7, 32, 0x7f), // upper
                    };
                    break;
                case 12:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(1, 1, 0, 0x0),
                        new HuffmanLine(2, 2, 0, 0x2),
                        new HuffmanLine(3, 3, 1, 0x6),
                        new HuffmanLine(5, 5, 0, 0x1c),
                        new HuffmanLine(6, 5, 1, 0x1d),
                        new HuffmanLine(8, 6, 1, 0x3c),
                        new HuffmanLine(10, 7, 0, 0x7a),
                        new HuffmanLine(11, 7, 1, 0x7b),
                        new HuffmanLine(13, 7, 2, 0x7c),
                        new HuffmanLine(17, 7, 3, 0x7d),
                        new HuffmanLine(25, 7, 4, 0x7e),
                        new HuffmanLine(41, 8, 5, 0xfe),
                        new HuffmanLine(73, 8, 32, 0xff), // upper
                    };
                    break;
                case 13:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(1, 1, 0, 0x0),
                        new HuffmanLine(2, 3, 0, 0x4),
                        new HuffmanLine(3, 4, 0, 0xc),
                        new HuffmanLine(4, 5, 0, 0x1c),
                        new HuffmanLine(5, 4, 1, 0xd),
                        new HuffmanLine(7, 3, 3, 0x5),
                        new HuffmanLine(15, 6, 1, 0x3a),
                        new HuffmanLine(17, 6, 2, 0x3b),
                        new HuffmanLine(21, 6, 3, 0x3c),
                        new HuffmanLine(29, 6, 4, 0x3d),
                        new HuffmanLine(45, 6, 5, 0x3e),
                        new HuffmanLine(77, 7, 6, 0x7e),
                        new HuffmanLine(141, 7, 32, 0x7f), // upper
                    };
                    break;
                case 14:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-2, 3, 0, 0x4),
                        new HuffmanLine(-1, 3, 0, 0x5),
                        new HuffmanLine(0, 1, 0, 0x0),
                        new HuffmanLine(1, 3, 0, 0x6),
                        new HuffmanLine(2, 3, 0, 0x7),
                    };
                    break;
                case 15:
                    lines = new List<HuffmanLine> {
                        new HuffmanLine(-24, 7, 4, 0x7c),
                        new HuffmanLine(-8, 6, 2, 0x3c),
                        new HuffmanLine(-4, 5, 1, 0x1c),
                        new HuffmanLine(-2, 4, 0, 0xc),
                        new HuffmanLine(-1, 3, 0, 0x4),
                        new HuffmanLine(0, 1, 0, 0x0),
                        new HuffmanLine(1, 3, 0, 0x5),
                        new HuffmanLine(2, 4, 0, 0xd),
                        new HuffmanLine(3, 5, 1, 0x1d),
                        new HuffmanLine(5, 6, 2, 0x3d),
                        new HuffmanLine(9, 7, 4, 0x7d),
                        new HuffmanLine(-25, 7, 32, 0x7e, "lower"),
                        new HuffmanLine(25, 7, 32, 0x7f), // upper
                    };
                    break;
                default:
                    throw new Jbig2Error("standard table B.${number} does not exist");
            }

            table = new HuffmanTable(lines, true);
            standardTablesCache[number] = table;
            return table;
        }



        static HuffmanTable GetCustomHuffmanTable(int index, List<int> referredTo, Dictionary<int, HuffmanTable> customTables)
        {
            // Returns a Tables segment that has been earlier decoded.
            // See 7.4.2.1.6 (symbol dictionary) or 7.4.3.1.6 (text region).
            var currentIndex = 0;
            for (int i = 0, ii = referredTo.Count; i < ii; i++)
            {
                if (customTables.TryGetValue(referredTo[i], out var table))
                {
                    if (index == currentIndex)
                    {
                        return table;
                    }
                    currentIndex++;
                }
            }
            throw new Jbig2Error("can't find custom Huffman table");
        }

        static HuffmanTables GetTextRegionHuffmanTables(TextRegion textRegion, List<int> referredTo, Dictionary<int, HuffmanTable> customTables, int numberOfSymbols, Bytes.Buffer reader)
        {
            // 7.4.3.1.7 Symbol ID Huffman table decoding

            // Read code lengths for RUNCODEs 0...34.
            var codes = new List<HuffmanLine>();
            for (var i = 0; i <= 34; i++)
            {
                var codeLength = (int)reader.ReadBits(4);
                codes.Add(new HuffmanLine(i, codeLength, 0, 0));
            }
            // Assign Huffman codes for RUNCODEs.
            var runCodesTable = new HuffmanTable(codes, false);

            // Read a Huffman code using the assignment above.
            // Interpret the RUNCODE codes and the additional bits (if any).
            codes = new List<HuffmanLine>();
            for (var i = 0; i < numberOfSymbols;)
            {
                var codeLength = (int)runCodesTable.Decode(reader);
                if (codeLength >= 32)
                {
                    int repeatedLength, numberOfRepeats, j;
                    switch (codeLength)
                    {
                        case 32:
                            if (i == 0)
                            {
                                throw new Jbig2Error("no previous value in symbol ID table");
                            }
                            numberOfRepeats = (int)reader.ReadBits(2) + 3;
                            repeatedLength = codes[i - 1].prefixLength;
                            break;
                        case 33:
                            numberOfRepeats = (int)reader.ReadBits(3) + 3;
                            repeatedLength = 0;
                            break;
                        case 34:
                            numberOfRepeats = (int)reader.ReadBits(7) + 11;
                            repeatedLength = 0;
                            break;
                        default:
                            throw new Jbig2Error("invalid code Length in symbol ID table");
                    }
                    for (j = 0; j < numberOfRepeats; j++)
                    {
                        codes.Add(new HuffmanLine(i, repeatedLength, 0, 0));
                        i++;
                    }
                }
                else
                {
                    codes.Add(new HuffmanLine(i, codeLength, 0, 0));
                    i++;
                }
            }
            reader.ByteAlign();
            var symbolIDTable = new HuffmanTable(codes, false);

            // 7.4.3.1.6 Text region segment Huffman table selection

            int customIndex = 0;
            HuffmanTable tableFirstS, tableDeltaS, tableDeltaT;

            switch (textRegion.huffmanFS)
            {
                case 0:
                case 1:
                    tableFirstS = GetStandardTable(textRegion.huffmanFS + 6);
                    break;
                case 3:
                    tableFirstS = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman FS selector");
            }

            switch (textRegion.huffmanDS)
            {
                case 0:
                case 1:
                case 2:
                    tableDeltaS = GetStandardTable(textRegion.huffmanDS + 8);
                    break;
                case 3:
                    tableDeltaS = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DS selector");
            }

            switch (textRegion.huffmanDT)
            {
                case 0:
                case 1:
                case 2:
                    tableDeltaT = GetStandardTable(textRegion.huffmanDT + 11);
                    break;
                case 3:
                    tableDeltaT = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DT selector");
            }

            if (textRegion.refinement)
            {
                // Load tables RDW, RDH, RDX and RDY.
                throw new Jbig2Error("refinement with Huffman is not supported");
            }

            return HuffmanTables.CreateSymbol(symbolIDTable, tableFirstS, tableDeltaS, tableDeltaT);
        }

        static HuffmanTables GetSymbolDictionaryHuffmanTables(SymbolDictionary dictionary, List<int> referredTo, Dictionary<int, HuffmanTable> customTables)
        {
            // 7.4.2.1.6 Symbol dictionary segment Huffman table selection

            int customIndex = 0;
            HuffmanTable tableDeltaHeight, tableDeltaWidth;
            switch (dictionary.huffmanDHSelector)
            {
                case 0:
                case 1:
                    tableDeltaHeight = GetStandardTable(dictionary.huffmanDHSelector + 4);
                    break;
                case 3:
                    tableDeltaHeight = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DH selector");
            }

            switch (dictionary.huffmanDWSelector)
            {
                case 0:
                case 1:
                    tableDeltaWidth = GetStandardTable(dictionary.huffmanDWSelector + 2);
                    break;
                case 3:
                    tableDeltaWidth = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DW selector");
            }

            HuffmanTable tableBitmapSize, tableAggregateInstances;
            if (dictionary.bitmapSizeSelector != 0)
            {
                tableBitmapSize = GetCustomHuffmanTable(customIndex, referredTo, customTables);
                customIndex++;
            }
            else
            {
                tableBitmapSize = GetStandardTable(1);
            }

            if (dictionary.aggregationInstancesSelector != 0)
            {
                tableAggregateInstances = GetCustomHuffmanTable(customIndex, referredTo, customTables);
            }
            else
            {
                tableAggregateInstances = GetStandardTable(1);
            }

            return HuffmanTables.CreateDelta(tableDeltaHeight, tableDeltaWidth, tableBitmapSize, tableAggregateInstances);
        }

        static Dictionary<int, byte[]> ReadUncompressedBitmap(Bytes.Buffer reader, int width, int height)
        {
            var bitmap = new Dictionary<int, byte[]>();
            for (var y = 0; y < height; y++)
            {
                var row = new byte[width];
                bitmap.Add(bitmap.Count, row);
                for (var x = 0; x < width; x++)
                {
                    row[x] = FiltersExtension.ToByte(reader.ReadBit());
                }
                reader.ByteAlign();
            }
            return bitmap;
        }

        static Dictionary<int, byte[]> DecodeMMRBitmap(Bytes.Buffer input, int width, int height, bool endOfBlock)
        {
            // MMR is the same compression algorithm as the PDF filter
            // CCITTFaxDecode with /K -1.
            var paramss = new CCITTFaxParams(K: -1, columns: width, rows: height, blackIs1: true, endOfBlock: endOfBlock);
            var decoder = new CCITTFaxDecoder(input, paramss);
            var bitmap = new Dictionary<int, byte[]>();
            int currentByte = 0;
            var eof = false;

            for (var y = 0; y < height; y++)
            {
                var row = new byte[width];
                bitmap.Add(y, row);
                var shift = -1;
                for (var x = 0; x < width; x++)
                {
                    if (shift < 0)
                    {
                        currentByte = decoder.ReadNextChar();
                        if (currentByte == -1)
                        {
                            // Set the rest of the bits to zero.
                            currentByte = 0;
                            eof = true;
                        }
                        shift = 7;
                    }
                    row[x] = FiltersExtension.ToByte((currentByte >> shift) & 1);
                    shift--;
                }
            }

            if (endOfBlock && !eof)
            {
                // Read until EOFB has been consumed.
                var lookForEOFLimit = 5;
                for (var i = 0; i < lookForEOFLimit; i++)
                {
                    if (decoder.ReadNextChar() == -1)
                    {
                        break;
                    }
                }
            }

            return bitmap;
        }

        // eslint-disable-next-line no-shadow

        public byte[] ParseChunks(List<ImageChunk> chunks)
        {
            return ParseJbig2Chunks(chunks);
        }

        public ImageData Parse(byte[] data)
        {
            var imgData = ParseJbig2(data);
            this.width = imgData.width;
            this.height = imgData.height;
            return imgData;
        }

        internal class HuffmanTables
        {
            internal HuffmanTable tableDeltaHeight;
            internal HuffmanTable tableDeltaWidth;
            internal HuffmanTable tableBitmapSize;
            internal HuffmanTable tableAggregateInstances;
            internal HuffmanTable symbolIDTable;
            internal HuffmanTable tableFirstS;
            internal HuffmanTable tableDeltaS;
            internal HuffmanTable tableDeltaT;

            public static HuffmanTables CreateDelta(HuffmanTable tableDeltaHeight, HuffmanTable tableDeltaWidth, HuffmanTable tableBitmapSize, HuffmanTable tableAggregateInstances)
            {
                return new HuffmanTables
                {
                    tableDeltaHeight = tableDeltaHeight,
                    tableDeltaWidth = tableDeltaWidth,
                    tableBitmapSize = tableBitmapSize,
                    tableAggregateInstances = tableAggregateInstances
                };
            }

            public static HuffmanTables CreateSymbol(HuffmanTable symbolIDTable, HuffmanTable tableFirstS, HuffmanTable tableDeltaS, HuffmanTable tableDeltaT)
            {
                return new HuffmanTables
                {
                    symbolIDTable = symbolIDTable,
                    tableFirstS = tableFirstS,
                    tableDeltaS = tableDeltaS,
                    tableDeltaT = tableDeltaT
                };
            }
        }
    }

    internal class GenericRegion
    {
        internal RegionSegmentInformation info;
        internal bool mmr;
        internal int template;
        internal bool prediction;
        internal List<Point> at;

        public GenericRegion()
        {
        }
    }

    internal class HalftoneRegion
    {
        internal RegionSegmentInformation info;
        internal bool mmr;
        internal int template;
        internal bool enableSkip;
        internal int combinationOperator;
        internal byte defaultPixelValue;
        internal int gridWidth;
        internal int gridHeight;
        internal int gridOffsetX;
        internal int gridOffsetY;
        internal int gridVectorX;
        internal int gridVectorY;

        public HalftoneRegion()
        {
        }
    }

    internal class PatternDictionary
    {
        internal bool mmr;
        internal int template;
        internal byte patternWidth;
        internal byte patternHeight;
        internal int maxPatternIndex;

        public PatternDictionary()
        {
        }
    }

    internal class TextRegion
    {
        internal RegionSegmentInformation info;
        internal bool huffman;
        internal bool refinement;
        internal int logStripSize;
        internal int stripSize;
        internal int referenceCorner;
        internal bool transposed;
        internal int combinationOperator;
        internal byte defaultPixelValue;
        internal int dsOffset;
        internal int refinementTemplate;
        internal int huffmanFS;
        internal int huffmanDS;
        internal int huffmanDT;
        internal int huffmanRefinementDW;
        internal int huffmanRefinementDH;
        internal int huffmanRefinementDX;
        internal int huffmanRefinementDY;
        internal bool huffmanRefinementSizeSelector;
        internal List<Point> refinementAt;
        internal int numberOfSymbolInstances;

        public TextRegion()
        {
        }
    }

    internal class SymbolDictionary
    {
        internal bool huffman;
        internal bool refinement;
        internal int huffmanDHSelector;
        internal int huffmanDWSelector;
        internal int bitmapSizeSelector;
        internal int aggregationInstancesSelector;
        internal bool bitmapCodingContextUsed;
        internal bool bitmapCodingContextRetained;
        internal int template;
        internal int refinementTemplate;
        internal List<Point> at;
        internal List<Point> refinementAt;
        internal int numberOfExportedSymbols;
        internal int numberOfNewSymbols;

        public SymbolDictionary()
        {
        }
    }

    internal class Segment
    {
        internal int start;
        internal SegmentHeader header;
        internal byte[] data;
        internal int end;

        public Segment(SegmentHeader header, byte[] data)
        {
            this.header = header;
            this.data = data;
        }
    }

    internal class PageInformation
    {
        internal uint width;
        internal uint height;
        internal uint resolutionX;
        internal uint resolutionY;
        internal bool lossless;
        internal bool refinement;
        internal int defaultPixelValue;
        internal int combinationOperator;
        internal bool requiresBuffer;
        internal bool combinationOperatorOverride;

        public PageInformation(uint width, uint height, uint resolutionX, uint resolutionY)
        {
            this.width = width;
            this.height = height;
            this.resolutionX = resolutionX;
            this.resolutionY = resolutionY;
        }
    }

    internal class RegionSegmentInformation
    {
        internal uint width;
        internal uint height;
        internal uint x;
        internal uint y;
        internal int combinationOperator;

        public RegionSegmentInformation(uint width, uint height, uint x, uint y, int combinationOperator)
        {
            this.width = width;
            this.height = height;
            this.x = x;
            this.y = y;
            this.combinationOperator = combinationOperator;
        }
    }

    internal class SegmentHeader
    {
        internal int number;
        internal int type;
        internal string typeName;
        internal bool deferredNonRetain;
        internal List<int> referredTo;
        internal int pageAssociation;
        internal uint Length;
        internal List<int> retainBits;
        internal int headerEnd;
        internal bool randomAccess;
        internal int numberOfPages;

        public SegmentHeader()
        {
        }
    }

    internal class ImageData
    {
        internal byte[] imgData;
        internal object width;
        internal object height;

        public ImageData(byte[] imgData, int width, int height)
        {
            this.imgData = imgData;
            this.width = width;
            this.height = height;
        }
    }

    internal class Refinement
    {
        internal Point[] coding;
        internal Point[] reference;

        public Refinement(Point[] coding, Point[] reference)
        {
            this.coding = coding;
            this.reference = reference;
        }
    }

    internal class Point
    {
        internal sbyte x;
        internal sbyte y;

        public Point(sbyte x, sbyte y)
        {
            this.x = x;
            this.y = y;
        }
    }
}


