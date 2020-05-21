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
        Jbig2Error(string msg) : base($"JBIG2 error: {msg}")
        {

        }
    }

    internal class Jbig2Image
    {
        // 7.3 Segment types
        string[] SegmentTypes = new string[]{
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

        List<Point[]> CodingTemplates = new List<Point[]>{
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

        Refinement[] RefinementTemplates = new Refinement[]
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

        // Utility data structures
        internal class ContextCache : Dictionary<string, sbyte[]>
        {

            public sbyte[] getContexts(string id)
            {
                if (TryGetValue(id, out var context))
                    return context;

                return this[id] = new sbyte[1 << 16];
            }
        }

        internal class DecodingContext
        {
            ContextCache cache;
            Decoder decoder;
            byte[] data;
            int start;
            int end;

            public DecodingContext(byte[] data, int start, int end)
            {
                this.data = data;
                this.start = start;
                this.end = end;
            }

            public Decoder Decoder
            {
                get
                {
                    return decoder ?? (decoder = new ArithmeticDecoder(this.data, this.start, this.end));
                }
            }

            public ContextCache ContextCache
            {
                get
                {
                    return cache ?? (cache = new ContextCache());
                }
            }
        }

        // Annex A. Arithmetic Integer Decoding Procedure
        // A.2 Procedure for decoding values
        void decodeInteger(ContextCache contextCache, string procedure, Decoder decoder)
        {
            var contexts = contextCache.getContexts(procedure);
            var prev = 1;

            int readBits(int length)
            {
                var v = 0;
                for (var i = 0; i < length; i++)
                {
                    var bit = decoder.readBit(contexts, prev);
                    prev =
                      prev < 256 ? (prev << 1) | bit : (((prev << 1) | bit) & 511) | 256;
                    v = (v << 1) | bit;
                }
                return (uint)v >> 0;
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
        void decodeIAID(ContextCache contextCache, Decoder decoder, int codeLength)
        {
            var contexts = contextCache.getContexts("IAID");

            var prev = 1;
            for (var i = 0; i < codeLength; i++)
            {
                var bit = decoder.readBit(contexts, prev);
                prev = (prev << 1) | bit;
            }
            if (codeLength < 31)
            {
                return prev & ((1 << codeLength) - 1);
            }
            return prev & 0x7fffffff;
        }



        // See 6.2.5.7 Decoding the bitmap.
        var ReusedContexts = new int[]{
    0x9b25, // 10011 0110010 0101
    0x0795, // 0011 110010 101
    0x00e5, // 001 11001 01
    0x0195, // 011001 0101
  };

        var RefinementReusedContexts = new int[]{
  0x0020, // '000' + '0' (coding) + '00010000' + '0' (reference)
  0x0008, // '0000' + '001000'
};

        void decodeBitmapTemplate0(int width, int height, DecodingContext decodingContext)
        {
            var decoder = decodingContext.Decoder;
            var contexts = decodingContext.ContextCache.getContexts("GB");
            var contextLabel;
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
                    row[j] = pixel = decoder.readBit(contexts, contextLabel);

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
        void decodeBitmap(
          var mmr,
          int width,
          int height,
          int templateIndex,
          var prediction,
          var skip,
          var at,
          DecodingContext decodingContext
        )
        {
            if (mmr)
            {
                var input = new Reader(
                  decodingContext.data,
                  decodingContext.start,
                  decodingContext.end
                );
                return decodeMMRBitmap(input, width, height, false);
            }

            // Use optimized version for the most common case
            if (
              templateIndex == 0 &&
              !skip &&
              !prediction &&
              at.length == 4 &&
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
                return decodeBitmapTemplate0(width, height, decodingContext);
            }

            var useskip = !!skip;
            var template = CodingTemplates[templateIndex].concat(at);

            // Sorting is non-standard, and it is not required. But sorting increases
            // the number of template bits that can be reused from the previous
            // contextLabel in the main loop.
            template.Sort((a, b) =>
            {
                return a.y - b.y || a.x - b.x;
            });

            var templateLength = template.length;
            var templateX = new sbyte[](templateLength);
            var templateY = new sbyte[](templateLength);
            var changingTemplateEntries = new List<>();
            int reuseMask = 0,
              minX = 0,
              maxX = 0,
              minY = 0;
            var c, k;

            for (k = 0; k < templateLength; k++)
            {
                templateX[k] = template[k].x;
                templateY[k] = template[k].y;
                minX = Math.min(minX, template[k].x);
                maxX = Math.max(maxX, template[k].x);
                minY = Math.min(minY, template[k].y);
                // Check if the template pixel appears in two consecutive context labels,
                // so it can be reused. Otherwise, we add it to the list of changing
                // template entries.
                if (
                  k < templateLength - 1 &&
                  template[k].y == template[k + 1].y &&
                  template[k].x == template[k + 1].x - 1
                )
                {
                    reuseMask |= 1 << (templateLength - 1 - k);
                }
                else
                {
                    changingTemplateEntries.Add(k);
                }
            }
            var changingEntriesLength = changingTemplateEntries.Count;

            var changingTemplateX = new sbyte[](changingEntriesLength);
            var changingTemplateY = new sbyte[](changingEntriesLength);
            var changingTemplateBit = new Uint16Array(changingEntriesLength);
            for (c = 0; c < changingEntriesLength; c++)
            {
                k = changingTemplateEntries[c];
                changingTemplateX[c] = template[k].x;
                changingTemplateY[c] = template[k].y;
                changingTemplateBit[c] = 1 << (templateLength - 1 - k);
            }

            // Get the safe bounding box edges from the width, height, minX, maxX, minY
            var sbb_left = -minX;
            var sbb_top = -minY;
            var sbb_right = width - maxX;

            var pseudoPixelContext = ReusedContexts[templateIndex];
            var row = new byte[](width);
            var bitmap = List<byte[]>();

            var decoder = decodingContext.decoder;
            var contexts = decodingContext.contextCache.getContexts("GB");

            int ltp = 0,
              j,
              i0,
              j0,
              contextLabel = 0,
              bit,
              shift;
            for (var i = 0; i < height; i++)
            {
                if (prediction)
                {
                    var sltp = decoder.readBit(contexts, pseudoPixelContext);
                    ltp ^= sltp;
                    if (ltp)
                    {
                        bitmap.Add(row); // duplicate previous row
                        continue;
                    }
                }
                row = new byte[](row);
                bitmap.Add(row);
                for (j = 0; j < width; j++)
                {
                    if (useskip && skip[i][j])
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
                            bit = bitmap[i0][j0];
                            if (bit)
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
                                    bit = bitmap[i0][j0];
                                    if (bit)
                                    {
                                        contextLabel |= bit << shift;
                                    }
                                }
                            }
                        }
                    }
                    var pixel = decoder.readBit(contexts, contextLabel);
                    row[j] = pixel;
                }
            }
            return bitmap;
        }

        // 6.3.2 Generic Refinement Region Decoding Procedure
        void decodeRefinement(int width, int height, int templateIndex,
          var referenceBitmap, int offsetX, int offsetY,
          var prediction, var at, var decodingContext)
        {
            var codingTemplate = RefinementTemplates[templateIndex].coding;
            if (templateIndex == 0)
            {
                codingTemplate = codingTemplate.concat(new[] { at[0] });
            }
            var codingTemplateLength = codingTemplate.length;
            var codingTemplateX = new Int32Array(codingTemplateLength);
            var codingTemplateY = new Int32Array(codingTemplateLength);
            var k;
            for (k = 0; k < codingTemplateLength; k++)
            {
                codingTemplateX[k] = codingTemplate[k].x;
                codingTemplateY[k] = codingTemplate[k].y;
            }

            var referenceTemplate = RefinementTemplates[templateIndex].reference;
            if (templateIndex == 0)
            {
                referenceTemplate = referenceTemplate.concat(new[] { at[1] });
            }
            var referenceTemplateLength = referenceTemplate.length;
            var referenceTemplateX = new Int32Array(referenceTemplateLength);
            var referenceTemplateY = new Int32Array(referenceTemplateLength);
            for (k = 0; k < referenceTemplateLength; k++)
            {
                referenceTemplateX[k] = referenceTemplate[k].x;
                referenceTemplateY[k] = referenceTemplate[k].y;
            }
            var referenceWidth = referenceBitmap[0].length;
            var referenceHeight = referenceBitmap.length;

            var pseudoPixelContext = RefinementReusedContexts[templateIndex];
            var bitmap = new List<>();

            var decoder = decodingContext.decoder;
            var contexts = decodingContext.contextCache.getContexts("GR");

            var ltp = 0;
            for (var i = 0; i < height; i++)
            {
                if (prediction)
                {
                    var sltp = decoder.readBit(contexts, pseudoPixelContext);
                    ltp ^= sltp;
                    if (ltp)
                    {
                        throw new Jbig2Error("prediction is not supported");
                    }
                }
                var row = new byte[](width);
                bitmap.Add(row);
                for (var j = 0; j < width; j++)
                {
                    var i0, j0;
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
                    var pixel = decoder.readBit(contexts, contextLabel);
                    row[j] = pixel;
                }
            }

            return bitmap;
        }

        // 6.5.5 Decoding the symbol dictionary
        void decodeSymbolDictionary(
          var huffman,
          var refinement,
          var symbols,
          var numberOfNewSymbols,
          var numberOfExportedSymbols,
          var huffmanTables,
          var templateIndex,
          var at,
          var refinementTemplateIndex,
          var refinementAt,
          var decodingContext,
          var huffmanInput)
        {
            if (huffman && refinement)
            {
                throw new Jbig2Error("symbol refinement with Huffman is not supported");
            }

            var newSymbols = new List<>();
            var currentHeight = 0;
            var symbolCodeLength = log2(symbols.length + numberOfNewSymbols);

            var decoder = decodingContext.decoder;
            var contextCache = decodingContext.contextCache;
            var tableB1, symbolWidths;
            if (huffman)
            {
                tableB1 = getStandardTable(1); // standard table B.1
                symbolWidths = new List<>();
                symbolCodeLength = Math.max(symbolCodeLength, 1); // 6.5.8.2.3
            }

            while (newSymbols.length < numberOfNewSymbols)
            {
                var deltaHeight = huffman
                  ? huffmanTables.tableDeltaHeight.decode(huffmanInput)
                  : decodeInteger(contextCache, "IADH", decoder); // 6.5.6
                currentHeight += deltaHeight;
                var currentWidth = 0,
                  totalWidth = 0;
                var firstSymbol = huffman ? symbolWidths.length : 0;
                while (true)
                {
                    var deltaWidth = huffman
                      ? huffmanTables.tableDeltaWidth.decode(huffmanInput)
                      : decodeInteger(contextCache, "IADW", decoder); // 6.5.7
                    if (deltaWidth == null)
                    {
                        break; // OOB
                    }
                    currentWidth += deltaWidth;
                    totalWidth += currentWidth;
                    var bitmap;
                    if (refinement)
                    {
                        // 6.5.8.2 Refinement/aggregate-coded symbol bitmap
                        var numberOfInstances = decodeInteger(contextCache, "IAAI", decoder);
                        if (numberOfInstances > 1)
                        {
                            bitmap = decodeTextRegion(
                              huffman,
                              refinement,
                              currentWidth,
                              currentHeight,
                              0,
                              numberOfInstances,
                              1, // strip size
                              symbols.concat(newSymbols),
                              symbolCodeLength,
                              0, // transposed
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
                            var symbolId = decodeIAID(contextCache, decoder, symbolCodeLength);
                            var rdx = decodeInteger(contextCache, "IARDX", decoder); // 6.4.11.3
                            var rdy = decodeInteger(contextCache, "IARDY", decoder); // 6.4.11.4
                            var symbol =
                              symbolId < symbols.length
                                ? symbols[symbolId]
                                : newSymbols[symbolId - symbols.length];
                            bitmap = decodeRefinement(
                              currentWidth,
                              currentHeight,
                              refinementTemplateIndex,
                              symbol,
                              rdx,
                              rdy,
                              false,
                              refinementAt,
                              decodingContext
                            );
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
                        bitmap = decodeBitmap(
                          false,
                          currentWidth,
                          currentHeight,
                          templateIndex,
                          false,
                          null,
                          at,
                          decodingContext
                        );
                        newSymbols.Add(bitmap);
                    }
                }
                if (huffman && !refinement)
                {
                    // 6.5.9 Height class collective bitmap
                    var bitmapSize = huffmanTables.tableBitmapSize.decode(huffmanInput);
                    huffmanInput.byteAlign();
                    var collectiveBitmap;
                    if (bitmapSize == 0)
                    {
                        // Uncompressed collective bitmap
                        collectiveBitmap = readUncompressedBitmap(
                          huffmanInput,
                          totalWidth,
                          currentHeight
                        );
                    }
                    else
                    {
                        // MMR collective bitmap
                        var originalEnd = huffmanInput.end;
                        var bitmapEnd = huffmanInput.position + bitmapSize;
                        huffmanInput.end = bitmapEnd;
                        collectiveBitmap = decodeMMRBitmap(
                          huffmanInput,
                          totalWidth,
                          currentHeight,
                          false
                        );
                        huffmanInput.end = originalEnd;
                        huffmanInput.position = bitmapEnd;
                    }
                    var numberOfSymbolsDecoded = symbolWidths.length;
                    if (firstSymbol == numberOfSymbolsDecoded - 1)
                    {
                        // collectiveBitmap is a single symbol.
                        newSymbols.Add(collectiveBitmap);
                    }
                    else
                    {
                        // Divide collectiveBitmap into symbols.
                        var i,
                          y,
                          xMin = 0,
                          xMax,
                          bitmapWidth,
                          symbolBitmap;
                        for (i = firstSymbol; i < numberOfSymbolsDecoded; i++)
                        {
                            bitmapWidth = symbolWidths[i];
                            xMax = xMin + bitmapWidth;
                            symbolBitmap = new List<>();
                            for (y = 0; y < currentHeight; y++)
                            {
                                symbolBitmap.Add(collectiveBitmap[y].subarray(xMin, xMax));
                            }
                            newSymbols.Add(symbolBitmap);
                            xMin = xMax;
                        }
                    }
                }
            }

            // 6.5.10 Exported symbols
            var exportedSymbols = new List<>();
            var flags = new List<>();
            var currentFlag = false;
            var totalSymbolsLength = symbols.length + numberOfNewSymbols;
            while (flags.length < totalSymbolsLength)
            {
                var runLength = huffman
                  ? tableB1.decode(huffmanInput)
                  : decodeInteger(contextCache, "IAEX", decoder);
                while (runLength--)
                {
                    flags.Add(currentFlag);
                }
                currentFlag = !currentFlag;
            }
            for (var i = 0, ii = symbols.length; i < ii; i++)
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

        void decodeTextRegion(
          var huffman,
          var refinement,
          var width,
          var height,
          var defaultPixelValue,
          var numberOfSymbolInstances,
          var stripSize,
          var inputSymbols,
          var symbolCodeLength,
          var transposed,
          var dsOffset,
          var referenceCorner,
          var combinationOperator,
          var huffmanTables,
          var refinementTemplateIndex,
          var refinementAt,
          var decodingContext,
          var logStripSize,
          var huffmanInput
        )
        {
            if (huffman && refinement)
            {
                throw new Jbig2Error("refinement with Huffman is not supported");
            }

            // Prepare bitmap
            var bitmap = new List<>();
            var i, row;
            for (i = 0; i < height; i++)
            {
                row = new byte[](width);
                if (defaultPixelValue)
                {
                    for (var j = 0; j < width; j++)
                    {
                        row[j] = defaultPixelValue;
                    }
                }
                bitmap.Add(row);
            }

            var decoder = decodingContext.decoder;
            var contextCache = decodingContext.contextCache;

            var stripT = huffman
              ? -huffmanTables.tableDeltaT.decode(huffmanInput)
              : -decodeInteger(contextCache, "IADT", decoder); // 6.4.6
            var firstS = 0;
            i = 0;
            while (i < numberOfSymbolInstances)
            {
                var deltaT = huffman
                  ? huffmanTables.tableDeltaT.decode(huffmanInput)
                  : decodeInteger(contextCache, "IADT", decoder); // 6.4.6
                stripT += deltaT;

                var deltaFirstS = huffman
                  ? huffmanTables.tableFirstS.decode(huffmanInput)
                  : decodeInteger(contextCache, "IAFS", decoder); // 6.4.7
                firstS += deltaFirstS;
                var currentS = firstS;
                do
                {
                    var currentT = 0; // 6.4.9
                    if (stripSize > 1)
                    {
                        currentT = huffman
                          ? huffmanInput.readBits(logStripSize)
                          : decodeInteger(contextCache, "IAIT", decoder);
                    }
                    var t = stripSize * stripT + currentT;
                    var symbolId = huffman
                      ? huffmanTables.symbolIDTable.decode(huffmanInput)
                      : decodeIAID(contextCache, decoder, symbolCodeLength);
                    var applyRefinement =
                      refinement &&
                      (huffman
                        ? huffmanInput.readBit()
                        : decodeInteger(contextCache, "IARI", decoder));
                    var symbolBitmap = inputSymbols[symbolId];
                    var symbolWidth = symbolBitmap[0].length;
                    var symbolHeight = symbolBitmap.length;
                    if (applyRefinement)
                    {
                        var rdw = decodeInteger(contextCache, "IARDW", decoder); // 6.4.11.1
                        var rdh = decodeInteger(contextCache, "IARDH", decoder); // 6.4.11.2
                        var rdx = decodeInteger(contextCache, "IARDX", decoder); // 6.4.11.3
                        var rdy = decodeInteger(contextCache, "IARDY", decoder); // 6.4.11.4
                        symbolWidth += rdw;
                        symbolHeight += rdh;
                        symbolBitmap = decodeRefinement(
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
                    var offsetT = t - (referenceCorner & 1 ? 0 : symbolHeight - 1);
                    var offsetS = currentS - (referenceCorner & 2 ? symbolWidth - 1 : 0);
                    var s2, t2, symbolRow;
                    if (transposed)
                    {
                        // Place Symbol Bitmap from T1,S1
                        for (s2 = 0; s2 < symbolHeight; s2++)
                        {
                            row = bitmap[offsetS + s2];
                            if (!row)
                            {
                                continue;
                            }
                            symbolRow = symbolBitmap[s2];
                            // To ignore Parts of Symbol bitmap which goes
                            // outside bitmap region
                            var maxWidth = Math.min(width - offsetT, symbolWidth);
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
                            row = bitmap[offsetT + t2];
                            if (!row)
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
                      ? huffmanTables.tableDeltaS.decode(huffmanInput)
                      : decodeInteger(contextCache, "IADS", decoder); // 6.4.8
                    if (deltaS == null)
                    {
                        break; // OOB
                    }
                    currentS += deltaS + dsOffset;
                } while (true);
            }
            return bitmap;
        }

        void decodePatternDictionary(
          var mmr,
          int patternWidth,
          int patternHeight,
          int maxPatternIndex,
          var template,
          DecodingContext decodingContext
        )
        {
            var at = new List<>();
            if (!mmr)
            {
                at.Add(new Point(x: -patternWidth, y: 0));
                if (template == 0)
                {
                    at.Add(new Point(x: -3, y: -1));
                    at.Add(new Point(x: 2, y: -2));
                    at.Add(new Point(x: -2, y: -2));
                }
            }
            var collectiveWidth = (maxPatternIndex + 1) * patternWidth;
            var collectiveBitmap = decodeBitmap(
              mmr,
              collectiveWidth,
              patternHeight,
              template,
              false,
              null,
              at,
              decodingContext
            );
            // Divide collective bitmap into patterns.
            var patterns = new List<>();
            for (var i = 0; i <= maxPatternIndex; i++)
            {
                var patternBitmap = new List<>();
                var xMin = patternWidth * i;
                var xMax = xMin + patternWidth;
                for (var y = 0; y < patternHeight; y++)
                {
                    patternBitmap.Add(collectiveBitmap[y].subarray(xMin, xMax));
                }
                patterns.Add(patternBitmap);
            }
            return patterns;
        }

        void decodeHalftoneRegion(
          int mmr,
          int patterns,
          int template,
          int regionWidth,
          int regionHeight,
          int defaultPixelValue,
          int enableSkip,
          int combinationOperator,
          int gridWidth,
          int gridHeight,
          int gridOffsetX,
          int gridOffsetY,
          int gridVectorX,
          int gridVectorY,
          int decodingContext
        )
        {
            var skip = null;
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
            var regionBitmap = new List<>();
            var i, j, row;
            for (i = 0; i < regionHeight; i++)
            {
                row = new byte[regionWidth];
                if (defaultPixelValue)
                {
                    for (j = 0; j < regionWidth; j++)
                    {
                        row[j] = defaultPixelValue;
                    }
                }
                regionBitmap.Add(row);
            }

            var numberOfPatterns = patterns.length;
            var pattern0 = patterns[0];
            var patternWidth = pattern0[0].length,
              patternHeight = pattern0.length;
            var bitsPerValue = log2(numberOfPatterns);
            var at = new List<>();
            if (mmr == 0)
            {
                at.Add(new Point(x: template <= 1 ? 3 : 2, y: -1));
                if (template == 0)
                {
                    at.Add(new Point(x: -3, y: -1));
                    at.Add(new Point(x: 2, y: -2));
                    at.Add(new Point(x: -2, y: -2));
                }
            }
            // Annex C. Gray-scale Image Decoding Procedure.
            var grayScaleBitPlanes = new List<>();
            var mmrInput, bitmap;
            if (mmr != 0)
            {
                // MMR bit planes are in one continuous stream. Only EOFB codes indicate
                // the end of each bitmap, so EOFBs must be decoded.
                mmrInput = new Reader(
                  decodingContext.data,
                  decodingContext.start,
                  decodingContext.end
                );
            }
            for (i = bitsPerValue - 1; i >= 0; i--)
            {
                if (mmr)
                {
                    bitmap = decodeMMRBitmap(mmrInput, gridWidth, gridHeight, true);
                }
                else
                {
                    bitmap = decodeBitmap(
                      false,
                      gridWidth,
                      gridHeight,
                      template,
                      false,
                      skip,
                      at,
                      decodingContext
                    );
                }
                grayScaleBitPlanes[i] = bitmap;
            }
            // 6.6.5.2 Rendering the patterns.
            var mg, ng, bit, patternIndex, patternBitmap, x, y, patternRow, regionRow;
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
                    if (
                      x >= 0 &&
                      x + patternWidth <= regionWidth &&
                      y >= 0 &&
                      y + patternHeight <= regionHeight
                    )
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
                        var regionX, regionY;
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

        void readSegmentHeader(byte[] data, int start)
        {
            var segmentHeader = { };
            segmentHeader.number = readUint32(data, start);
            var flags = data[start + 4];
            var segmentType = flags & 0x3f;
            if (!SegmentTypes[segmentType])
            {
                throw new Jbig2Error("invalid segment type: " + segmentType);
            }
            segmentHeader.type = segmentType;
            segmentHeader.typeName = SegmentTypes[segmentType];
            segmentHeader.deferredNonRetain = !!(flags & 0x80);

            var pageAssociationFieldSize = !!(flags & 0x40);
            var referredFlags = data[start + 5];
            var referredToCount = (referredFlags >> 5) & 7;
            var retainBits = new[] { referredFlags & 31 };
            var position = start + 6;
            if (referredFlags == 7)
            {
                referredToCount = readUint32(data, position - 1) & 0x1fffffff;
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
            var referredTo = new List<>();
            var i, ii;
            for (i = 0; i < referredToCount; i++)
            {
                var number;
                if (referredToSegmentNumberSize == 1)
                {
                    number = data[position];
                }
                else if (referredToSegmentNumberSize == 2)
                {
                    number = readUint16(data, position);
                }
                else
                {
                    number = readUint32(data, position);
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
                segmentHeader.pageAssociation = readUint32(data, position);
                position += 4;
            }
            segmentHeader.length = readUint32(data, position);
            position += 4;

            if (segmentHeader.length == 0xffffffff)
            {
                // 7.2.7 Segment data length, unknown segment length
                if (segmentType == 38)
                {
                    // ImmediateGenericRegion
                    var genericRegionInfo = readRegionSegmentInformation(data, position);
                    var genericRegionSegmentFlags =
                      data[position + RegionSegmentInformationFieldLength];
                    var genericRegionMmr = !!(genericRegionSegmentFlags & 1);
                    // searching for the segment end
                    var searchPatternLength = 6;
                    var searchPattern = new byte[](searchPatternLength);
                    if (!genericRegionMmr)
                    {
                        searchPattern[0] = 0xff;
                        searchPattern[1] = 0xac;
                    }
                    searchPattern[2] = ((uint)genericRegionInfo.height >> 24) & 0xff;
                    searchPattern[3] = (genericRegionInfo.height >> 16) & 0xff;
                    searchPattern[4] = (genericRegionInfo.height >> 8) & 0xff;
                    searchPattern[5] = genericRegionInfo.height & 0xff;
                    for (i = position, ii = data.length; i < ii; i++)
                    {
                        var j = 0;
                        while (j < searchPatternLength && searchPattern[j] == data[i + j])
                        {
                            j++;
                        }
                        if (j == searchPatternLength)
                        {
                            segmentHeader.length = i + searchPatternLength;
                            break;
                        }
                    }
                    if (segmentHeader.length == 0xffffffff)
                    {
                        throw new Jbig2Error("segment end was not found");
                    }
                }
                else
                {
                    throw new Jbig2Error("invalid unknown segment length");
                }
            }
            segmentHeader.headerEnd = position;
            return segmentHeader;
        }

        void readSegments(var header, byte[] data, int start, int end)
        {
            var segments = new List<Segment>();
            var position = start;
            while (position < end)
            {
                var segmentHeader = readSegmentHeader(data, position);
                position = segmentHeader.headerEnd;
                var segment = new Segment(header: segmentHeader, data);
                if (!header.randomAccess)
                {
                    segment.start = position;
                    position += segmentHeader.length;
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
                for (var i = 0, ii = segments.length; i < ii; i++)
                {
                    segments[i].start = position;
                    position += segments[i].header.length;
                    segments[i].end = position;
                }
            }
            return segments;
        }

        // 7.4.1 Region segment information field
        void readRegionSegmentInformation(byte[] data, int start)
        {
            return new RegionSegmentInformation(
                width: data.ReadUint32(start),
                height: data.ReadUint32(start + 4),
                x: data.ReadUint32(start + 8),
                y: data.ReadUint32(start + 12),
                combinationOperator: data[start + 16] & 7
                );
        }
        var RegionSegmentInformationFieldLength = 17;

        void processSegment(Segment segment, var visitor)
        {
            var header = segment.header;

            var data = segment.data,
              position = segment.start,
              end = segment.end;
            var args, at, i, atLength;
            switch (header.type)
            {
                case 0: // SymbolDictionary
                        // 7.4.2 Symbol dictionary segment syntax
                    var dictionary = { };
                    var dictionaryFlags = readUint16(data, position); // 7.4.2.1.1
                    dictionary.huffman = !!(dictionaryFlags & 1);
                    dictionary.refinement = !!(dictionaryFlags & 2);
                    dictionary.huffmanDHSelector = (dictionaryFlags >> 2) & 3;
                    dictionary.huffmanDWSelector = (dictionaryFlags >> 4) & 3;
                    dictionary.bitmapSizeSelector = (dictionaryFlags >> 6) & 1;
                    dictionary.aggregationInstancesSelector = (dictionaryFlags >> 7) & 1;
                    dictionary.bitmapCodingContextUsed = !!(dictionaryFlags & 256);
                    dictionary.bitmapCodingContextRetained = !!(dictionaryFlags & 512);
                    dictionary.template = (dictionaryFlags >> 10) & 3;
                    dictionary.refinementTemplate = (dictionaryFlags >> 12) & 1;
                    position += 2;
                    if (!dictionary.huffman)
                    {
                        atLength = dictionary.template == 0 ? 4 : 1;
                        at = new List<>();
                        for (i = 0; i < atLength; i++)
                        {
                            at.Add(new Point(x: readInt8(data, position), y: readInt8(data, position + 1)));
                            position += 2;
                        }
                        dictionary.at = at;
                    }
                    if (dictionary.refinement && !dictionary.refinementTemplate)
                    {
                        at = new List<>();
                        for (i = 0; i < 2; i++)
                        {
                            at.Add(new Point(x: readInt8(data, position), y: readInt8(data, position + 1)));
                            position += 2;
                        }
                        dictionary.refinementAt = at;
                    }
                    dictionary.numberOfExportedSymbols = readUint32(data, position);
                    position += 4;
                    dictionary.numberOfNewSymbols = readUint32(data, position);
                    position += 4;
                    args = new[] {
                      dictionary,
                      header.number,
                      header.referredTo,
                      data,
                      position,
                      end
                    };
                    break;
                case 6: // ImmediateTextRegion
                case 7: // ImmediateLosslessTextRegion
                    var textRegion = { };
                    textRegion.info = readRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var textRegionSegmentFlags = readUint16(data, position);
                    position += 2;
                    textRegion.huffman = !!(textRegionSegmentFlags & 1);
                    textRegion.refinement = !!(textRegionSegmentFlags & 2);
                    textRegion.logStripSize = (textRegionSegmentFlags >> 2) & 3;
                    textRegion.stripSize = 1 << textRegion.logStripSize;
                    textRegion.referenceCorner = (textRegionSegmentFlags >> 4) & 3;
                    textRegion.transposed = !!(textRegionSegmentFlags & 64);
                    textRegion.combinationOperator = (textRegionSegmentFlags >> 7) & 3;
                    textRegion.defaultPixelValue = (textRegionSegmentFlags >> 9) & 1;
                    textRegion.dsOffset = (textRegionSegmentFlags << 17) >> 27;
                    textRegion.refinementTemplate = (textRegionSegmentFlags >> 15) & 1;
                    if (textRegion.huffman)
                    {
                        var textRegionHuffmanFlags = readUint16(data, position);
                        position += 2;
                        textRegion.huffmanFS = textRegionHuffmanFlags & 3;
                        textRegion.huffmanDS = (textRegionHuffmanFlags >> 2) & 3;
                        textRegion.huffmanDT = (textRegionHuffmanFlags >> 4) & 3;
                        textRegion.huffmanRefinementDW = (textRegionHuffmanFlags >> 6) & 3;
                        textRegion.huffmanRefinementDH = (textRegionHuffmanFlags >> 8) & 3;
                        textRegion.huffmanRefinementDX = (textRegionHuffmanFlags >> 10) & 3;
                        textRegion.huffmanRefinementDY = (textRegionHuffmanFlags >> 12) & 3;
                        textRegion.huffmanRefinementSizeSelector = !!(
                          textRegionHuffmanFlags & 0x4000
                        );
                    }
                    if (textRegion.refinement && !textRegion.refinementTemplate)
                    {
                        at = new List<>();
                        for (i = 0; i < 2; i++)
                        {
                            at.Add(new Point(x: readInt8(data, position), y: readInt8(data, position + 1)));
                            position += 2;
                        }
                        textRegion.refinementAt = at;
                    }
                    textRegion.numberOfSymbolInstances = readUint32(data, position);
                    position += 4;
                    args = new[] { textRegion, header.referredTo, data, position, end };
                    break;
                case 16: // PatternDictionary
                         // 7.4.4. Pattern dictionary segment syntax
                    var patternDictionary = { };
                    var patternDictionaryFlags = data[position++];
                    patternDictionary.mmr = !!(patternDictionaryFlags & 1);
                    patternDictionary.template = (patternDictionaryFlags >> 1) & 3;
                    patternDictionary.patternWidth = data[position++];
                    patternDictionary.patternHeight = data[position++];
                    patternDictionary.maxPatternIndex = readUint32(data, position);
                    position += 4;
                    args = new[] { patternDictionary, header.number, data, position, end };
                    break;
                case 22: // ImmediateHalftoneRegion
                case 23: // ImmediateLosslessHalftoneRegion
                         // 7.4.5 Halftone region segment syntax
                    var halftoneRegion = { };
                    halftoneRegion.info = readRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var halftoneRegionFlags = data[position++];
                    halftoneRegion.mmr = !!(halftoneRegionFlags & 1);
                    halftoneRegion.template = (halftoneRegionFlags >> 1) & 3;
                    halftoneRegion.enableSkip = !!(halftoneRegionFlags & 8);
                    halftoneRegion.combinationOperator = (halftoneRegionFlags >> 4) & 7;
                    halftoneRegion.defaultPixelValue = (halftoneRegionFlags >> 7) & 1;
                    halftoneRegion.gridWidth = readUint32(data, position);
                    position += 4;
                    halftoneRegion.gridHeight = readUint32(data, position);
                    position += 4;
                    halftoneRegion.gridOffsetX = readUint32(data, position) & 0xffffffff;
                    position += 4;
                    halftoneRegion.gridOffsetY = readUint32(data, position) & 0xffffffff;
                    position += 4;
                    halftoneRegion.gridVectorX = readUint16(data, position);
                    position += 2;
                    halftoneRegion.gridVectorY = readUint16(data, position);
                    position += 2;
                    args = new[] { halftoneRegion, header.referredTo, data, position, end };
                    break;
                case 38: // ImmediateGenericRegion
                case 39: // ImmediateLosslessGenericRegion
                    var genericRegion = { };
                    genericRegion.info = readRegionSegmentInformation(data, position);
                    position += RegionSegmentInformationFieldLength;
                    var genericRegionSegmentFlags = data[position++];
                    genericRegion.mmr = !!(genericRegionSegmentFlags & 1);
                    genericRegion.template = (genericRegionSegmentFlags >> 1) & 3;
                    genericRegion.prediction = !!(genericRegionSegmentFlags & 8);
                    if (!genericRegion.mmr)
                    {
                        atLength = genericRegion.template == 0 ? 4 : 1;
                        at = new List<>();
                        for (i = 0; i < atLength; i++)
                        {
                            at.Add(new Point(x: readInt8(data, position), y: readInt8(data, position + 1)));
                            position += 2;
                        }
                        genericRegion.at = at;
                    }
                    args = new[] { genericRegion, data, position, end };
                    break;
                case 48: // PageInformation
                    var pageInfo = new PageInformation(
                        width: readUint32(data, position),
                        height: readUint32(data, position + 4),
                        resolutionX: readUint32(data, position + 8),
                        resolutionY: readUint32(data, position + 12));
                    if (pageInfo.height == 0xffffffff)
                    {
                        pageInfo.height = 0;
                        //????delete pageInfo.height;
                    }
                    var pageSegmentFlags = data[position + 16];
                    readUint16(data, position + 17); // pageStripingInformation
                    pageInfo.lossless = !!(pageSegmentFlags & 1);
                    pageInfo.refinement = !!(pageSegmentFlags & 2);
                    pageInfo.defaultPixelValue = (pageSegmentFlags >> 2) & 1;
                    pageInfo.combinationOperator = (pageSegmentFlags >> 3) & 3;
                    pageInfo.requiresBuffer = !!(pageSegmentFlags & 32);
                    pageInfo.combinationOperatorOverride = !!(pageSegmentFlags & 64);
                    args = new[] { pageInfo };
                    break;
                case 49: // EndOfPage
                    break;
                case 50: // EndOfStripe
                    break;
                case 51: // EndOfFile
                    break;
                case 53: // Tables
                    args = new[] { header.number, data, position, end };
                    break;
                case 62: // 7.4.15 defines 2 extension types which
                         // are comments and can be ignored.
                    break;
                default:
                    throw new Jbig2Error(
                      "segment type ${header.typeName}(${header.type})" +
                        " is not implemented"
                    );
            }
            var callbackName = "on" + header.typeName;
            if (callbackName is visitor)
            {
                visitor[callbackName].apply(visitor, args);
            }
        }

        void processSegments(List<Segment> segments, var visitor)
        {
            for (var i = 0, ii = segments.length; i < ii; i++)
            {
                processSegment(segments[i], visitor);
            }
        }

        void parseJbig2Chunks(var chunks)
        {
            var visitor = new SimpleSegmentVisitor();
            for (var i = 0, ii = chunks.length; i < ii; i++)
            {
                var chunk = chunks[i];
                var segments = readSegments(new var(), chunk.data, chunk.start, chunk.end);
                processSegments(segments, visitor);
            }
            return visitor.buffer;
        }

        ImageData parseJbig2(byte[] data)
        {
            var end = data.length;
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

            var header = Object.create(null);
            position += 8;
            var flags = data[position++];
            header.randomAccess = !(flags & 1);
            if (!(flags & 2))
            {
                header.numberOfPages = data.readUint32(position);
                position += 4;
            }

            var segments = readSegments(header, data, position, end);
            var visitor = new SimpleSegmentVisitor();
            processSegments(segments, visitor);

            var width = visitor.currentPageInfo.width;
            var height = visitor.currentPageInfo.height;
            var bitPacked = visitor.buffer;
            var imgData = new Uint8ClampedArray(width * height);
            var q = 0,
              k = 0;
            for (var i = 0; i < height; i++)
            {
                var mask = 0,
                  buffer;
                for (var j = 0; j < width; j++)
                {
                    if (!mask)
                    {
                        mask = 128;
                        buffer = bitPacked[k++];
                    }
                    imgData[q++] = buffer & mask ? 0 : 255;
                    mask >>= 1;
                }
            }

            return new ImageData(imgData, width, height);
        }

        internal class SimpleSegmentVisitor
        {
            void onPageInformation(var info)
            {
                this.currentPageInfo = info;
                var rowSize = (info.width + 7) >> 3;
                var buffer = new Uint8ClampedArray(rowSize * info.height);
                // The contents of ArrayBuffers are initialized to 0.
                // Fill the buffer with 0xFF only if info.defaultPixelValue is set
                if (info.defaultPixelValue)
                {
                    for (var i = 0, ii = buffer.length; i < ii; i++)
                    {
                        buffer[i] = 0xff;
                    }
                }
                this.buffer = buffer;
            }
            void drawBitmap(var regionInfo, var bitmap)
            {
                var pageInfo = this.currentPageInfo;
                var width = regionInfo.width,
                  height = regionInfo.height;
                var rowSize = (pageInfo.width + 7) >> 3;
                var combinationOperator = pageInfo.combinationOperatorOverride
                  ? regionInfo.combinationOperator
                  : pageInfo.combinationOperator;
                var buffer = this.buffer;
                var mask0 = 128 >> (regionInfo.x & 7);
                var offset0 = regionInfo.y * rowSize + (regionInfo.x >> 3);
                var i, j, mask, offset;
                switch (combinationOperator)
                {
                    case 0: // OR
                        for (i = 0; i < height; i++)
                        {
                            mask = mask0;
                            offset = offset0;
                            for (j = 0; j < width; j++)
                            {
                                if (bitmap[i][j])
                                {
                                    buffer[offset] |= mask;
                                }
                                mask >>= 1;
                                if (!mask)
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
                            for (j = 0; j < width; j++)
                            {
                                if (bitmap[i][j])
                                {
                                    buffer[offset] ^= mask;
                                }
                                mask >>= 1;
                                if (!mask)
                                {
                                    mask = 128;
                                    offset++;
                                }
                            }
                            offset0 += rowSize;
                        }
                        break;
                    default:
                        throw new Jbig2Error(
                          "operator ${combinationOperator} is not supported"
                        );
                }
            }

            void onImmediateGenericRegion(var region, var data, var start, var end)
            {
                var regionInfo = region.info;
                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = decodeBitmap(
                  region.mmr,
                  regionInfo.width,
                  regionInfo.height,
                  region.template,
                  region.prediction,
                  null,
                  region.at,
                  decodingContext
                );
                this.drawBitmap(regionInfo, bitmap);
            }

            void onImmediateLosslessGenericRegion()
            {
                this.onImmediateGenericRegion.apply(this, arguments);
            }

            void onSymbolDictionary(var dictionary, var currentSegment, var referredSegments, var data, var start, var end)
            {
                var huffmanTables, huffmanInput;
                if (dictionary.huffman)
                {
                    huffmanTables = getSymbolDictionaryHuffmanTables(
                      dictionary,
                      referredSegments,
                      this.customTables
                    );
                    huffmanInput = new Reader(data, start, end);
                }

                // Combines exported symbols from all referred segments
                var symbols = this.symbols;
                if (!symbols)
                {
                    this.symbols = symbols = new Dictionary<int, var>();
                }

                var inputSymbols = new List<>();
                for (var i = 0, ii = referredSegments.length; i < ii; i++)
                {
                    var referredSymbols = symbols[referredSegments[i]];
                    // referredSymbols is undefined when we have a reference to a Tables
                    // segment instead of a SymbolDictionary.
                    if (referredSymbols)
                    {
                        inputSymbols = inputSymbols.concat(referredSymbols);
                    }
                }

                var decodingContext = new DecodingContext(data, start, end);
                symbols[currentSegment] = decodeSymbolDictionary(dictionary.huffman, dictionary.refinement, inputSymbols,
                  dictionary.numberOfNewSymbols, dictionary.numberOfExportedSymbols, huffmanTables, dictionary.template,
                  dictionary.at, dictionary.refinementTemplate, dictionary.refinementAt, decodingContext,
                  huffmanInput
                );
            }

            void onImmediateTextRegion(var region, var referredSegments, var data, var start, var end)
            {
                var regionInfo = region.info;
                var huffmanTables, huffmanInput;

                // Combines exported symbols from all referred segments
                var symbols = this.symbols;
                var inputSymbols = new List<>();
                for (var i = 0, ii = referredSegments.length; i < ii; i++)
                {
                    var referredSymbols = symbols[referredSegments[i]];
                    // referredSymbols is undefined when we have a reference to a Tables
                    // segment instead of a SymbolDictionary.
                    if (referredSymbols)
                    {
                        inputSymbols = inputSymbols.concat(referredSymbols);
                    }
                }
                var symbolCodeLength = log2(inputSymbols.length);
                if (region.huffman)
                {
                    huffmanInput = new Reader(data, start, end);
                    huffmanTables = getTextRegionHuffmanTables(
                      region,
                      referredSegments,
                      this.customTables,
                      inputSymbols.length,
                      huffmanInput
                    );
                }

                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = decodeTextRegion(region.huffman, region.refinement, regionInfo.width,
                  regionInfo.height, region.defaultPixelValue, region.numberOfSymbolInstances, region.stripSize,
                  inputSymbols, symbolCodeLength, region.transposed, region.dsOffset,
                  region.referenceCorner, region.combinationOperator, huffmanTables, region.refinementTemplate,
                  region.refinementAt, decodingContext, region.logStripSize, huffmanInput
                );
                this.drawBitmap(regionInfo, bitmap);
            }
            void onImmediateLosslessTextRegion()
            {
                this.onImmediateTextRegion.apply(this, arguments);
            }
            void onPatternDictionary(var dictionary, var currentSegment, var data, var start, var end)
            {
                var patterns = this.patterns;
                if (!patterns)
                {
                    this.patterns = patterns = new Dictionary<int, var>();
                }
                var decodingContext = new DecodingContext(data, start, end);
                patterns[currentSegment] = decodePatternDictionary(dictionary.mmr, dictionary.patternWidth, dictionary.patternHeight,
                    dictionary.maxPatternIndex, dictionary.template, decodingContext);
            }
            void onImmediateHalftoneRegion(var region, var referredSegments, byte[] data, int start, int end)
            {
                // HalftoneRegion refers to exactly one PatternDictionary.
                var patterns = this.patterns[referredSegments[0]];
                var regionInfo = region.info;
                var decodingContext = new DecodingContext(data, start, end);
                var bitmap = decodeHalftoneRegion(
                  region.mmr,
                  patterns,
                  region.template,
                  regionInfo.width,
                  regionInfo.height,
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
                this.drawBitmap(regionInfo, bitmap);
            }
            void onImmediateLosslessHalftoneRegion()
            {
                this.onImmediateHalftoneRegion.apply(this, arguments);
            }

            void onTables(var currentSegment, byte[] data, int start, int end)
            {
                var customTables = this.customTables;
                if (customTables == null)
                {
                    this.customTables = customTables = new Dictionary<int, var> { };
                }
                customTables[currentSegment] = decodeTablesSegment(data, start, end);
            }
        }

        internal class HuffmanLine
        {
            public HuffmanLine(var lineData)
            {
                if (lineData.length == 2)
                {
                    // OOB line.
                    this.isOOB = true;
                    this.rangeLow = 0;
                    this.prefixLength = lineData[0];
                    this.rangeLength = 0;
                    this.prefixCode = lineData[1];
                    this.isLowerRange = false;
                }
                else
                {
                    // Normal, upper range or lower range line.
                    // Upper range lines are processed like normal lines.
                    this.isOOB = false;
                    this.rangeLow = lineData[0];
                    this.prefixLength = lineData[1];
                    this.rangeLength = lineData[2];
                    this.prefixCode = lineData[3];
                    this.isLowerRange = lineData[4] == "lower";
                }
            }
        }

        internal class HuffmanTreeNode
        {

            public HuffmanTreeNode(var line)
            {
                this.children = new List<>();
                if (line)
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


            void buildTree(var line, int shift)
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
                    var node = this.children[bit];
                    if (!node)
                    {
                        this.children[bit] = node = new HuffmanTreeNode(null);
                    }
                    node.buildTree(line, shift - 1);
                }
            }
            void decodeNode(var reader)
            {
                if (this.isLeaf)
                {
                    if (this.isOOB)
                    {
                        return null;
                    }
                    var htOffset = reader.readBits(this.rangeLength);
                    return this.rangeLow + (this.isLowerRange ? -htOffset : htOffset);
                }
                var node = this.children[reader.readBit()];
                if (!node)
                {
                    throw new Jbig2Error("invalid Huffman data");
                }
                return node.decodeNode(reader);
            }
        }

        internal class HuffmanTable
        {
            public HuffmanTable(var lines, var prefixCodesDone)
            {
                if (!prefixCodesDone)
                {
                    this.assignPrefixCodes(lines);
                }
                // Create Huffman tree.
                this.rootNode = new HuffmanTreeNode(null);
                for (var i = 0, ii = lines.length; i < ii; i++)
                {
                    var line = lines[i];
                    if (line.prefixLength > 0)
                    {
                        this.rootNode.buildTree(line, line.prefixLength - 1);
                    }
                }
            }


            void decode(var reader)
            {
                return this.rootNode.decodeNode(reader);
            }

            void assignPrefixCodes(var lines)
            {
                // Annex B.3 Assigning the prefix codes.
                var linesLength = lines.length;
                var prefixLengthMax = 0;
                for (var i = 0; i < linesLength; i++)
                {
                    prefixLengthMax = Math.max(prefixLengthMax, lines[i].prefixLength);
                }

                var histogram = new uint[prefixLengthMax + 1];
                for (var i = 0; i < linesLength; i++)
                {
                    histogram[lines[i].prefixLength]++;
                }
                var currentLength = 1,
                  firstCode = 0,
                  currentCode,
                  currentTemp,
                  line;
                histogram[0] = 0;

                while (currentLength <= prefixLengthMax)
                {
                    firstCode = (firstCode + histogram[currentLength - 1]) << 1;
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

        void decodeTablesSegment(byte[] data, int start, int end)
        {
            // Decodes a Tables segment, i.e., a custom Huffman table.
            // Annex B.2 Code table structure.
            var flags = data[start];
            var lowestValue = data.readUint32(start + 1) & 0xffffffff;
            var highestValue = data.readUint32(start + 5) & 0xffffffff;
            var reader = new Reader(data, start + 9, end);

            var prefixSizeBits = ((flags >> 1) & 7) + 1;
            var rangeSizeBits = ((flags >> 4) & 7) + 1;
            var lines = new List<>();
            var prefixLength,
              rangeLength,
              currentRangeLow = lowestValue;

            // Normal table lines
            do
            {
                prefixLength = reader.readBits(prefixSizeBits);
                rangeLength = reader.readBits(rangeSizeBits);
                lines.Add(
                  new HuffmanLine(new[] { currentRangeLow, prefixLength, rangeLength, 0 })
                  );
                currentRangeLow += 1 << rangeLength;
            } while (currentRangeLow < highestValue);

            // Lower range table line
            prefixLength = reader.readBits(prefixSizeBits);
            lines.Add(
              new HuffmanLine(new[] { lowestValue - 1, prefixLength, 32, 0, "lower" })
                );

            // Upper range table line
            prefixLength = reader.readBits(prefixSizeBits);
            lines.Add(new HuffmanLine(new[] { highestValue, prefixLength, 32, 0 }));

            if (flags & 1)
            {
                // Out-of-band table line
                prefixLength = reader.readBits(prefixSizeBits);
                lines.Add(new HuffmanLine(new[] { prefixLength, 0 }));
            }

            return new HuffmanTable(lines, false);
        }

        Dictionary<int, var> standardTablesCache = new Dictionary<int, var>();

        void getStandardTable(int number)
        {
            // Annex B.5 Standard Huffman tables.
            if (standardTablesCache.TryGetValue(number, out var table))
            {
                return table;
            }
            var lines;
            switch (number)
            {
                case 1:
                    lines = new[] {
                        new []{0, 1, 4, 0x0 },
                        new []{16, 2, 8, 0x2 },
                        new []{272, 3, 16, 0x6 },
                        new []{65808, 3, 32, 0x7 }, // upper
                    };
                    break;
                case 2:
                    lines = new[]{
                      new []{0, 1, 0, 0x0 },
                      new []{1, 2, 0, 0x2 },
                      new []{2, 3, 0, 0x6 },
                      new []{3, 4, 3, 0xe },
                      new []{11, 5, 6, 0x1e },
                      new []{75, 6, 32, 0x3e }, // upper
                      new []{6, 0x3f }, // OOB
                      };
                    break;
                case 3:
                    lines = new[] {
                        new []{-256, 8, 8, 0xfe },
                        new []{0, 1, 0, 0x0 },
                        new []{1, 2, 0, 0x2 },
                        new []{2, 3, 0, 0x6 },
                        new []{3, 4, 3, 0xe },
                        new []{11, 5, 6, 0x1e },
                        new []{-257, 8, 32, 0xff, "lower" },
                        new []{75, 7, 32, 0x7e }, // upper
                        new []{6, 0x3e }, // OOB
                    };
                    break;
                case 4:
                    lines = new[] {
                      new []{1, 1, 0, 0x0 },
                      new []{2, 2, 0, 0x2 },
                      new []{3, 3, 0, 0x6 },
                      new []{4, 4, 3, 0xe },
                      new []{12, 5, 6, 0x1e },
                      new []{76, 5, 32, 0x1f }, // upper
                    };
                    break;
                case 5:
                    lines = new[] {
                      new []{-255, 7, 8, 0x7e },
                      new []{1, 1, 0, 0x0 },
                      new []{2, 2, 0, 0x2 },
                      new []{3, 3, 0, 0x6 },
                      new []{4, 4, 3, 0xe },
                      new []{12, 5, 6, 0x1e },
                      new []{-256, 7, 32, 0x7f, "lower" },
                      new []{76, 6, 32, 0x3e }, // upper
                    };
                    break;
                case 6:
                    lines = new[] {
                        new[]{-2048, 5, 10, 0x1c},
                        new []{-1024, 4, 9, 0x8},
                        new []{-512, 4, 8, 0x9},
                        new []{-256, 4, 7, 0xa},
                        new []{-128, 5, 6, 0x1d},
                        new []{-64, 5, 5, 0x1e},
                        new []{-32, 4, 5, 0xb},
                        new []{0, 2, 7, 0x0},
                        new []{128, 3, 7, 0x2},
                        new []{256, 3, 8, 0x3},
                        new []{512, 4, 9, 0xc},
                        new []{1024, 4, 10, 0xd},
                        new []{-2049, 6, 32, 0x3e, "lower"},
                        new []{2048, 6, 32, 0x3f}, // upper
                    };
                    break;
                case 7:
                    lines = new[]{
                        new[] { -1024, 4, 9, 0x8},
                        new []{-512, 3, 8, 0x0},
                        new []{-256, 4, 7, 0x9},
                        new []{-128, 5, 6, 0x1a},
                        new []{-64, 5, 5, 0x1b},
                        new []{-32, 4, 5, 0xa},
                        new []{0, 4, 5, 0xb},
                        new []{32, 5, 5, 0x1c},
                        new []{64, 5, 6, 0x1d},
                        new []{128, 4, 7, 0xc},
                        new []{256, 3, 8, 0x1},
                        new []{512, 3, 9, 0x2},
                        new []{1024, 3, 10, 0x3},
                        new []{-1025, 5, 32, 0x1e, "lower"},
                        new []{2048, 5, 32, 0x1f}, // upper
                    };
                    break;
                case 8:
                    lines = new[]{
                        new []{-15, 8, 3, 0xfc},
                        new []{-7, 9, 1, 0x1fc},
                        new []{-5, 8, 1, 0xfd},
                        new []{-3, 9, 0, 0x1fd},
                        new []{-2, 7, 0, 0x7c},
                        new []{-1, 4, 0, 0xa},
                        new []{0, 2, 1, 0x0},
                        new []{2, 5, 0, 0x1a},
                        new []{3, 6, 0, 0x3a},
                        new []{4, 3, 4, 0x4},
                        new []{20, 6, 1, 0x3b},
                        new []{22, 4, 4, 0xb},
                        new []{38, 4, 5, 0xc},
                        new []{70, 5, 6, 0x1b},
                        new []{134, 5, 7, 0x1c},
                        new []{262, 6, 7, 0x3c},
                        new []{390, 7, 8, 0x7d},
                        new []{646, 6, 10, 0x3d},
                        new []{-16, 9, 32, 0x1fe, "lower"},
                        new []{1670, 9, 32, 0x1ff}, // upper
                        new []{2, 0x1}, // OOB
                    };
                    break;
                case 9:
                    lines = new[]{
                        new []{-31, 8, 4, 0xfc},
                        new []{-15, 9, 2, 0x1fc},
                        new []{-11, 8, 2, 0xfd},
                        new []{-7, 9, 1, 0x1fd},
                        new []{-5, 7, 1, 0x7c},
                        new []{-3, 4, 1, 0xa},
                        new []{-1, 3, 1, 0x2},
                        new []{1, 3, 1, 0x3},
                        new []{3, 5, 1, 0x1a},
                        new []{5, 6, 1, 0x3a},
                        new []{7, 3, 5, 0x4},
                        new []{39, 6, 2, 0x3b},
                        new []{43, 4, 5, 0xb},
                        new []{75, 4, 6, 0xc},
                        new []{139, 5, 7, 0x1b},
                        new []{267, 5, 8, 0x1c},
                        new []{523, 6, 8, 0x3c},
                        new []{779, 7, 9, 0x7d},
                        new []{1291, 6, 11, 0x3d},
                        new []{-32, 9, 32, 0x1fe, "lower"},
                        new []{3339, 9, 32, 0x1ff}, // upper
                        new []{2, 0x0}, // OOB
                    };
                    break;
                case 10:
                    lines = new[]{
                      new []{-21, 7, 4, 0x7a},
          new []{-5, 8, 0, 0xfc},
          new []{-4, 7, 0, 0x7b},
          new []{-3, 5, 0, 0x18},
          new []{-2, 2, 2, 0x0},
          new []{2, 5, 0, 0x19},
          new []{3, 6, 0, 0x36},
          new []{4, 7, 0, 0x7c},
          new []{5, 8, 0, 0xfd},
          new []{6, 2, 6, 0x1},
          new []{70, 5, 5, 0x1a},
          new []{102, 6, 5, 0x37},
          new []{134, 6, 6, 0x38},
          new []{198, 6, 7, 0x39},
          new []{326, 6, 8, 0x3a},
          new []{582, 6, 9, 0x3b},
          new []{1094, 6, 10, 0x3c},
          new []{2118, 7, 11, 0x7d},
          new []{-22, 8, 32, 0xfe, "lower"},
          new []{4166, 8, 32, 0xff}, // upper
          new []{2, 0x2}, // OOB
          };
                    break;
                case 11:
                    lines = new[]{
          new[] { 1, 1, 0, 0x0},
          new []{2, 2, 1, 0x2},
          new []{4, 4, 0, 0xc},
          new []{5, 4, 1, 0xd},
          new []{7, 5, 1, 0x1c},
          new []{9, 5, 2, 0x1d},
          new []{13, 6, 2, 0x3c},
          new []{17, 7, 2, 0x7a},
          new []{21, 7, 3, 0x7b},
          new []{29, 7, 4, 0x7c},
          new []{45, 7, 5, 0x7d},
          new []{77, 7, 6, 0x7e},
          new []{141, 7, 32, 0x7f}, // upper
          };
                    break;
                case 12:
                    lines = new[]{
          new []{1, 1, 0, 0x0},
          new []{2, 2, 0, 0x2},
          new []{3, 3, 1, 0x6},
          new []{5, 5, 0, 0x1c},
          new []{6, 5, 1, 0x1d},
          new []{8, 6, 1, 0x3c},
          new []{10, 7, 0, 0x7a},
          new []{11, 7, 1, 0x7b},
          new []{13, 7, 2, 0x7c},
          new []{17, 7, 3, 0x7d},
          new []{25, 7, 4, 0x7e},
          new []{41, 8, 5, 0xfe},
          new []{73, 8, 32, 0xff}, // upper
          };
                    break;
                case 13:
                    lines = new[]{
          new []{1, 1, 0, 0x0},
          new []{2, 3, 0, 0x4},
          new []{3, 4, 0, 0xc},
          new []{4, 5, 0, 0x1c},
          new []{5, 4, 1, 0xd},
          new []{7, 3, 3, 0x5},
          new []{15, 6, 1, 0x3a},
          new []{17, 6, 2, 0x3b},
          new []{21, 6, 3, 0x3c},
          new []{29, 6, 4, 0x3d},
          new []{45, 6, 5, 0x3e},
          new []{77, 7, 6, 0x7e},
          new []{141, 7, 32, 0x7f}, // upper
          };
                    break;
                case 14:
                    lines = new[]{
          new []{-2, 3, 0, 0x4},
          new []{-1, 3, 0, 0x5},
          new []{0, 1, 0, 0x0},
          new []{1, 3, 0, 0x6},
          new []{2, 3, 0, 0x7},
          };
                    break;
                case 15:
                    lines = new[]{
          new []{-24, 7, 4, 0x7c},
          new []{-8, 6, 2, 0x3c},
          new []{-4, 5, 1, 0x1c},
          new []{-2, 4, 0, 0xc},
          new []{-1, 3, 0, 0x4},
          new []{0, 1, 0, 0x0},
          new []{1, 3, 0, 0x5},
          new []{2, 4, 0, 0xd},
          new []{3, 5, 1, 0x1d},
          new []{5, 6, 2, 0x3d},
          new []{9, 7, 4, 0x7d},
          new []{-25, 7, 32, 0x7e, "lower"},
          new []{25, 7, 32, 0x7f}, // upper
          };
                    break;
                default:
                    throw new Jbig2Error("standard table B.${number} does not exist");
            }

            for (var i = 0, ii = lines.length; i < ii; i++)
            {
                lines[i] = new HuffmanLine(lines[i]);
            }
            table = new HuffmanTable(lines, true);
            standardTablesCache[number] = table;
            return table;
        }
        public class Reader
        {
            public Reader(byte[] data, int start, int end)
            {
                this.data = data;
                this.start = start;
                this.end = end;
                this.position = start;
                this.shift = -1;
                this.currentByte = 0;
            }

            void readBit()
            {
                if (this.shift < 0)
                {
                    if (this.position >= this.end)
                    {
                        throw new Jbig2Error("end of data while reading bit");
                    }
                    this.currentByte = this.data[this.position++];
                    this.shift = 7;
                }
                var bit = (this.currentByte >> this.shift) & 1;
                this.shift--;
                return bit;
            }

            void readBits(int numBits)
            {
                var result = 0,
                  i;
                for (i = numBits - 1; i >= 0; i--)
                {
                    result |= this.readBit() << i;
                }
                return result;
            }

            void byteAlign()
            {
                this.shift = -1;
            }

            void next()
            {
                if (this.position >= this.end)
                {
                    return -1;
                }
                return this.data[this.position++];
            }
        }

        void getCustomHuffmanTable(int index, int referredTo, int customTables)
        {
            // Returns a Tables segment that has been earlier decoded.
            // See 7.4.2.1.6 (symbol dictionary) or 7.4.3.1.6 (text region).
            var currentIndex = 0;
            for (var i = 0, ii = referredTo.length; i < ii; i++)
            {
                var table = customTables[referredTo[i]];
                if (table)
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

        void getTextRegionHuffmanTables(var textRegion, var referredTo, var customTables, var numberOfSymbols, var reader)
        {
            // 7.4.3.1.7 Symbol ID Huffman table decoding

            // Read code lengths for RUNCODEs 0...34.
            var codes = new List<>();
            for (var i = 0; i <= 34; i++)
            {
                var codeLength = reader.readBits(4);
                codes.Add(new HuffmanLine(new[] { i, codeLength, 0, 0 }));
            }
            // Assign Huffman codes for RUNCODEs.
            var runCodesTable = new HuffmanTable(codes, false);

            // Read a Huffman code using the assignment above.
            // Interpret the RUNCODE codes and the additional bits (if any).
            codes.length = 0;
            for (var i = 0; i < numberOfSymbols;)
            {
                var codeLength = runCodesTable.decode(reader);
                if (codeLength >= 32)
                {
                    var repeatedLength, numberOfRepeats, j;
                    switch (codeLength)
                    {
                        case 32:
                            if (i == 0)
                            {
                                throw new Jbig2Error("no previous value in symbol ID table");
                            }
                            numberOfRepeats = reader.readBits(2) + 3;
                            repeatedLength = codes[i - 1].prefixLength;
                            break;
                        case 33:
                            numberOfRepeats = reader.readBits(3) + 3;
                            repeatedLength = 0;
                            break;
                        case 34:
                            numberOfRepeats = reader.readBits(7) + 11;
                            repeatedLength = 0;
                            break;
                        default:
                            throw new Jbig2Error("invalid code length in symbol ID table");
                    }
                    for (j = 0; j < numberOfRepeats; j++)
                    {
                        codes.Add(new HuffmanLine(new[] { i, repeatedLength, 0, 0 }));
                        i++;
                    }
                }
                else
                {
                    codes.Add(new HuffmanLine(new[] { i, codeLength, 0, 0 }));
                    i++;
                }
            }
            reader.byteAlign();
            var symbolIDTable = new HuffmanTable(codes, false);

            // 7.4.3.1.6 Text region segment Huffman table selection

            var customIndex = 0,
              tableFirstS,
              tableDeltaS,
              tableDeltaT;

            switch (textRegion.huffmanFS)
            {
                case 0:
                case 1:
                    tableFirstS = getStandardTable(textRegion.huffmanFS + 6);
                    break;
                case 3:
                    tableFirstS = getCustomHuffmanTable(customIndex, referredTo, customTables);
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
                    tableDeltaS = getStandardTable(textRegion.huffmanDS + 8);
                    break;
                case 3:
                    tableDeltaS = getCustomHuffmanTable(customIndex, referredTo, customTables);
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
                    tableDeltaT = getStandardTable(textRegion.huffmanDT + 11);
                    break;
                case 3:
                    tableDeltaT = getCustomHuffmanTable(customIndex, referredTo, customTables);
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

            return new var(symbolIDTable, tableFirstS, tableDeltaS, tableDeltaT);
        }

        void getSymbolDictionaryHuffmanTables(var dictionary, var referredTo, var customTables)
        {
            // 7.4.2.1.6 Symbol dictionary segment Huffman table selection

            var customIndex = 0,
              tableDeltaHeight,
              tableDeltaWidth;
            switch (dictionary.huffmanDHSelector)
            {
                case 0:
                case 1:
                    tableDeltaHeight = getStandardTable(dictionary.huffmanDHSelector + 4);
                    break;
                case 3:
                    tableDeltaHeight = getCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DH selector");
            }

            switch (dictionary.huffmanDWSelector)
            {
                case 0:
                case 1:
                    tableDeltaWidth = getStandardTable(dictionary.huffmanDWSelector + 2);
                    break;
                case 3:
                    tableDeltaWidth = getCustomHuffmanTable(customIndex, referredTo, customTables);
                    customIndex++;
                    break;
                default:
                    throw new Jbig2Error("invalid Huffman DW selector");
            }

            var tableBitmapSize, tableAggregateInstances;
            if (dictionary.bitmapSizeSelector)
            {
                tableBitmapSize = getCustomHuffmanTable(customIndex, referredTo, customTables);
                customIndex++;
            }
            else
            {
                tableBitmapSize = getStandardTable(1);
            }

            if (dictionary.aggregationInstancesSelector)
            {
                tableAggregateInstances = getCustomHuffmanTable(customIndex, referredTo, customTables);
            }
            else
            {
                tableAggregateInstances = getStandardTable(1);
            }

            return new var(tableDeltaHeight, tableDeltaWidth, tableBitmapSize, tableAggregateInstances);
        }

        void readUncompressedBitmap(var reader, int width, int height)
        {
            var bitmap = new List<>();
            for (var y = 0; y < height; y++)
            {
                var row = new byte[](width);
                bitmap.Add(row);
                for (var x = 0; x < width; x++)
                {
                    row[x] = reader.readBit();
                }
                reader.byteAlign();
            }
            return bitmap;
        }

        void decodeMMRBitmap(var input, int width, int height, int endOfBlock)
        {
            // MMR is the same compression algorithm as the PDF filter
            // CCITTFaxDecode with /K -1.
            var paramss = FaxParams(K: -1, Columns: width, Rows: height, BlackIs1: true, EndOfBlock: endOfBlock);
            var decoder = new CCITTFaxDecoder(input, paramss);
            var bitmap = new List<>();
            var currentByte;
            var eof = false;

            for (var y = 0; y < height; y++)
            {
                var row = new byte[](width);
                bitmap.Add(row);
                var shift = -1;
                for (var x = 0; x < width; x++)
                {
                    if (shift < 0)
                    {
                        currentByte = decoder.readNextChar();
                        if (currentByte == -1)
                        {
                            // Set the rest of the bits to zero.
                            currentByte = 0;
                            eof = true;
                        }
                        shift = 7;
                    }
                    row[x] = (currentByte >> shift) & 1;
                    shift--;
                }
            }

            if (endOfBlock && !eof)
            {
                // Read until EOFB has been consumed.
                var lookForEOFLimit = 5;
                for (var i = 0; i < lookForEOFLimit; i++)
                {
                    if (decoder.readNextChar() == -1)
                    {
                        break;
                    }
                }
            }

            return bitmap;
        }

        // eslint-disable-next-line no-shadow

        void parseChunks(var chunks)
        {
            return parseJbig2Chunks(chunks);
        }

        void parse(byte[] data)
        {
            var imgData = parseJbig2(data);
            this.width = width;
            this.height = height;
            return imgData;
        }
    }

}


