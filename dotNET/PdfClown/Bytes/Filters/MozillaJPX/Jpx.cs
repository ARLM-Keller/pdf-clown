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

using PdfClown.Documents.Contents.Fonts.CCF;
using PdfClown.Util.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PdfClown.Bytes.Filters.Jpx
{
    public class JpxError : Exception
    {
        public JpxError(string msg)
            : base($"JPX error: { msg}")
        {
        }
    }

    public class JpxImage
    {
        // Table E.1
        Dictionary<string, int> SubbandsGainLog2 = new Dictionary<string, int> { { "LL", 0 }, { "LH", 1 }, { "HL", 1 }, { "HH", 2 } };
        bool failOnCorruptedImage;
        internal List<TileResultB> tiles;
        internal int width;
        internal int height;
        internal ushort componentsCount;
        internal int bitsPerComponent;

        // eslint-disable-next-line no-shadow
        public JpxImage()
        {
            this.failOnCorruptedImage = false;
        }

        void JpxImageClosure()
        { }

        public void Parse(byte[] data)
        {
            var head = ReadUint16(data, 0);
            // No box header, immediate start of codestream (SOC)
            if (head == 0xff4f)
            {
                ParseCodestream(data, 0, data.Length);
                return;
            }

            var position = 0;
            var length = data.Length;
            while (position < length)
            {
                var headerSize = 8;
                long lbox = ReadUint32(data, position);
                var tbox = ReadUint32(data, position + 4);
                position += headerSize;
                if (lbox == 1)
                {
                    // XLBox: read UInt64 according to spec.
                    // JavaScript's int precision of 53 bit should be sufficient here.
                    lbox =
                      ReadUint32(data, position) * 4294967296 +
                      ReadUint32(data, position + 4);
                    position += 8;
                    headerSize += 8;
                }
                if (lbox == 0)
                {
                    lbox = length - position + headerSize;
                }
                if (lbox < headerSize)
                {
                    throw new JpxError("Invalid box field size");
                }
                var dataLength = lbox - headerSize;
                var jumpDataLength = true;
                switch (tbox)
                {
                    case 0x6a703268: // 'jp2h'
                        jumpDataLength = false; // parsing child boxes
                        break;
                    case 0x636f6c72: // 'colr'
                                     // Colorspaces are not used, the CS from the PDF is used.
                        var method = data[position];
                        if (method == 1)
                        {
                            // enumerated colorspace
                            var colorspace = ReadUint32(data, position + 3);
                            switch (colorspace)
                            {
                                case 16: // this indicates a sRGB colorspace
                                case 17: // this indicates a grayscale colorspace
                                case 18: // this indicates a YUV colorspace
                                    break;
                                default:
                                    Debug.WriteLine("warn: Unknown colorspace " + colorspace);
                                    break;
                            }
                        }
                        else if (method == 2)
                        {
                            Debug.WriteLine("info: ICC profile not supported");
                        }
                        break;
                    case 0x6a703263: // 'jp2c'
                        this.ParseCodestream(data, position, position + dataLength);
                        break;
                    case 0x6a502020: // 'jP\024\024'
                        if (ReadUint32(data, position) != 0x0d0a870a)
                        {
                            Debug.WriteLine("warn: Invalid JP2 signature");
                        }
                        break;
                    // The following header types are valid but currently not used:
                    case 0x6a501a1a: // 'jP\032\032'
                    case 0x66747970: // 'ftyp'
                    case 0x72726571: // 'rreq'
                    case 0x72657320: // 'res '
                    case 0x69686472: // 'ihdr'
                        break;
                    default:
                        var headerType = new string(new char[] {
                          (char)((tbox >> 24) & 0xff),
                          (char)((tbox >> 16) & 0xff),
                          (char)((tbox >> 8) & 0xff),
                          (char)(tbox & 0xff)});
                        Debug.WriteLine("warn: Unsupported header type " + tbox + " (" + headerType + ")");
                        break;
                }
                if (jumpDataLength)
                {
                    position += (int)dataLength;
                }
            }
        }

        void ParseImageProperties(BinaryReader stream)
        {
            var newByte = stream.ReadByte();
            while (newByte >= 0)
            {
                var oldByte = newByte;
                newByte = stream.ReadByte();
                var code = (oldByte << 8) | newByte;
                // Image and tile size (SIZ)
                if (code == 0xff51)
                {
                    stream.BaseStream.Position += 4;
                    var Xsiz = ((uint)stream.ReadInt32()) >> 0; // Byte 4
                    var Ysiz = ((uint)stream.ReadInt32()) >> 0; // Byte 8
                    var XOsiz = ((uint)stream.ReadInt32()) >> 0; // Byte 12
                    var YOsiz = ((uint)stream.ReadInt32()) >> 0; // Byte 16
                    stream.BaseStream.Position += 16;
                    var Csiz = stream.ReadUInt16(); // Byte 36
                    this.width = (int)(Xsiz - XOsiz);
                    this.height = (int)(Ysiz - YOsiz);
                    this.componentsCount = Csiz;
                    // Results are always returned as "Uint8ClampedArray"s.
                    this.bitsPerComponent = 8;
                    return;
                }
            }
            throw new JpxError("No size marker found in JPX stream");
        }
        void ParseCodestream(byte[] data, int start, long end)
        {
            var context = new Context();
            var doNotRecover = false;
            try
            {
                var position = start;
                while (position + 1 < end)
                {
                    var code = ReadUint16(data, position);
                    position += 2;

                    var length = 0;
                    var j = 0;
                    var sqcd = 0;
                    var spqcds = (List<EpsilonMU>)null;
                    var spqcdSize = 0;
                    var scalarExpounded = false;
                    var tile = (Tile)null;
                    switch (code)
                    {
                        case 0xff4f: // Start of codestream (SOC)
                            context.mainHeader = true;
                            break;
                        case 0xffd9: // End of codestream (EOC)
                            break;
                        case 0xff51: // Image and tile size (SIZ)
                            length = ReadUint16(data, position);
                            var siz = new SIZ(
                                Xsiz: ReadUint32(data, position + 4),
                                Ysiz: ReadUint32(data, position + 8),
                                XOsiz: ReadUint32(data, position + 12),
                                YOsiz: ReadUint32(data, position + 16),
                                XTsiz: ReadUint32(data, position + 20),
                                YTsiz: ReadUint32(data, position + 24),
                                XTOsiz: ReadUint32(data, position + 28),
                                YTOsiz: ReadUint32(data, position + 32));
                            var componentsCount = ReadUint16(data, position + 36);
                            siz.Csiz = componentsCount;
                            var components = new List<Component>(componentsCount);
                            j = position + 38;
                            for (var i = 0; i < componentsCount; i++)
                            {
                                Component component = new Component(
                                    precision: (data[j] & 0x7f) + 1,
                                    isSigned: 0 != (data[j] & 0x80),
                                    XRsiz: data[j + 1],
                                    YRsiz: data[j + 2]
                                    );
                                j += 3;
                                CalculateComponentDimensions(component, siz);
                                components.Add(component);
                            }
                            context.SIZ = siz;
                            context.components = components;
                            CalculateTileGrids(context, components);
                            context.QCC = new Dictionary<int, Quantization>();
                            context.COC = new Dictionary<int, Cod>();
                            break;
                        case 0xff5c: // Quantization default (QCD)
                            length = ReadUint16(data, position);
                            var qcd = new Quantization();
                            j = position + 2;
                            sqcd = data[j++];
                            switch (sqcd & 0x1f)
                            {
                                case 0:
                                    spqcdSize = 8;
                                    scalarExpounded = true;
                                    break;
                                case 1:
                                    spqcdSize = 16;
                                    scalarExpounded = false;
                                    break;
                                case 2:
                                    spqcdSize = 16;
                                    scalarExpounded = true;
                                    break;
                                default:
                                    throw new Exception("Invalid SQcd value " + sqcd);
                            }
                            qcd.noQuantization = spqcdSize == 8;
                            qcd.scalarExpounded = scalarExpounded;
                            qcd.guardBits = sqcd >> 5;
                            spqcds = new List<EpsilonMU>();
                            while (j < length + position)
                            {
                                var spqcd = new EpsilonMU();
                                if (spqcdSize == 8)
                                {
                                    spqcd.epsilon = data[j++] >> 3;
                                    spqcd.mu = 0;
                                }
                                else
                                {
                                    spqcd.epsilon = data[j] >> 3;
                                    spqcd.mu = ((data[j] & 0x7) << 8) | data[j + 1];
                                    j += 2;
                                }
                                spqcds.Add(spqcd);
                            }
                            qcd.SPqcds = spqcds;
                            if (context.mainHeader)
                            {
                                context.QCD = qcd;
                            }
                            else
                            {
                                context.currentTile.QCD = qcd;
                                context.currentTile.QCC = new Dictionary<int, Quantization>();
                            }
                            break;
                        case 0xff5d: // Quantization component (QCC)
                            length = ReadUint16(data, position);
                            var qcc = new Quantization();
                            j = position + 2;
                            ushort cqcc;
                            if (context.SIZ.Csiz < 257)
                            {
                                cqcc = data[j++];
                            }
                            else
                            {
                                cqcc = ReadUint16(data, j);
                                j += 2;
                            }
                            sqcd = data[j++];
                            switch (sqcd & 0x1f)
                            {
                                case 0:
                                    spqcdSize = 8;
                                    scalarExpounded = true;
                                    break;
                                case 1:
                                    spqcdSize = 16;
                                    scalarExpounded = false;
                                    break;
                                case 2:
                                    spqcdSize = 16;
                                    scalarExpounded = true;
                                    break;
                                default:
                                    throw new Exception("Invalid SQcd value " + sqcd);
                            }
                            qcc.noQuantization = spqcdSize == 8;
                            qcc.scalarExpounded = scalarExpounded;
                            qcc.guardBits = sqcd >> 5;
                            spqcds = new List<EpsilonMU>();
                            while (j < length + position)
                            {
                                var spqcd = new EpsilonMU { };
                                if (spqcdSize == 8)
                                {
                                    spqcd.epsilon = data[j++] >> 3;
                                    spqcd.mu = 0;
                                }
                                else
                                {
                                    spqcd.epsilon = data[j] >> 3;
                                    spqcd.mu = ((data[j] & 0x7) << 8) | data[j + 1];
                                    j += 2;
                                }
                                spqcds.Add(spqcd);
                            }
                            qcc.SPqcds = spqcds;
                            if (context.mainHeader)
                            {
                                context.QCC[cqcc] = qcc;
                            }
                            else
                            {
                                context.currentTile.QCC[cqcc] = qcc;
                            }
                            break;
                        case 0xff52: // Coding style default (COD)
                            length = ReadUint16(data, position);
                            var cod = new Cod();
                            j = position + 2;
                            var scod = data[j++];

                            cod.entropyCoderWithCustomPrecincts = 0 != (scod & 1);
                            cod.sopMarkerUsed = 0 != (scod & 2);
                            cod.ephMarkerUsed = 0 != (scod & 4);

                            cod.progressionOrder = data[j++];
                            cod.layersCount = ReadUint16(data, j);
                            j += 2;
                            cod.multipleComponentTransform = data[j++] != 0;

                            cod.decompositionLevelsCount = data[j++];
                            cod.xcb = (data[j++] & 0xf) + 2;
                            cod.ycb = (data[j++] & 0xf) + 2;
                            var blockStyle = data[j++];
                            cod.selectiveArithmeticCodingBypass = 0 != (blockStyle & 1);
                            cod.resetContextProbabilities = 0 != (blockStyle & 2);
                            cod.terminationOnEachCodingPass = 0 != (blockStyle & 4);
                            cod.verticallyStripe = 0 != (blockStyle & 8);
                            cod.predictableTermination = 0 != (blockStyle & 16);
                            cod.segmentationSymbolUsed = 0 != (blockStyle & 32);
                            cod.reversibleTransformation = data[j++] != 0;
                            if (cod.entropyCoderWithCustomPrecincts)
                            {
                                var precinctsSizes = new List<PrecinctSize>();
                                while (j < length + position)
                                {
                                    var precinctsSize = data[j++];
                                    precinctsSizes.Add(new PrecinctSize(PPx: precinctsSize & 0xf, PPy: precinctsSize >> 4));
                                }
                                cod.precinctsSizes = precinctsSizes;
                            }
                            var unsupported = new List<string>();
                            if (cod.selectiveArithmeticCodingBypass)
                            {
                                unsupported.Add("selectiveArithmeticCodingBypass");
                            }
                            if (cod.resetContextProbabilities)
                            {
                                unsupported.Add("resetContextProbabilities");
                            }
                            if (cod.terminationOnEachCodingPass)
                            {
                                unsupported.Add("terminationOnEachCodingPass");
                            }
                            if (cod.verticallyStripe)
                            {
                                unsupported.Add("verticallyStripe");
                            }
                            if (cod.predictableTermination)
                            {
                                unsupported.Add("predictableTermination");
                            }
                            if (unsupported.Count > 0)
                            {
                                doNotRecover = true;
                                throw new Exception(
                                  "Unsupported COD options (" + string.Join(", ", unsupported) + ")"
                                );
                            }
                            if (context.mainHeader)
                            {
                                context.COD = cod;
                            }
                            else
                            {
                                context.currentTile.COD = cod;
                                context.currentTile.COC = new Dictionary<int, Cod>();
                            }
                            break;
                        case 0xff90: // Start of tile-part (SOT)
                            length = ReadUint16(data, position);
                            tile = new Tile(index: ReadUint16(data, position + 2),
                                            length: (int)ReadUint32(data, position + 4),
                                            partIndex: data[position + 8],
                                            partsCount: data[position + 9]);
                            tile.dataEnd = tile.length + position - 2;


                            context.mainHeader = false;
                            if (tile.partIndex == 0)
                            {
                                // reset component specific settings
                                tile.COD = context.COD;
                                tile.COC = context.COC; // clone of the global COC
                                tile.QCD = context.QCD;
                                tile.QCC = context.QCC; // clone of the global COC
                            }
                            context.currentTile = tile;
                            break;
                        case 0xff93: // Start of data (SOD)
                            tile = context.currentTile;
                            if (tile.partIndex == 0)
                            {
                                InitializeTile(context, tile.index);
                                BuildPackets(context);
                            }

                            // moving to the end of the data
                            length = tile.dataEnd - position;
                            ParseTilePackets(context, data, position, length);
                            break;
                        case 0xff55: // Tile-part lengths, main header (TLM)
                        case 0xff57: // Packet length, main header (PLM)
                        case 0xff58: // Packet length, tile-part header (PLT)
                        case 0xff64: // Comment (COM)
                            length = ReadUint16(data, position);
                            // skipping content
                            break;
                        case 0xff53: // Coding style component (COC)
                            throw new Exception(
                              "Codestream code 0xFF53 (COC) is not implemented"
                            );
                        default:
                            throw new Exception("Unknown codestream code: " + code.ToString());
                    }
                    position += length;
                }
            }
            catch (Exception e)
            {
                if (doNotRecover || this.failOnCorruptedImage)
                {
                    throw new JpxError(e.Message);
                }
                else
                {
                    Debug.WriteLine("warn: JPX: Trying to recover from: " + e.Message);
                }
            }
            this.tiles = TransformComponents(context);
            this.width = (int)(context.SIZ.Xsiz - context.SIZ.XOsiz);
            this.height = (int)(context.SIZ.Ysiz - context.SIZ.YOsiz);
            this.componentsCount = context.SIZ.Csiz;
        }

        void CalculateComponentDimensions(Component component, SIZ siz)
        {
            // Section B.2 Component mapping
            component.x0 = (int)Math.Ceiling((double)siz.XOsiz / component.XRsiz);
            component.x1 = (int)Math.Ceiling((double)siz.Xsiz / component.XRsiz);
            component.y0 = (int)Math.Ceiling((double)siz.YOsiz / component.YRsiz);
            component.y1 = (int)Math.Ceiling((double)siz.Ysiz / component.YRsiz);
            component.width = component.x1 - component.x0;
            component.height = component.y1 - component.y0;
        }

        void CalculateTileGrids(Context context, List<Component> components)
        {
            var siz = context.SIZ;
            // Section B.3 Division into tile and tile-components
            var tile = (Tile)null;
            var tiles = new List<Tile>();
            var numXtiles = (int)Math.Ceiling((double)(siz.Xsiz - siz.XTOsiz) / siz.XTsiz);
            var numYtiles = (int)Math.Ceiling((double)(siz.Ysiz - siz.YTOsiz) / siz.YTsiz);
            for (var q = 0; q < numYtiles; q++)
            {
                for (var p = 0; p < numXtiles; p++)
                {
                    tile = new Tile();
                    tile.tx0 = (int)Math.Max(siz.XTOsiz + p * siz.XTsiz, siz.XOsiz);
                    tile.ty0 = (int)Math.Max(siz.YTOsiz + q * siz.YTsiz, siz.YOsiz);
                    tile.tx1 = (int)Math.Min(siz.XTOsiz + (p + 1) * siz.XTsiz, siz.Xsiz);
                    tile.ty1 = (int)Math.Min(siz.YTOsiz + (q + 1) * siz.YTsiz, siz.Ysiz);
                    tile.width = tile.tx1 - tile.tx0;
                    tile.height = tile.ty1 - tile.ty0;
                    tile.components = new Dictionary<int, Component>();
                    tiles.Add(tile);
                }
            }
            context.tiles = tiles;

            var componentsCount = siz.Csiz;
            for (int i = 0, ii = componentsCount; i < ii; i++)
            {
                var component = components[i];
                var jj = tiles.Count;
                for (var j = 0; j < jj; j++)
                {
                    var tileComponent = new Component();
                    tile = tiles[j];
                    tileComponent.tcx0 = (int)Math.Ceiling((double)tile.tx0 / component.XRsiz);
                    tileComponent.tcy0 = (int)Math.Ceiling((double)tile.ty0 / component.YRsiz);
                    tileComponent.tcx1 = (int)Math.Ceiling((double)tile.tx1 / component.XRsiz);
                    tileComponent.tcy1 = (int)Math.Ceiling((double)tile.ty1 / component.YRsiz);
                    tileComponent.width = tileComponent.tcx1 - tileComponent.tcx0;
                    tileComponent.height = tileComponent.tcy1 - tileComponent.tcy0;
                    tile.components[i] = tileComponent;
                }
            }
        }

        Dimension GetBlocksDimensions(Context context, Component component, int r)
        {
            var codOrCoc = component.codingStyleParameters;
            var result = new Dimension();
            if (!codOrCoc.entropyCoderWithCustomPrecincts)
            {
                result.PPx = 15;
                result.PPy = 15;
            }
            else
            {
                result.PPx = codOrCoc.precinctsSizes[r].PPx;
                result.PPy = codOrCoc.precinctsSizes[r].PPy;
            }
            // calculate codeblock size as described in section B.7
            result.xcb_ =
              r > 0
                ? Math.Min(codOrCoc.xcb, result.PPx - 1)
                : Math.Min(codOrCoc.xcb, result.PPx);
            result.ycb_ =
              r > 0
                ? Math.Min(codOrCoc.ycb, result.PPy - 1)
                : Math.Min(codOrCoc.ycb, result.PPy);
            return result;
        }

        void buildPrecincts(Context context, Resolution resolution, Dimension dimensions)
        {
            // Section B.6 Division resolution to precincts
            var precinctWidth = 1 << dimensions.PPx;
            var precinctHeight = 1 << dimensions.PPy;
            // Jasper introduces codeblock groups for mapping each subband codeblocks
            // to precincts. Precinct partition divides a resolution according to width
            // and height parameters. The subband that belongs to the resolution level
            // has a different size than the level, unless it is the zero resolution.

            // From Jasper documentation: jpeg2000.pdf, section K: Tier-2 coding:
            // The precinct partitioning for a particular subband is derived from a
            // partitioning of its parent LL band (i.e., the LL band at the next higher
            // resolution level)... The LL band associated with each resolution level is
            // divided into precincts... Each of the resulting precinct regions is then
            // mapped into its child subbands (if any) at the next lower resolution
            // level. This is accomplished by using the coordinate transformation
            // (u, v) = (ceil(x/2), ceil(y/2)) where (x, y) and (u, v) are the
            // coordinates of a point in the LL band and child subband, respectively.
            var isZeroRes = resolution.resLevel == 0;
            var precinctWidthInSubband = 1 << (dimensions.PPx + (isZeroRes ? 0 : -1));
            var precinctHeightInSubband = 1 << (dimensions.PPy + (isZeroRes ? 0 : -1));
            var numprecinctswide =
              resolution.trx1 > resolution.trx0
                ? Math.Ceiling((double)resolution.trx1 / precinctWidth) -
                  Math.Floor((double)resolution.trx0 / precinctWidth)
                : 0;
            var numprecinctshigh =
              resolution.try1 > resolution.try0
                ? Math.Ceiling((double)resolution.try1 / precinctHeight) -
                  Math.Floor((double)resolution.try0 / precinctHeight)
                : 0;
            var numprecincts = numprecinctswide * numprecinctshigh;

            resolution.precinctParameters = new PrecinctParameters(
                precinctWidth,
                precinctHeight,
                numprecinctswide,
                numprecinctshigh,
                numprecincts,
                precinctWidthInSubband,
                precinctHeightInSubband
                );
        }

        void BuildCodeblocks(Context context, SubBand subband, Dimension dimensions)
        {
            // Section B.7 Division sub-band into code-blocks
            var xcb_ = dimensions.xcb_;
            var ycb_ = dimensions.ycb_;
            var codeblockWidth = 1 << xcb_;
            var codeblockHeight = 1 << ycb_;
            var cbx0 = subband.tbx0 >> xcb_;
            var cby0 = subband.tby0 >> ycb_;
            var cbx1 = (subband.tbx1 + codeblockWidth - 1) >> xcb_;
            var cby1 = (subband.tby1 + codeblockHeight - 1) >> ycb_;
            var precinctParameters = subband.resolution.precinctParameters;
            var codeblocks = new List<CodeBlock>();
            var precincts = new Dictionary<double, Precinct>();
            var i = 0;
            var j = 0;
            var codeblock = (CodeBlock)null;
            var precinctNumber = 0D;
            for (j = cby0; j < cby1; j++)
            {
                for (i = cbx0; i < cbx1; i++)
                {
                    codeblock = new CodeBlock(
                        cbx: i,
                        cby: j,
                        tbx0: codeblockWidth * i,
                        tby0: codeblockHeight * j,
                        tbx1: codeblockWidth * (i + 1),
                        tby1: codeblockHeight * (j + 1)
                        );

                    codeblock.tbx0_ = Math.Max(subband.tbx0, codeblock.tbx0);
                    codeblock.tby0_ = Math.Max(subband.tby0, codeblock.tby0);
                    codeblock.tbx1_ = Math.Min(subband.tbx1, codeblock.tbx1);
                    codeblock.tby1_ = Math.Min(subband.tby1, codeblock.tby1);

                    // Calculate precinct number for this codeblock, codeblock position
                    // should be relative to its subband, use actual dimension and position
                    // See comment about codeblock group width and height
                    var pi = Math.Floor((double)(codeblock.tbx0_ - subband.tbx0) / precinctParameters.precinctWidthInSubband);
                    var pj = Math.Floor((double)(codeblock.tby0_ - subband.tby0) / precinctParameters.precinctHeightInSubband);
                    precinctNumber = pi + pj * precinctParameters.numprecinctswide;

                    codeblock.precinctNumber = precinctNumber;
                    codeblock.subbandType = subband.type;
                    codeblock.Lblock = 3;

                    if (codeblock.tbx1_ <= codeblock.tbx0_ ||
                      codeblock.tby1_ <= codeblock.tby0_)
                    {
                        continue;
                    }
                    codeblocks.Add(codeblock);
                    // building precinct for the sub-band
                    if (precincts.TryGetValue(precinctNumber, out var precinct))
                    {
                        if (i < precinct.cbxMin)
                        {
                            precinct.cbxMin = i;
                        }
                        else if (i > precinct.cbxMax)
                        {
                            precinct.cbxMax = i;
                        }
                        if (j < precinct.cbyMin)
                        {
                            precinct.cbxMin = j;
                        }
                        else if (j > precinct.cbyMax)
                        {
                            precinct.cbyMax = j;
                        }
                    }
                    else
                    {
                        precincts[precinctNumber] = precinct = new Precinct(
                            cbxMin: i,
                            cbyMin: j,
                            cbxMax: i,
                            cbyMax: j);
                    }
                    codeblock.precinct = precinct;
                }
            }
            subband.codeblockParameters = new CodeblockParameters(
                codeblockWidth: xcb_,
                codeblockHeight: ycb_,
                numcodeblockwide: cbx1 - cbx0 + 1,
                numcodeblockhigh: cby1 - cby0 + 1);
            subband.codeblocks = codeblocks;
            subband.precincts = precincts;
        }

        Packet CreatePacket(Resolution resolution, float precinctNumber, int layerNumber)
        {
            var precinctCodeblocks = new List<CodeBlock>();
            // Section B.10.8 Order of info in packet
            var subbands = resolution.subbands;
            // sub-bands already ordered in 'LL', 'HL', 'LH', and 'HH' sequence
            for (int i = 0, ii = subbands.Count; i < ii; i++)
            {
                var subband = subbands[i];
                var codeblocks = subband.codeblocks;
                for (int j = 0, jj = codeblocks.Count; j < jj; j++)
                {
                    var codeblock = codeblocks[j];
                    if (codeblock.precinctNumber != precinctNumber)
                    {
                        continue;
                    }
                    precinctCodeblocks.Add(codeblock);
                }
            }
            return new Packet(layerNumber, codeblocks: precinctCodeblocks);
        }

        byte readInt8(byte[] data, int offset)
        {
            return (byte)((data[offset] << 24) >> 24);
        }

        ushort ReadUint16(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        uint ReadUint32(byte[] data, int offset)
        {
            return (
              ((uint)((data[offset] << 24) |
                (data[offset + 1] << 16) |
                (data[offset + 2] << 8) |
                data[offset + 3])) >>
              0
            );
        }

        // Calculate the base 2 logarithm of the number `x`. This differs from the
        // native function in the sense that it returns the ceiling value and that it
        // returns 0 instead of `Infinity`/`NaN` for `x` values smaller than/equal to 0.
        public static int log2(double x)
        {
            if (x <= 0)
            {
                return 0;
            }
            return (int)Math.Ceiling(Math.Log(x, 2D));
        }

        internal abstract class Iterator
        {
            public abstract Packet nextPacket();
        }

        internal class LayerResolutionComponentPositionIterator : Iterator
        {
            private int l;
            private int r;
            private int i;
            private int k;
            private JpxImage image;
            private Context context;
            private SIZ siz;
            private int tileIndex;
            private Tile tile;
            private int layersCount;
            private int componentsCount;
            private int maxDecompositionLevelsCount;

            public LayerResolutionComponentPositionIterator(JpxImage image, Context context)
            {
                this.image = image;
                this.context = context;
                siz = context.SIZ;
                tileIndex = context.currentTile.index;
                tile = context.tiles[tileIndex];
                layersCount = tile.codingStyleDefaultParameters.layersCount;
                componentsCount = siz.Csiz;
                maxDecompositionLevelsCount = 0;
                for (var q = 0; q < componentsCount; q++)
                {
                    maxDecompositionLevelsCount = Math.Max(
                      maxDecompositionLevelsCount,
                      tile.components[q].codingStyleParameters.decompositionLevelsCount
                    );
                }

                l = 0;
                r = 0;
                i = 0;
                k = 0;
            }

            public override Packet nextPacket()
            {
                // Section B.12.1.1 Layer-resolution-component-position
                for (; l < layersCount; l++)
                {
                    for (; r <= maxDecompositionLevelsCount; r++)
                    {
                        for (; i < componentsCount; i++)
                        {
                            var component = tile.components[i];
                            if (r > component.codingStyleParameters.decompositionLevelsCount)
                            {
                                continue;
                            }

                            var resolution = component.resolutions[r];
                            var numprecincts = resolution.precinctParameters.numprecincts;
                            for (; k < numprecincts;)
                            {
                                var packet = image.CreatePacket(resolution, k, l);
                                k++;
                                return packet;
                            }
                            k = 0;
                        }
                        i = 0;
                    }
                    r = 0;
                }
                throw new JpxError("Out of packets");
            }
        }

        internal class ResolutionLayerComponentPositionIterator : Iterator
        {
            private JpxImage image;
            private Context context;
            private SIZ siz;
            private int tileIndex;
            private Tile tile;
            private int layersCount;
            private int componentsCount;
            private int maxDecompositionLevelsCount;
            private int r;
            private int l;
            private int i;
            private int k;

            public ResolutionLayerComponentPositionIterator(JpxImage image, Context context)
            {
                this.image = image;
                this.context = context;
                siz = context.SIZ;
                tileIndex = context.currentTile.index;
                tile = context.tiles[tileIndex];
                layersCount = tile.codingStyleDefaultParameters.layersCount;
                componentsCount = siz.Csiz;
                maxDecompositionLevelsCount = 0;
                for (var q = 0; q < componentsCount; q++)
                {
                    maxDecompositionLevelsCount = Math.Max(
                      maxDecompositionLevelsCount,
                      tile.components[q].codingStyleParameters.decompositionLevelsCount
                    );
                }

                r = 0;
                l = 0;
                i = 0;
                k = 0;
            }
            public override Packet nextPacket()
            {
                // Section B.12.1.2 Resolution-layer-component-position
                for (; r <= maxDecompositionLevelsCount; r++)
                {
                    for (; l < layersCount; l++)
                    {
                        for (; i < componentsCount; i++)
                        {
                            var component = (Component)tile.components[i];
                            if (r > component.codingStyleParameters.decompositionLevelsCount)
                            {
                                continue;
                            }

                            var resolution = component.resolutions[r];
                            var numprecincts = resolution.precinctParameters.numprecincts;
                            for (; k < numprecincts;)
                            {
                                var packet = image.CreatePacket(resolution, k, l);
                                k++;
                                return packet;
                            }
                            k = 0;
                        }
                        i = 0;
                    }
                    l = 0;
                }
                throw new JpxError("Out of packets");
            }
        }

        internal class ResolutionPositionComponentLayerIterator : Iterator
        {
            private readonly JpxImage image;
            private readonly Context context;
            private SIZ siz;
            private ushort tileIndex;
            private Tile tile;
            private ushort layersCount;
            private ushort componentsCount;
            private int l;
            private int r;
            private int c;
            private int p;
            private int maxDecompositionLevelsCount;
            private int[] maxNumPrecinctsInLevel;

            public ResolutionPositionComponentLayerIterator(JpxImage image, Context context)
            {
                this.image = image;
                this.context = context;
                siz = context.SIZ;
                tileIndex = context.currentTile.index;
                tile = context.tiles[tileIndex];
                layersCount = tile.codingStyleDefaultParameters.layersCount;
                componentsCount = siz.Csiz;
                l = 0; r = 0; c = 0; p = 0;
                maxDecompositionLevelsCount = 0;
                for (c = 0; c < componentsCount; c++)
                {
                    var component = tile.components[c];
                    maxDecompositionLevelsCount = Math.Max(
                      maxDecompositionLevelsCount,
                      component.codingStyleParameters.decompositionLevelsCount
                    );
                }
                maxNumPrecinctsInLevel = new int[maxDecompositionLevelsCount + 1];
                for (r = 0; r <= maxDecompositionLevelsCount; ++r)
                {
                    var maxNumPrecincts = 0D;
                    for (c = 0; c < componentsCount; ++c)
                    {
                        var resolutions = tile.components[c].resolutions;
                        if (r < resolutions.Count)
                        {
                            maxNumPrecincts = Math.Max(
                              maxNumPrecincts,
                              resolutions[r].precinctParameters.numprecincts
                            );
                        }
                    }
                    maxNumPrecinctsInLevel[r] = (int)maxNumPrecincts;
                }
                l = 0;
                r = 0;
                c = 0;
                p = 0;
            }

            public override Packet nextPacket()
            {
                // Section B.12.1.3 Resolution-position-component-layer
                for (; r <= maxDecompositionLevelsCount; r++)
                {
                    for (; p < maxNumPrecinctsInLevel[r]; p++)
                    {
                        for (; c < componentsCount; c++)
                        {
                            var component = tile.components[c];
                            if (r > component.codingStyleParameters.decompositionLevelsCount)
                            {
                                continue;
                            }
                            var resolution = component.resolutions[r];
                            var numprecincts = resolution.precinctParameters.numprecincts;
                            if (p >= numprecincts)
                            {
                                continue;
                            }
                            for (; l < layersCount;)
                            {
                                var packet = image.CreatePacket(resolution, p, l);
                                l++;
                                return packet;
                            }
                            l = 0;
                        }
                        c = 0;
                    }
                    p = 0;
                }
                throw new JpxError("Out of packets");
            }
        }

        internal class PositionComponentResolutionLayerIterator : Iterator
        {
            private readonly JpxImage image;
            private readonly Context context;
            private readonly SIZ siz;
            private ushort tileIndex;
            private Tile tile;
            private ushort layersCount;
            private ushort componentsCount;
            private PrecinctSizes precinctsSizes;
            private PrecinctSizes precinctsIterationSizes;
            private int l;
            private int r;
            private int c;
            private int px;
            private int py;

            public PositionComponentResolutionLayerIterator(JpxImage image, Context context)
            {
                this.image = image;
                this.context = context;
                siz = context.SIZ;
                tileIndex = context.currentTile.index;
                tile = context.tiles[tileIndex];
                layersCount = tile.codingStyleDefaultParameters.layersCount;
                componentsCount = siz.Csiz;
                precinctsSizes = image.GetPrecinctSizesInImageScale(tile);
                precinctsIterationSizes = precinctsSizes;
                l = 0;
                r = 0;
                c = 0;
                px = 0;
                py = 0;
            }

            public override Packet nextPacket()
            {
                // Section B.12.1.4 Position-component-resolution-layer
                for (; py < precinctsIterationSizes.maxNumHigh; py++)
                {
                    for (; px < precinctsIterationSizes.maxNumWide; px++)
                    {
                        for (; c < componentsCount; c++)
                        {
                            var component = tile.components[c];
                            var decompositionLevelsCount =
                              component.codingStyleParameters.decompositionLevelsCount;
                            for (; r <= decompositionLevelsCount; r++)
                            {
                                var resolution = component.resolutions[r];
                                var sizeInImageScale =
                                  precinctsSizes.components[c].resolutions[r];
                                var k = image.GetPrecinctIndexIfExist(
                                  px,
                                  py,
                                  sizeInImageScale,
                                  precinctsIterationSizes,
                                  resolution
                                );
                                if (k == null)
                                {
                                    continue;
                                }
                                for (; l < layersCount;)
                                {
                                    var packet = image.CreatePacket(resolution, (float)k, l);
                                    l++;
                                    return packet;
                                }
                                l = 0;
                            }
                            r = 0;
                        }
                        c = 0;
                    }
                    px = 0;
                }
                throw new JpxError("Out of packets");
            }
        }

        internal class ComponentPositionResolutionLayerIterator : Iterator
        {
            private readonly JpxImage image;
            private readonly Context context;
            private readonly SIZ siz;
            private readonly ushort tileIndex;
            private readonly Tile tile;
            private readonly ushort layersCount;
            private readonly ushort componentsCount;
            private readonly PrecinctSizes precinctsSizes;
            private int l;
            private int r;
            private int c;
            private int px;
            private int py;

            public ComponentPositionResolutionLayerIterator(JpxImage image, Context context)
            {
                this.image = image;
                this.context = context;
                siz = context.SIZ;
                tileIndex = context.currentTile.index;
                tile = context.tiles[tileIndex];
                layersCount = tile.codingStyleDefaultParameters.layersCount;
                componentsCount = siz.Csiz;
                precinctsSizes = image.GetPrecinctSizesInImageScale(tile);
                l = 0;
                r = 0;
                c = 0;
                px = 0;
                py = 0;

            }
            public override Packet nextPacket()
            {
                // Section B.12.1.5 Component-position-resolution-layer
                for (; c < componentsCount; ++c)
                {
                    var component = tile.components[c];
                    var precinctsIterationSizes = precinctsSizes.components[c];
                    var decompositionLevelsCount =
                      component.codingStyleParameters.decompositionLevelsCount;
                    for (; py < precinctsIterationSizes.maxNumHigh; py++)
                    {
                        for (; px < precinctsIterationSizes.maxNumWide; px++)
                        {
                            for (; r <= decompositionLevelsCount; r++)
                            {
                                var resolution = component.resolutions[r];
                                var sizeInImageScale = precinctsIterationSizes.resolutions[r];
                                var k = image.GetPrecinctIndexIfExist(
                                  px,
                                  py,
                                  sizeInImageScale,
                                  precinctsIterationSizes,
                                  resolution
                                );
                                if (k == null)
                                {
                                    continue;
                                }
                                for (; l < layersCount;)
                                {
                                    var packet = image.CreatePacket(resolution, (float)k, l);
                                    l++;
                                    return packet;
                                }
                                l = 0;
                            }
                            r = 0;
                        }
                        px = 0;
                    }
                    py = 0;
                }
                throw new JpxError("Out of packets");
            }
        }

        float? GetPrecinctIndexIfExist(
            int pxIndex,
            int pyIndex,
            Size sizeInImageScale,
            PrecinctSizes precinctIterationSizes,
            Resolution resolution)
        {
            var posX = pxIndex * precinctIterationSizes.minWidth;
            var posY = pyIndex * precinctIterationSizes.minHeight;
            if (
              posX % sizeInImageScale.width != 0 ||
              posY % sizeInImageScale.height != 0
            )
            {
                return null;
            }
            var startPrecinctRowIndex =
              (posY / sizeInImageScale.width) *
              resolution.precinctParameters.numprecinctswide;
            return (float)(posX / sizeInImageScale.height + startPrecinctRowIndex);
        }

        PrecinctSizes GetPrecinctSizesInImageScale(Tile tile)
        {
            var componentsCount = tile.components.Count;
            var minWidth = float.MaxValue;
            var minHeight = float.MaxValue;
            var maxNumWide = 0f;
            var maxNumHigh = 0f;
            var sizePerComponent = new Dictionary<int, PrecinctSizes>(componentsCount);
            for (var c = 0; c < componentsCount; c++)
            {
                var component = tile.components[c];
                var decompositionLevelsCount =
                  component.codingStyleParameters.decompositionLevelsCount;
                var sizePerResolution = new List<Size>(decompositionLevelsCount + 1);
                var minWidthCurrentComponent = float.MaxValue;
                var minHeightCurrentComponent = float.MaxValue;
                var maxNumWideCurrentComponent = 0f;
                var maxNumHighCurrentComponent = 0f;
                var scale = 1;
                for (var r = decompositionLevelsCount; r >= 0; --r)
                {
                    var resolution = component.resolutions[r];
                    var widthCurrentResolution =
                      scale * resolution.precinctParameters.precinctWidth;
                    var heightCurrentResolution =
                      scale * resolution.precinctParameters.precinctHeight;
                    minWidthCurrentComponent = Math.Min(
                      minWidthCurrentComponent,
                      widthCurrentResolution
                    );
                    minHeightCurrentComponent = Math.Min(
                      minHeightCurrentComponent,
                      heightCurrentResolution
                    );
                    maxNumWideCurrentComponent = Math.Max(
                      maxNumWideCurrentComponent,
                      (float)resolution.precinctParameters.numprecinctswide
                    );
                    maxNumHighCurrentComponent = Math.Max(
                      maxNumHighCurrentComponent,
                      (float)resolution.precinctParameters.numprecinctshigh
                    );
                    sizePerResolution[r] = new Size(
                        width: widthCurrentResolution,
                        height: heightCurrentResolution
                        );
                    scale <<= 1;
                }
                minWidth = Math.Min(minWidth, minWidthCurrentComponent);
                minHeight = Math.Min(minHeight, minHeightCurrentComponent);
                maxNumWide = Math.Max(maxNumWide, maxNumWideCurrentComponent);
                maxNumHigh = Math.Max(maxNumHigh, maxNumHighCurrentComponent);
                sizePerComponent[c] = new PrecinctSizes(
                    minWidth: minWidthCurrentComponent,
                    minHeight: minHeightCurrentComponent,
                    maxNumWide: maxNumWideCurrentComponent,
                    maxNumHigh: maxNumHighCurrentComponent,
                    resolutions: sizePerResolution
                    );
            }
            return new PrecinctSizes(
                minWidth,
                minHeight,
                maxNumWide,
                maxNumHigh,
                components: sizePerComponent
                );
        }

        void BuildPackets(Context context)
        {
            var siz = context.SIZ;
            var tileIndex = context.currentTile.index;
            var tile = context.tiles[tileIndex];
            var componentsCount = siz.Csiz;
            // Creating resolutions and sub-bands for each component
            for (var c = 0; c < componentsCount; c++)
            {
                var component = (Component)tile.components[c];
                var decompositionLevelsCount =
                  component.codingStyleParameters.decompositionLevelsCount;
                // Section B.5 Resolution levels and sub-bands
                var resolutions = new List<Resolution>();
                var subbands = new List<SubBand>();
                for (var r = 0; r <= decompositionLevelsCount; r++)
                {
                    var blocksDimensions = GetBlocksDimensions(context, component, r);
                    var scale = 1 << (decompositionLevelsCount - r);
                    var resolution = new Resolution(
                        trx0: (int)Math.Ceiling((double)component.tcx0 / scale),
                        try0: (int)Math.Ceiling((double)component.tcy0 / scale),
                        trx1: (int)Math.Ceiling((double)component.tcx1 / scale),
                        try1: (int)Math.Ceiling((double)component.tcy1 / scale),
                        resLevel: r);
                    buildPrecincts(context, resolution, blocksDimensions);
                    resolutions.Add(resolution);

                    var subband = (SubBand)null;
                    if (r == 0)
                    {
                        // one sub-band (LL) with last decomposition
                        subband = new SubBand(
                            type: "LL",
                            tbx0: (int)Math.Ceiling((double)component.tcx0 / scale),
                            tby0: (int)Math.Ceiling((double)component.tcy0 / scale),
                            tbx1: (int)Math.Ceiling((double)component.tcx1 / scale),
                            tby1: (int)Math.Ceiling((double)component.tcy1 / scale),
                            resolution: resolution);
                        BuildCodeblocks(context, subband, blocksDimensions);
                        subbands.Add(subband);
                        resolution.subbands = new List<SubBand>(new[] { subband });
                    }
                    else
                    {
                        var bscale = 1 << (decompositionLevelsCount - r + 1);
                        var resolutionSubbands = new List<SubBand>();
                        // three sub-bands (HL, LH and HH) with rest of decompositions
                        subband = new SubBand(
                            type: "HL",
                            tbx0: (int)Math.Ceiling((double)component.tcx0 / bscale - 0.5),
                            tby0: (int)Math.Ceiling((double)component.tcy0 / bscale),
                            tbx1: (int)Math.Ceiling((double)component.tcx1 / bscale - 0.5),
                            tby1: (int)Math.Ceiling((double)component.tcy1 / bscale),
                            resolution: resolution);
                        BuildCodeblocks(context, subband, blocksDimensions);
                        subbands.Add(subband);
                        resolutionSubbands.Add(subband);

                        subband = new SubBand(
                            type: "LH",
                            tbx0: (int)Math.Ceiling((double)component.tcx0 / bscale),
                            tby0: (int)Math.Ceiling((double)component.tcy0 / bscale - 0.5),
                            tbx1: (int)Math.Ceiling((double)component.tcx1 / bscale),
                            tby1: (int)Math.Ceiling((double)component.tcy1 / bscale - 0.5),
                            resolution: resolution);
                        BuildCodeblocks(context, subband, blocksDimensions);
                        subbands.Add(subband);
                        resolutionSubbands.Add(subband);

                        subband = new SubBand(
                            type: "HH",
                            tbx0: (int)Math.Ceiling((double)component.tcx0 / bscale - 0.5),
                            tby0: (int)Math.Ceiling((double)component.tcy0 / bscale - 0.5),
                            tbx1: (int)Math.Ceiling((double)component.tcx1 / bscale - 0.5),
                            tby1: (int)Math.Ceiling((double)component.tcy1 / bscale - 0.5),
                            resolution: resolution);
                        BuildCodeblocks(context, subband, blocksDimensions);
                        subbands.Add(subband);
                        resolutionSubbands.Add(subband);

                        resolution.subbands = resolutionSubbands;
                    }
                }
                component.resolutions = resolutions;
                component.subbands = subbands;
            }
            // Generate the packets sequence
            var progressionOrder = tile.codingStyleDefaultParameters.progressionOrder;
            switch (progressionOrder)
            {
                case 0:
                    tile.packetsIterator = new LayerResolutionComponentPositionIterator(this, context);
                    break;
                case 1:
                    tile.packetsIterator = new ResolutionLayerComponentPositionIterator(this, context);
                    break;
                case 2:
                    tile.packetsIterator = new ResolutionPositionComponentLayerIterator(this, context);
                    break;
                case 3:
                    tile.packetsIterator = new PositionComponentResolutionLayerIterator(this, context);
                    break;
                case 4:
                    tile.packetsIterator = new ComponentPositionResolutionLayerIterator(this, context);
                    break;
                default:
                    throw new JpxError($"Unsupported progression order { progressionOrder }");
            }
        }
        int ParseTilePackets(Context context, byte[] data, int offset, int dataLength)
        {
            var position = 0;
            var buffer = 0;
            var bufferSize = 0;
            var skipNextBit = false;
            int readBits(int count)
            {
                while (bufferSize < count)
                {
                    var b = data[offset + position];
                    position++;
                    if (skipNextBit)
                    {
                        buffer = (buffer << 7) | b;
                        bufferSize += 7;
                        skipNextBit = false;
                    }
                    else
                    {
                        buffer = (buffer << 8) | b;
                        bufferSize += 8;
                    }
                    if (b == 0xff)
                    {
                        skipNextBit = true;
                    }
                }
                bufferSize -= count;
                return (int)(((uint)buffer) >> bufferSize) & ((1 << count) - 1);
            }
            bool skipMarkerIfEqual(byte value)
            {
                if (data[offset + position - 1] == 0xff &&
                  data[offset + position] == value)
                {
                    skipBytes(1);
                    return true;
                }
                else if (data[offset + position] == 0xff &&
                data[offset + position + 1] == value)
                {
                    skipBytes(2);
                    return true;
                }
                return false;
            }
            void skipBytes(int count)
            {
                position += count;
            }
            void alignToByte()
            {
                bufferSize = 0;
                if (skipNextBit)
                {
                    position++;
                    skipNextBit = false;
                }
            }
            int readCodingpasses()
            {
                if (readBits(1) == 0)
                {
                    return 1;
                }
                if (readBits(1) == 0)
                {
                    return 2;
                }
                var value = readBits(2);
                if (value < 3)
                {
                    return value + 3;
                }
                value = readBits(5);
                if (value < 31)
                {
                    return value + 6;
                }
                value = readBits(7);
                return value + 37;
            }
            var tileIndex = context.currentTile.index;
            var tile = context.tiles[tileIndex];
            var sopMarkerUsed = context.COD.sopMarkerUsed;
            var ephMarkerUsed = context.COD.ephMarkerUsed;
            var packetsIterator = tile.packetsIterator;
            while (position < dataLength)
            {
                alignToByte();
                if (sopMarkerUsed && skipMarkerIfEqual(0x91))
                {
                    // Skip also marker segment length and packet sequence ID
                    skipBytes(4);
                }
                var packet = packetsIterator.nextPacket();
                if (0 == readBits(1))
                {
                    continue;
                }
                var layerNumber = packet.layerNumber;
                var queue = new Queue<PacketItem>();
                var codeblock = (CodeBlock)null;
                for (int i = 0, ii = packet.codeblocks.Count; i < ii; i++)
                {
                    codeblock = packet.codeblocks[i];
                    var precinct = codeblock.precinct;
                    var codeblockColumn = codeblock.cbx - precinct.cbxMin;
                    var codeblockRow = codeblock.cby - precinct.cbyMin;
                    var codeblockIncluded = false;
                    var firstTimeInclusion = false;
                    var valueReady = false;
                    var inclusionTree = (InclusionTree)null;
                    var zeroBitPlanesTree = (TagTree)null;

                    if (codeblock.included != null)
                    {
                        codeblockIncluded = 0 != readBits(1);
                    }
                    else
                    {
                        // reading inclusion tree
                        precinct = codeblock.precinct;
                        if (precinct.inclusionTree != null)
                        {
                            inclusionTree = precinct.inclusionTree;
                        }
                        else
                        {
                            // building inclusion and zero bit-planes trees
                            var width = precinct.cbxMax - precinct.cbxMin + 1;
                            var height = precinct.cbyMax - precinct.cbyMin + 1;
                            inclusionTree = new InclusionTree(width, height, layerNumber);
                            zeroBitPlanesTree = new TagTree(width, height);
                            precinct.inclusionTree = inclusionTree;
                            precinct.zeroBitPlanesTree = zeroBitPlanesTree;
                        }

                        if (inclusionTree.Reset(codeblockColumn, codeblockRow, layerNumber))
                        {
                            while (true)
                            {
                                if (readBits(1) != 0)
                                {
                                    valueReady = !inclusionTree.NextLevel();
                                    if (valueReady)
                                    {
                                        codeblock.included = true;
                                        codeblockIncluded = firstTimeInclusion = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    inclusionTree.IncrementValue(layerNumber);
                                    break;
                                }
                            }
                        }
                    }
                    if (!codeblockIncluded)
                    {
                        continue;
                    }
                    if (firstTimeInclusion)
                    {
                        zeroBitPlanesTree = precinct.zeroBitPlanesTree;
                        zeroBitPlanesTree.Reset(codeblockColumn, codeblockRow);
                        while (true)
                        {
                            if (readBits(1) != 0)
                            {
                                valueReady = !zeroBitPlanesTree.NextLevel();
                                if (valueReady)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                zeroBitPlanesTree.IncrementValue();
                            }
                        }
                        codeblock.zeroBitPlanes = zeroBitPlanesTree.value;
                    }
                    var codingpasses = readCodingpasses();
                    while (readBits(1) != 0)
                    {
                        codeblock.Lblock++;
                    }
                    var codingpassesLog2 = log2(codingpasses);
                    // rounding down log2
                    var bits =
                      (codingpasses < 1 << codingpassesLog2
                        ? codingpassesLog2 - 1
                        : codingpassesLog2) + codeblock.Lblock;
                    var codedDataLength = readBits(bits);
                    queue.Enqueue(new PacketItem(codeblock, codingpasses, dataLength: codedDataLength));
                }
                alignToByte();
                if (ephMarkerUsed)
                {
                    skipMarkerIfEqual(0x92);
                }
                while (queue.Count > 0)
                {
                    var packetItem = queue.Dequeue();
                    codeblock = packetItem.codeblock;
                    if (codeblock.data == null)
                    {
                        codeblock.data = new List<CodeBlockData>();
                    }
                    codeblock.data.Add(new CodeBlockData(
                        data,
                        start: offset + position,
                        end: offset + position + packetItem.dataLength,
                        codingpasses: packetItem.codingpasses
                        ));
                    position += packetItem.dataLength;
                }
            }
            return position;
        }
        void CopyCoefficients(float[] coefficients, int levelWidth, int levelHeight,
            SubBand subband, float delta, int mb, bool reversible, bool segmentationSymbolUsed)
        {
            var x0 = subband.tbx0;
            var y0 = subband.tby0;
            var width = subband.tbx1 - subband.tbx0;
            var codeblocks = subband.codeblocks;
            var right = subband.type[0] == 'H' ? 1 : 0;
            var bottom = subband.type[1] == 'H' ? levelWidth : 0;

            for (int i = 0, ii = codeblocks.Count; i < ii; ++i)
            {
                var codeblock = codeblocks[i];
                var blockWidth = codeblock.tbx1_ - codeblock.tbx0_;
                var blockHeight = codeblock.tby1_ - codeblock.tby0_;
                if (blockWidth == 0 || blockHeight == 0)
                {
                    continue;
                }
                if (codeblock.data == null)
                {
                    continue;
                }

                var bitModel = new BitModel(
                  blockWidth,
                  blockHeight,
                  codeblock.subbandType,
                  (byte)codeblock.zeroBitPlanes,
                  mb
                );
                var currentCodingpassType = 2; // first bit plane starts from cleanup

                // collect data
                var data = codeblock.data;
                var totalLength = 0;
                var codingpasses = 0;
                var j = 0; var jj = 0;
                var dataItem = (CodeBlockData)null;
                for (j = 0, jj = data.Count; j < jj; j++)
                {
                    dataItem = data[j];
                    totalLength += dataItem.end - dataItem.start;
                    codingpasses += dataItem.codingpasses;
                }
                var encodedData = new byte[totalLength];
                var position = 0;
                for (j = 0, jj = data.Count; j < jj; j++)
                {
                    dataItem = data[j];
                    var chunk = dataItem.data.CopyOfRange(dataItem.start, dataItem.end);
                    Array.Copy(chunk, 0, encodedData, position, chunk.Length);
                    position += chunk.Length;
                }
                // decoding the item
                var decoder = new ArithmeticDecoder(encodedData, 0, totalLength);
                bitModel.SetDecoder(decoder);

                for (j = 0; j < codingpasses; j++)
                {
                    switch (currentCodingpassType)
                    {
                        case 0:
                            bitModel.RunSignificancePropagationPass();
                            break;
                        case 1:
                            bitModel.RunMagnitudeRefinementPass();
                            break;
                        case 2:
                            bitModel.RunCleanupPass();
                            if (segmentationSymbolUsed)
                            {
                                bitModel.ÑheckSegmentationSymbol();
                            }
                            break;
                    }
                    currentCodingpassType = (currentCodingpassType + 1) % 3;
                }

                var offset = codeblock.tbx0_ - x0 + (codeblock.tby0_ - y0) * width;
                var sign = bitModel.coefficentsSign;
                var magnitude = bitModel.coefficentsMagnitude;
                var bitsDecoded = bitModel.bitsDecoded;
                var magnitudeCorrection = reversible ? 0 : 0.5F;
                var k = 0;
                var n = 0F;
                var nb = 0;
                position = 0;
                // Do the interleaving of Section F.3.3 here, so we do not need
                // to copy later. LL level is not interleaved, just copied.
                var interleave = subband.type != "LL";
                for (j = 0; j < blockHeight; j++)
                {
                    var row = (offset / width) | 0; // row in the non-interleaved subband
                    var levelOffset = 2 * row * (levelWidth - width) + right + bottom;
                    for (k = 0; k < blockWidth; k++)
                    {
                        n = magnitude[position];
                        if (n != 0)
                        {
                            n = (n + magnitudeCorrection) * delta;
                            if (sign[position] != 0)
                            {
                                n = -n;
                            }
                            nb = bitsDecoded[position];
                            var pos = interleave ? levelOffset + (offset << 1) : offset;
                            if (reversible && nb >= mb)
                            {
                                coefficients[pos] = n;
                            }
                            else
                            {
                                coefficients[pos] = n * (1 << (mb - nb));
                            }
                        }
                        offset++;
                        position++;
                    }
                    offset += width - blockWidth;
                }
            }
        }
        TileResultF TransformTile(Context context, Tile tile, int c)
        {
            var component = tile.components[c];
            var codingStyleParameters = component.codingStyleParameters;
            var quantizationParameters = component.quantizationParameters;
            var decompositionLevelsCount =
              codingStyleParameters.decompositionLevelsCount;
            var spqcds = quantizationParameters.SPqcds;
            var scalarExpounded = quantizationParameters.scalarExpounded;
            var guardBits = quantizationParameters.guardBits;
            var segmentationSymbolUsed = codingStyleParameters.segmentationSymbolUsed;
            var precision = context.components[c].precision;

            var reversible = codingStyleParameters.reversibleTransformation;
            Transform transform = reversible
              ? (Transform)new ReversibleTransform()
              : new IrreversibleTransform();

            var subbandCoefficients = new List<Coefficient>();
            var b = 0;
            for (var i = 0; i <= decompositionLevelsCount; i++)
            {
                var resolution = component.resolutions[i];

                var width = resolution.trx1 - resolution.trx0;
                var height = resolution.try1 - resolution.try0;
                // Allocate space for the whole sublevel.
                var coefficients = new float[width * height];
                for (int j = 0, jj = resolution.subbands.Count; j < jj; j++)
                {
                    var mu = 0;
                    var epsilon = 0;
                    if (!scalarExpounded)
                    {
                        // formula E-5
                        mu = spqcds[0].mu;
                        epsilon = spqcds[0].epsilon + (i > 0 ? 1 - i : 0);
                    }
                    else
                    {
                        mu = spqcds[b].mu;
                        epsilon = spqcds[b].epsilon;
                        b++;
                    }

                    var subband = resolution.subbands[j];
                    var gainLog2 = SubbandsGainLog2[subband.type];

                    // calculate quantization coefficient (Section E.1.1.1)
                    var delta = reversible
                      ? 1
                      : (float)Math.Pow(2, (precision + gainLog2 - epsilon) * (1 + mu / 2048));
                    var mb = guardBits + epsilon - 1;

                    // In the first resolution level, copyCoefficients will fill the
                    // whole array with coefficients. In the succeeding passes,
                    // copyCoefficients will consecutively fill in the values that belong
                    // to the interleaved positions of the HL, LH, and HH coefficients.
                    // The LL coefficients will then be interleaved in Transform.iterate().
                    CopyCoefficients(coefficients, width, height, subband, delta, mb, reversible, segmentationSymbolUsed);
                }
                subbandCoefficients.Add(new Coefficient(width, height, items: coefficients));
            }

            var result = transform.Calculate(
              subbandCoefficients,
              component.tcx0,
              component.tcy0
            );
            return new TileResultF(left: component.tcx0, top: component.tcy0, width: result.width, height: result.height, items: result.items);
        }

        List<TileResultB> TransformComponents(Context context)
        {
            var siz = context.SIZ;
            var components = context.components;
            var componentsCount = siz.Csiz;
            var resultImages = new List<TileResultB>();

            for (int i = 0, ii = context.tiles.Count; i < ii; i++)
            {
                var tile = context.tiles[i];
                var transformedTiles = new List<TileResultF>();
                for (var c = 0; c < componentsCount; c++)
                {
                    transformedTiles.Add(TransformTile(context, tile, c));
                }
                var tile0 = transformedTiles[0];
                var output = new byte[tile0.items.Length * componentsCount];
                var result = new TileResultB(
                   left: tile0.left,
                   top: tile0.top,
                   width: tile0.width,
                   height: tile0.height,
                   items: output);

                // Section G.2.2 Inverse multi component transform
                var shift = 0;
                var offset = 0F;
                var pos = 0;
                var j = 0;
                var jj = 0;
                var y0 = 0F;
                var y1 = 0F;
                var y2 = 0F;
                if (tile.codingStyleDefaultParameters.multipleComponentTransform)
                {
                    var fourComponents = componentsCount == 4;
                    var y0items = transformedTiles[0].items;
                    var y1items = transformedTiles[1].items;
                    var y2items = transformedTiles[2].items;
                    var y3items = fourComponents ? transformedTiles[3].items : null;

                    // HACK: The multiple component transform formulas below assume that
                    // all components have the same precision. With this in mind, we
                    // compute shift and offset only once.
                    shift = components[0].precision - 8;
                    offset = (128 << shift) + 0.5F;

                    var component0 = tile.components[0];
                    var alpha01 = componentsCount - 3;
                    jj = y0items.Length;
                    if (!component0.codingStyleParameters.reversibleTransformation)
                    {
                        // inverse irreversible multiple component transform
                        for (j = 0; j < jj; j++, pos += alpha01)
                        {
                            y0 = y0items[j] + offset;
                            y1 = y1items[j];
                            y2 = y2items[j];
                            output[pos++] = (byte)((int)(y0 + 1.402 * y2) >> shift);
                            output[pos++] = (byte)((int)(y0 - (0.34413 * y1 - 0.71414 * y2)) >> shift);
                            output[pos++] = (byte)((int)(y0 + 1.772 * y1) >> shift);
                        }
                    }
                    else
                    {
                        // inverse reversible multiple component transform
                        for (j = 0; j < jj; j++, pos += alpha01)
                        {
                            y0 = y0items[j] + offset;
                            y1 = y1items[j];
                            y2 = y2items[j];
                            var g = y0 - ((int)(y2 + y1) >> 2);

                            output[pos++] = (byte)((int)(g + y2) >> shift);
                            output[pos++] = (byte)((int)g >> shift);
                            output[pos++] = (byte)((int)(g + y1) >> shift);
                        }
                    }
                    if (fourComponents)
                    {
                        for (j = 0, pos = 3; j < jj; j++, pos += 4)
                        {
                            output[pos] = (byte)((int)(y3items[j] + offset) >> shift);
                        }
                    }
                }
                else
                {
                    // no multi-component transform
                    for (var c = 0; c < componentsCount; c++)
                    {
                        var items = transformedTiles[c].items;
                        shift = components[c].precision - 8;
                        offset = (128 << shift) + 0.5F;
                        for (pos = c, j = 0, jj = items.Length; j < jj; j++)
                        {
                            output[pos] = (byte)((int)(items[j] + offset) >> shift);
                            pos += componentsCount;
                        }
                    }
                }
                resultImages.Add(result);
            }
            return resultImages;
        }

        void InitializeTile(Context context, int tileIndex)
        {
            var siz = context.SIZ;
            var componentsCount = siz.Csiz;
            var tile = context.tiles[tileIndex];
            for (var c = 0; c < componentsCount; c++)
            {
                var component = tile.components[c];
                var qcdOrQcc =
                  context.currentTile.QCC.TryGetValue(c, out var quantization)
                     ? quantization
                     : context.currentTile.QCD;
                component.quantizationParameters = qcdOrQcc;
                var codOrCoc =
                  context.currentTile.COC.TryGetValue(c, out var cod)
                    ? cod
                    : context.currentTile.COD;
                component.codingStyleParameters = codOrCoc;
            }
            tile.codingStyleDefaultParameters = context.currentTile.COD;
        }

    }

    internal class CodeBlockData
    {
        internal readonly byte[] data;
        internal readonly int start;
        internal readonly int end;
        internal readonly int codingpasses;

        public CodeBlockData(byte[] data, int start, int end, int codingpasses)
        {
            this.data = data;
            this.start = start;
            this.end = end;
            this.codingpasses = codingpasses;
        }
    }

    internal class PacketItem
    {
        internal readonly CodeBlock codeblock;
        internal readonly int codingpasses;
        internal readonly int dataLength;

        public PacketItem(CodeBlock codeblock, int codingpasses, int dataLength)
        {
            this.codeblock = codeblock;
            this.codingpasses = codingpasses;
            this.dataLength = dataLength;
        }
    }

    internal class PrecinctSizes
    {
        internal readonly float minWidth;
        internal readonly float minHeight;
        internal readonly float maxNumWide;
        internal readonly float maxNumHigh;
        internal List<Size> resolutions;
        internal Dictionary<int, PrecinctSizes> components;

        public PrecinctSizes(float minWidth, float minHeight, float maxNumWide, float maxNumHigh, Dictionary<int, PrecinctSizes> components = null, List<Size> resolutions = null)
        {
            this.components = components;
            this.minWidth = minWidth;
            this.minHeight = minHeight;
            this.maxNumWide = maxNumWide;
            this.maxNumHigh = maxNumHigh;
            this.resolutions = resolutions;
        }
    }

    internal class Coefficient
    {
        internal readonly int width;
        internal readonly int height;
        internal float[] items;

        public Coefficient(int width, int height, float[] items)
        {
            this.width = width;
            this.height = height;
            this.items = items;
        }
    }

    internal class Packet
    {
        internal readonly int layerNumber;
        internal readonly List<CodeBlock> codeblocks;

        public Packet(int layerNumber, List<CodeBlock> codeblocks)
        {
            this.layerNumber = layerNumber;
            this.codeblocks = codeblocks;
        }
    }

    internal class CodeblockParameters
    {
        internal readonly int codeblockWidth;
        internal readonly int codeblockHeight;
        internal readonly int numcodeblockwide;
        internal readonly int numcodeblockhigh;

        public CodeblockParameters(int codeblockWidth, int codeblockHeight, int numcodeblockwide, int numcodeblockhigh)
        {
            this.codeblockWidth = codeblockWidth;
            this.codeblockHeight = codeblockHeight;
            this.numcodeblockwide = numcodeblockwide;
            this.numcodeblockhigh = numcodeblockhigh;
        }
    }

    internal class Precinct
    {
        internal int cbxMin;
        internal int cbyMin;
        internal int cbxMax;
        internal int cbyMax;
        internal InclusionTree inclusionTree;
        internal TagTree zeroBitPlanesTree;

        public Precinct(int cbxMin, int cbyMin, int cbxMax, int cbyMax)
        {
            this.cbxMin = cbxMin;
            this.cbyMin = cbyMin;
            this.cbxMax = cbxMax;
            this.cbyMax = cbyMax;
        }
    }

    internal class CodeBlock
    {
        internal readonly int cbx;
        internal readonly int cby;
        internal readonly int tbx0;
        internal readonly int tby0;
        internal readonly int tbx1;
        internal readonly int tby1;
        internal int tbx0_;
        internal int tby0_;
        internal int tbx1_;
        internal int tby1_;
        internal double precinctNumber;
        internal string subbandType;
        internal int Lblock;
        internal Precinct precinct;
        internal bool? included;
        internal List<CodeBlockData> data;
        internal float zeroBitPlanes;

        public CodeBlock(int cbx, int cby, int tbx0, int tby0, int tbx1, int tby1)
        {
            this.cbx = cbx;
            this.cby = cby;
            this.tbx0 = tbx0;
            this.tby0 = tby0;
            this.tbx1 = tbx1;
            this.tby1 = tby1;
        }

    }

    internal class PrecinctParameters
    {
        internal readonly int precinctWidth;
        internal readonly int precinctHeight;
        internal readonly double numprecinctswide;
        internal readonly double numprecinctshigh;
        internal readonly double numprecincts;
        internal readonly int precinctWidthInSubband;
        internal readonly int precinctHeightInSubband;

        public PrecinctParameters(int precinctWidth, int precinctHeight, double numprecinctswide, double numprecinctshigh, double numprecincts, int precinctWidthInSubband, int precinctHeightInSubband)
        {
            this.precinctWidth = precinctWidth;
            this.precinctHeight = precinctHeight;
            this.numprecinctswide = numprecinctswide;
            this.numprecinctshigh = numprecinctshigh;
            this.numprecincts = numprecincts;
            this.precinctWidthInSubband = precinctWidthInSubband;
            this.precinctHeightInSubband = precinctHeightInSubband;
        }
    }

    internal class SubBand
    {
        internal readonly string type;
        internal readonly int tbx0;
        internal readonly int tby0;
        internal readonly int tbx1;
        internal readonly int tby1;
        internal readonly Resolution resolution;
        internal CodeblockParameters codeblockParameters;
        internal List<CodeBlock> codeblocks;
        internal Dictionary<double, Precinct> precincts;

        public SubBand(string type, int tbx0, int tby0, int tbx1, int tby1, Resolution resolution)
        {
            this.type = type;
            this.tbx0 = tbx0;
            this.tby0 = tby0;
            this.tbx1 = tbx1;
            this.tby1 = tby1;
            this.resolution = resolution;
        }
    }

    internal class Resolution
    {
        internal readonly int trx0;
        internal readonly int try0;
        internal readonly int trx1;
        internal readonly int try1;
        internal readonly int resLevel;
        internal List<SubBand> subbands;
        internal PrecinctParameters precinctParameters;

        public Resolution(int trx0, int try0, int trx1, int try1, int resLevel)
        {
            this.trx0 = trx0;
            this.try0 = try0;
            this.trx1 = trx1;
            this.try1 = try1;
            this.resLevel = resLevel;
        }
    }

    internal class Dimension
    {
        internal int PPx;
        internal int PPy;
        internal int xcb_;
        internal int ycb_;

        public Dimension()
        {
        }
    }

    internal class TagTree
    {
        internal List<Level> levels;
        internal int currentLevel;
        internal float value;

        // eslint-disable-next-line no-shadow
        public TagTree(int width, int height)
        {
            var levelsLength = JpxImage.log2(Math.Max(width, height)) + 1;
            this.levels = new List<Level>();
            for (var i = 0; i < levelsLength; i++)
            {
                var level = new Level(width, height, items: new float[0]);
                this.levels.Add(level);
                width = (int)Math.Ceiling((double)width / 2);
                height = (int)Math.Ceiling((double)height / 2);
            }
        }

        public void Reset(int i, int j)
        {
            var currentLevel = 0;
            var value = 0F;
            var level = (Level)null;
            while (currentLevel < this.levels.Count)
            {
                level = this.levels[currentLevel];
                var index = i + j * level.width;
                if (level.items.Length > index)
                {
                    value = level.items[index];
                    break;
                }
                level.index = index;
                i >>= 1;
                j >>= 1;
                currentLevel++;
            }
            currentLevel--;
            level = this.levels[currentLevel];
            CheckBuffer(level);
            level.items[level.index] = value;
            this.currentLevel = currentLevel;
            // delete this.value;
        }

        private static void CheckBuffer(Level level)
        {
            if (level.items.Length < level.index + 1)
            {
                var temp = level.items;
                level.items = new float[level.index + 1];
                level.items.Set(temp, 0);
            }
        }

        public void IncrementValue()
        {
            var level = this.levels[this.currentLevel];
            level.items[level.index]++;
        }
        public bool NextLevel()
        {
            var currentLevel = this.currentLevel;
            var level = this.levels[currentLevel];
            var value = level.items[level.index];
            currentLevel--;
            if (currentLevel < 0)
            {
                this.value = value;
                return false;
            }

            this.currentLevel = currentLevel;
            level = this.levels[currentLevel];
            CheckBuffer(level);
            level.items[level.index] = value;
            return true;
        }
    }

    internal class Level
    {
        internal int width;
        internal int height;
        internal float[] items;
        internal int index;

        public Level(int width, int height, float[] items)
        {
            this.width = width;
            this.height = height;
            this.items = items;
        }
    }

    internal class InclusionTree
    {
        private List<Level> levels;
        private int currentLevel;

        // eslint-disable-next-line no-shadow
        public InclusionTree(int width, int height, float defaultValue)
        {
            var levelsLength = JpxImage.log2(Math.Max(width, height)) + 1;
            this.levels = new List<Level>();
            for (var i = 0; i < levelsLength; i++)
            {
                var items = new float[width * height];
                var jj = items.Length;
                for (var j = 0; j < jj; j++)
                {
                    items[j] = defaultValue;
                }

                var level = new Level(width, height, items);
                this.levels.Add(level);

                width = (int)Math.Ceiling(width / 2d);
                height = (int)Math.Ceiling(height / 2d);
            }
        }

        public bool Reset(int i, int j, int stopValue)
        {
            var currentLevel = 0;
            while (currentLevel < this.levels.Count)
            {
                var level = this.levels[currentLevel];
                var index = i + j * level.width;
                level.index = index;
                var value = level.items[index];

                if (value == 0xff)
                {
                    break;
                }

                if (value > stopValue)
                {
                    this.currentLevel = currentLevel;
                    // already know about this one, propagating the value to top levels
                    this.PropagateValues();
                    return false;
                }

                i >>= 1;
                j >>= 1;
                currentLevel++;
            }
            this.currentLevel = currentLevel - 1;
            return true;
        }
        public void IncrementValue(int stopValue)
        {
            var level = this.levels[this.currentLevel];
            level.items[level.index] = (byte)(stopValue + 1);
            this.PropagateValues();
        }

        public void PropagateValues()
        {
            var levelIndex = this.currentLevel;
            var level = this.levels[levelIndex];
            var currentValue = level.items[level.index];
            while (--levelIndex >= 0)
            {
                level = this.levels[levelIndex];
                level.items[level.index] = currentValue;
            }
        }
        public bool NextLevel()
        {
            var currentLevel = this.currentLevel;
            var level = this.levels[currentLevel];
            var value = level.items[level.index];
            level.items[level.index] = 0xff;
            currentLevel--;
            if (currentLevel < 0)
            {
                return false;
            }

            this.currentLevel = currentLevel;
            level = this.levels[currentLevel];
            level.items[level.index] = value;
            return true;
        }
    }

    // Section D. Coefficient bit modeling
    public class BitModel
    {
        public static readonly int UNIFORM_CONTEXT = 17;
        public static readonly int RUNLENGTH_CONTEXT = 18;
        // Table D-1
        // The index is binary presentation: 0dddvvhh, ddd - sum of Di (0..4),
        // vv - sum of Vi (0..2), and hh - sum of Hi (0..2)
        // prettier-ignore
        public static readonly byte[] LLAndLHContextsLabel = new byte[]{
      0, 5, 8, 0, 3, 7, 8, 0, 4, 7, 8, 0, 0, 0, 0, 0, 1, 6, 8, 0, 3, 7, 8, 0, 4,
      7, 8, 0, 0, 0, 0, 0, 2, 6, 8, 0, 3, 7, 8, 0, 4, 7, 8, 0, 0, 0, 0, 0, 2, 6,
      8, 0, 3, 7, 8, 0, 4, 7, 8, 0, 0, 0, 0, 0, 2, 6, 8, 0, 3, 7, 8, 0, 4, 7, 8
    };
        // prettier-ignore
        public static readonly byte[] HLContextLabel = new byte[]{
      0, 3, 4, 0, 5, 7, 7, 0, 8, 8, 8, 0, 0, 0, 0, 0, 1, 3, 4, 0, 6, 7, 7, 0, 8,
      8, 8, 0, 0, 0, 0, 0, 2, 3, 4, 0, 6, 7, 7, 0, 8, 8, 8, 0, 0, 0, 0, 0, 2, 3,
      4, 0, 6, 7, 7, 0, 8, 8, 8, 0, 0, 0, 0, 0, 2, 3, 4, 0, 6, 7, 7, 0, 8, 8, 8
    };
        // prettier-ignore
        public static readonly byte[] HHContextLabel = new byte[]{
      0, 1, 2, 0, 1, 2, 2, 0, 2, 2, 2, 0, 0, 0, 0, 0, 3, 4, 5, 0, 4, 5, 5, 0, 5,
      5, 5, 0, 0, 0, 0, 0, 6, 7, 7, 0, 7, 7, 7, 0, 7, 7, 7, 0, 0, 0, 0, 0, 8, 8,
      8, 0, 8, 8, 8, 0, 8, 8, 8, 0, 0, 0, 0, 0, 8, 8, 8, 0, 8, 8, 8, 0, 8, 8, 8
    };
        internal int width;
        internal int height;
        internal byte[] contextLabelTable;
        internal byte[] neighborsSignificance;
        internal byte[] coefficentsSign;
        internal byte[] coefficentsMagnitude;
        internal byte[] processingFlags;
        internal byte[] bitsDecoded;
        internal ArithmeticDecoder decoder;
        internal sbyte[] contexts;

        // eslint-disable-next-line no-shadow
        public BitModel(int width, int height, string subband, byte zeroBitPlanes, int mb)
        {
            this.width = width;
            this.height = height;

            byte[] contextLabelTable;
            if (subband == "HH")
            {
                contextLabelTable = HHContextLabel;
            }
            else if (subband == "HL")
            {
                contextLabelTable = HLContextLabel;
            }
            else
            {
                contextLabelTable = LLAndLHContextsLabel;
            }
            this.contextLabelTable = contextLabelTable;

            var coefficientCount = width * height;

            // coefficients outside the encoding region treated as insignificant
            // add border state cells for significanceState
            this.neighborsSignificance = new byte[coefficientCount];
            this.coefficentsSign = new byte[coefficientCount];
            //Array coefficentsMagnitude;
            //if (mb > 14)
            //{
            //    coefficentsMagnitude = new uint[coefficientCount];
            //}
            //else if (mb > 6)
            //{
            //    coefficentsMagnitude = new ushort[coefficientCount];
            //}
            //else
            //{
            //    coefficentsMagnitude = new byte[coefficientCount];
            //}
            this.coefficentsMagnitude = new byte[coefficientCount];
            this.processingFlags = new byte[coefficientCount];

            var bitsDecoded = new byte[coefficientCount];
            if (zeroBitPlanes != 0)
            {
                for (var i = 0; i < coefficientCount; i++)
                {
                    bitsDecoded[i] = zeroBitPlanes;
                }
            }
            this.bitsDecoded = bitsDecoded;

            this.Reset();
        }

        public void SetDecoder(ArithmeticDecoder decoder)
        {
            this.decoder = decoder;
        }

        public void Reset()
        {
            // We have 17 contexts that are accessed via context labels,
            // plus the uniform and runlength context.
            this.contexts = new sbyte[19];

            // Contexts are packed into 1 byte:
            // highest 7 bits carry the index, lowest bit carries mps
            this.contexts[0] = (4 << 1) | 0;
            this.contexts[UNIFORM_CONTEXT] = (46 << 1) | 0;
            this.contexts[RUNLENGTH_CONTEXT] = (3 << 1) | 0;
        }

        public void SetNeighborsSignificance(int row, int column, int index)
        {
            var neighborsSignificance = this.neighborsSignificance;
            var width = this.width;
            var height = this.height;
            var left = column > 0;
            var right = column + 1 < width;
            var i = 0;

            if (row > 0)
            {
                i = index - width;
                if (left)
                {
                    neighborsSignificance[i - 1] += 0x10;
                }
                if (right)
                {
                    neighborsSignificance[i + 1] += 0x10;
                }
                neighborsSignificance[i] += 0x04;
            }

            if (row + 1 < height)
            {
                i = index + width;
                if (left)
                {
                    neighborsSignificance[i - 1] += 0x10;
                }
                if (right)
                {
                    neighborsSignificance[i + 1] += 0x10;
                }
                neighborsSignificance[i] += 0x04;
            }

            if (left)
            {
                neighborsSignificance[index - 1] += 0x01;
            }
            if (right)
            {
                neighborsSignificance[index + 1] += 0x01;
            }
            neighborsSignificance[index] |= 0x80;
        }

        public void RunSignificancePropagationPass()
        {
            var decoder = this.decoder;
            var width = this.width;
            var height = this.height;
            var coefficentsMagnitude = this.coefficentsMagnitude;
            var coefficentsSign = this.coefficentsSign;
            var neighborsSignificance = this.neighborsSignificance;
            var processingFlags = this.processingFlags;
            var contexts = this.contexts;
            var labels = this.contextLabelTable;
            var bitsDecoded = this.bitsDecoded;
            var processedInverseMask = ~1;
            var processedMask = 1;
            var firstMagnitudeBitMask = 2;

            for (var i0 = 0; i0 < height; i0 += 4)
            {
                for (var j = 0; j < width; j++)
                {
                    var index = i0 * width + j;
                    for (var i1 = 0; i1 < 4; i1++, index += width)
                    {
                        var i = i0 + i1;
                        if (i >= height)
                        {
                            break;
                        }
                        // clear processed flag first
                        processingFlags[index] = (byte)(processingFlags[index] & processedInverseMask);

                        if (0 != coefficentsMagnitude[index] ||
                          0 == neighborsSignificance[index])
                        {
                            continue;
                        }

                        var contextLabel = labels[neighborsSignificance[index]];
                        var decision = decoder.ReadBit(contexts, contextLabel);
                        if (decision != 0)
                        {
                            var sign = this.DecodeSignBit(i, j, index);
                            coefficentsSign[index] = (byte)sign;
                            coefficentsMagnitude[index] = 1;
                            this.SetNeighborsSignificance(i, j, index);
                            processingFlags[index] = (byte)(processingFlags[index] | firstMagnitudeBitMask);
                        }
                        bitsDecoded[index]++;
                        processingFlags[index] = (byte)(processingFlags[index] | processedMask);
                    }
                }
            }
        }
        int DecodeSignBit(int row, int column, int index)
        {
            var width = this.width;
            var height = this.height;
            var coefficentsMagnitude = this.coefficentsMagnitude;
            var coefficentsSign = this.coefficentsSign;
            var contribution = 0;
            var sign0 = 0;
            var sign1 = 0;
            var significance1 = false;
            var contextLabel = 0;
            var decoded = 0;

            // calculate horizontal contribution
            significance1 = column > 0 && coefficentsMagnitude[index - 1] != 0;
            if (column + 1 < width && coefficentsMagnitude[index + 1] != 0)
            {
                sign1 = coefficentsSign[index + 1];
                if (significance1)
                {
                    sign0 = coefficentsSign[index - 1];
                    contribution = 1 - sign1 - sign0;
                }
                else
                {
                    contribution = 1 - sign1 - sign1;
                }
            }
            else if (significance1)
            {
                sign0 = coefficentsSign[index - 1];
                contribution = 1 - sign0 - sign0;
            }
            else
            {
                contribution = 0;
            }
            var horizontalContribution = 3 * contribution;

            // calculate vertical contribution and combine with the horizontal
            significance1 = row > 0 && coefficentsMagnitude[index - width] != 0;
            if (row + 1 < height && coefficentsMagnitude[index + width] != 0)
            {
                sign1 = coefficentsSign[index + width];
                if (significance1)
                {
                    sign0 = coefficentsSign[index - width];
                    contribution = 1 - sign1 - sign0 + horizontalContribution;
                }
                else
                {
                    contribution = 1 - sign1 - sign1 + horizontalContribution;
                }
            }
            else if (significance1)
            {
                sign0 = coefficentsSign[index - width];
                contribution = 1 - sign0 - sign0 + horizontalContribution;
            }
            else
            {
                contribution = horizontalContribution;
            }

            if (contribution >= 0)
            {
                contextLabel = 9 + contribution;
                decoded = this.decoder.ReadBit(this.contexts, contextLabel);
            }
            else
            {
                contextLabel = 9 - contribution;
                decoded = this.decoder.ReadBit(this.contexts, contextLabel) ^ 1;
            }
            return decoded;
        }

        public void RunMagnitudeRefinementPass()
        {
            var decoder = this.decoder;
            var width = this.width;
            var height = this.height;
            var coefficentsMagnitude = this.coefficentsMagnitude;
            var neighborsSignificance = this.neighborsSignificance;
            var contexts = this.contexts;
            var bitsDecoded = this.bitsDecoded;
            var processingFlags = this.processingFlags;
            var processedMask = 1;
            var firstMagnitudeBitMask = 2;
            var length = width * height;
            var width4 = width * 4;

            for (int index0 = 0, indexNext = 0; index0 < length; index0 = indexNext)
            {
                indexNext = Math.Min(length, index0 + width4);
                for (var j = 0; j < width; j++)
                {
                    for (var index = index0 + j; index < indexNext; index += width)
                    {
                        // significant but not those that have just become
                        if (0 == coefficentsMagnitude[index] ||
                          (processingFlags[index] & processedMask) != 0)
                        {
                            continue;
                        }

                        var contextLabel = 16;
                        if ((processingFlags[index] & firstMagnitudeBitMask) != 0)
                        {
                            processingFlags[index] = (byte)(processingFlags[index] ^ firstMagnitudeBitMask);
                            // first refinement
                            var significance = neighborsSignificance[index] & 127;
                            contextLabel = significance == 0 ? 15 : 14;
                        }

                        var bit = decoder.ReadBit(contexts, contextLabel);
                        coefficentsMagnitude[index] = (byte)((coefficentsMagnitude[index] << 1) | bit);
                        bitsDecoded[index]++;
                        processingFlags[index] = (byte)(processingFlags[index] | processedMask);
                    }
                }
            }
        }
        public void RunCleanupPass()
        {
            var decoder = this.decoder;
            var width = this.width;
            var height = this.height;
            var neighborsSignificance = this.neighborsSignificance;
            var coefficentsMagnitude = this.coefficentsMagnitude;
            var coefficentsSign = this.coefficentsSign;
            var contexts = this.contexts;
            var labels = this.contextLabelTable;
            var bitsDecoded = this.bitsDecoded;
            var processingFlags = this.processingFlags;
            var processedMask = 1;
            var firstMagnitudeBitMask = 2;
            var oneRowDown = width;
            var twoRowsDown = width * 2;
            var threeRowsDown = width * 3;
            var iNext = 0;
            for (var i0 = 0; i0 < height; i0 = iNext)
            {
                iNext = Math.Min(i0 + 4, height);
                var indexBase = i0 * width;
                var checkAllEmpty = i0 + 3 < height;
                for (var j = 0; j < width; j++)
                {
                    var index0 = indexBase + j;
                    // using the property: labels[neighborsSignificance[index]] == 0
                    // when neighborsSignificance[index] == 0
                    var allEmpty =
                      checkAllEmpty &&
                      processingFlags[index0] == 0 &&
                      processingFlags[index0 + oneRowDown] == 0 &&
                      processingFlags[index0 + twoRowsDown] == 0 &&
                      processingFlags[index0 + threeRowsDown] == 0 &&
                      neighborsSignificance[index0] == 0 &&
                      neighborsSignificance[index0 + oneRowDown] == 0 &&
                      neighborsSignificance[index0 + twoRowsDown] == 0 &&
                      neighborsSignificance[index0 + threeRowsDown] == 0;
                    var i1 = 0;
                    var index = index0;
                    var i = i0;
                    var sign = 0;
                    if (allEmpty)
                    {
                        var hasSignificantCoefficent = decoder.ReadBit(contexts, RUNLENGTH_CONTEXT);
                        if (0 == hasSignificantCoefficent)
                        {
                            bitsDecoded[index0]++;
                            bitsDecoded[index0 + oneRowDown]++;
                            bitsDecoded[index0 + twoRowsDown]++;
                            bitsDecoded[index0 + threeRowsDown]++;
                            continue; // next column
                        }
                        i1 =
                          (decoder.ReadBit(contexts, UNIFORM_CONTEXT) << 1) |
                          decoder.ReadBit(contexts, UNIFORM_CONTEXT);
                        if (i1 != 0)
                        {
                            i = i0 + i1;
                            index += i1 * width;
                        }

                        sign = this.DecodeSignBit(i, j, index);
                        coefficentsSign[index] = (byte)sign;
                        coefficentsMagnitude[index] = 1;
                        this.SetNeighborsSignificance(i, j, index);
                        processingFlags[index] = (byte)(processingFlags[index] | firstMagnitudeBitMask);

                        index = index0;
                        for (var i2 = i0; i2 <= i; i2++, index += width)
                        {
                            bitsDecoded[index]++;
                        }

                        i1++;
                    }
                    for (i = i0 + i1; i < iNext; i++, index += width)
                    {
                        if (
                          coefficentsMagnitude[index] != 0 ||
                          (processingFlags[index] & processedMask) != 0
                        )
                        {
                            continue;
                        }

                        var contextLabel = labels[neighborsSignificance[index]];
                        var decision = decoder.ReadBit(contexts, contextLabel);
                        if (decision == 1)
                        {
                            sign = this.DecodeSignBit(i, j, index);
                            coefficentsSign[index] = (byte)sign;
                            coefficentsMagnitude[index] = 1;
                            this.SetNeighborsSignificance(i, j, index);
                            processingFlags[index] = (byte)(processingFlags[index] | firstMagnitudeBitMask);
                        }
                        bitsDecoded[index]++;
                    }
                }
            }
        }
        public void ÑheckSegmentationSymbol()
        {
            var decoder = this.decoder;
            var contexts = this.contexts;
            var symbol =
              (decoder.ReadBit(contexts, UNIFORM_CONTEXT) << 3) |
              (decoder.ReadBit(contexts, UNIFORM_CONTEXT) << 2) |
              (decoder.ReadBit(contexts, UNIFORM_CONTEXT) << 1) |
              decoder.ReadBit(contexts, UNIFORM_CONTEXT);
            if (symbol != 0xa)
            {
                throw new JpxError("Invalid segmentation symbol");
            }
        }
    }

    // Section F, Discrete wavelet transformation
    internal class Transform
    {
        // eslint-disable-next-line no-shadow
        public Transform() { }

        public Coefficient Calculate(List<Coefficient> subbands, int u0, int v0)
        {
            var ll = subbands[0];
            for (int i = 1, ii = subbands.Count; i < ii; i++)
            {
                ll = this.Iterate(ll, subbands[i], u0, v0);
            }
            return ll;
        }

        public void Extend(float[] buffer, int offset, int size)
        {
            // Section F.3.7 extending... using max extension of 4
            var i1 = offset - 1;
            var j1 = offset + 1;
            var i2 = offset + size - 2;
            var j2 = offset + size;
            buffer[i1--] = buffer[j1++];
            buffer[j2++] = buffer[i2--];
            buffer[i1--] = buffer[j1++];
            buffer[j2++] = buffer[i2--];
            buffer[i1--] = buffer[j1++];
            buffer[j2++] = buffer[i2--];
            buffer[i1] = buffer[j1];
            buffer[j2] = buffer[i2];
        }

        public virtual Coefficient Iterate(Coefficient ll, Coefficient hl_lh_hh, int u0, int v0)
        {
            var llWidth = ll.width;
            var llHeight = ll.height;
            var llItems = ll.items;
            var width = hl_lh_hh.width;
            var height = hl_lh_hh.height;
            var items = hl_lh_hh.items;
            var i = 0;
            var j = 0;
            var k = 0;
            var l = 0;
            var u = 0;
            var v = 0;

            // Interleave LL according to Section F.3.3
            for (k = 0, i = 0; i < llHeight; i++)
            {
                l = i * 2 * width;
                for (j = 0; j < llWidth; j++, k++, l += 2)
                {
                    items[l] = llItems[k];
                }
            }
            // The LL band is not needed anymore.
            llItems = ll.items = null;

            var bufferPadding = 4;
            var rowBuffer = new float[width + 2 * bufferPadding];

            // Section F.3.4 HOR_SR
            if (width == 1)
            {
                // if width = 1, when u0 even keep items as is, when odd divide by 2
                if ((u0 & 1) != 0)
                {
                    for (v = 0, k = 0; v < height; v++, k += width)
                    {
                        items[k] = (byte)(items[k] * 0.5F);
                    }
                }
            }
            else
            {
                for (v = 0, k = 0; v < height; v++, k += width)
                {
                    var sub1 = items.CopyOfRange(k, k + width);
                    rowBuffer.Set(sub1, bufferPadding);

                    this.Extend(rowBuffer, bufferPadding, width);
                    this.Filter(rowBuffer, bufferPadding, width);

                    var sub2 = rowBuffer.CopyOfRange(bufferPadding, bufferPadding + width);
                    items.Set(sub2, k);
                }
            }

            // Accesses to the items array can take long, because it may not fit into
            // CPU cache and has to be fetched from main memory. Since subsequent
            // accesses to the items array are not local when reading columns, we
            // have a cache miss every time. To reduce cache misses, get up to
            // 'numBuffers' items at a time and store them into the individual
            // buffers. The colBuffers should be small enough to fit into CPU cache.
            var numBuffers = 16;
            var colBuffers = new List<float[]>();
            for (i = 0; i < numBuffers; i++)
            {
                colBuffers.Add(new float[height + 2 * bufferPadding]);
            }
            var b = 0;
            var currentBuffer = 0;
            var llc = bufferPadding + height;

            // Section F.3.5 VER_SR
            if (height == 1)
            {
                // if height = 1, when v0 even keep items as is, when odd divide by 2
                if ((v0 & 1) != 0)
                {
                    for (u = 0; u < width; u++)
                    {
                        items[u] *= 0.5F;
                    }
                }
            }
            else
            {
                for (u = 0; u < width; u++)
                {
                    // if we ran out of buffers, copy several image columns at once
                    if (currentBuffer == 0)
                    {
                        numBuffers = Math.Min(width - u, numBuffers);
                        for (k = u, l = bufferPadding; l < llc; k += width, l++)
                        {
                            for (b = 0; b < numBuffers; b++)
                            {
                                colBuffers[b][l] = items[k + b];
                            }
                        }
                        currentBuffer = numBuffers;
                    }

                    currentBuffer--;
                    var buffer = colBuffers[currentBuffer];
                    this.Extend(buffer, bufferPadding, height);
                    this.Filter(buffer, bufferPadding, height);

                    // If this is last buffer in this group of buffers, flush all buffers.
                    if (currentBuffer == 0)
                    {
                        k = u - numBuffers + 1;
                        for (l = bufferPadding; l < llc; k += width, l++)
                        {
                            for (b = 0; b < numBuffers; b++)
                            {
                                items[k + b] = colBuffers[b][l];
                            }
                        }
                    }
                }
            }

            return new Coefficient(width, height, items);
        }

        public virtual void Filter(float[] x, int offset, int length)
        { }
    }

    // Section 3.8.2 Irreversible 9-7 filter
    internal class IrreversibleTransform : Transform
    {
        // eslint-disable-next-line no-shadow
        public IrreversibleTransform()
        {
            //call(this);
        }


        public override void Filter(float[] x, int offset, int length)
        {
            var len = length >> 1;
            offset = offset | 0;
            var j = 0;
            var n = 0;
            var current = 0D;
            var next = 0D;

            var alpha = -1.586134342059924D;
            var beta = -0.052980118572961D;
            var gamma = 0.882911075530934D;
            var delta = 0.443506852043971D;
            var K = 1.230174104914001D;
            var K_ = 1 / K;

            // step 1 is combined with step 3

            // step 2
            j = offset - 3;
            for (n = len + 4; n-- != 0; j += 2)
            {
                x[j] = (float)(x[j] * K_);
            }

            // step 1 & 3
            j = offset - 2;
            current = delta * x[j - 1];
            for (n = len + 3; n-- != 0; j += 2)
            {
                next = delta * x[j + 1];
                x[j] = (float)(K * x[j] - current - next);
                if (n-- != 0)
                {
                    j += 2;
                    current = delta * x[j + 1];
                    x[j] = (float)(K * x[j] - current - next);
                }
                else
                {
                    break;
                }
            }

            // step 4
            j = offset - 1;
            current = gamma * x[j - 1];
            for (n = len + 2; n-- != 0; j += 2)
            {
                next = gamma * x[j + 1];
                x[j] = (float)(x[j] - (current + next));
                if (n-- != 0)
                {
                    j += 2;
                    current = gamma * x[j + 1];
                    x[j] = (float)(x[j] - (current + next));
                }
                else
                {
                    break;
                }
            }

            // step 5
            j = offset;
            current = beta * x[j - 1];
            for (n = len + 1; n-- != 0; j += 2)
            {
                next = beta * x[j + 1];
                x[j] = (float)(x[j] - (current + next));
                if (n-- != 0)
                {
                    j += 2;
                    current = beta * x[j + 1];
                    x[j] = (float)(x[j] - (current + next));
                }
                else
                {
                    break;
                }
            }

            // step 6
            if (len != 0)
            {
                j = offset + 1;
                current = alpha * x[j - 1];
                for (n = len; n-- != 0; j += 2)
                {
                    next = alpha * x[j + 1];
                    x[j] = (float)(x[j] - (current + next));
                    if (n-- != 0)
                    {
                        j += 2;
                        current = alpha * x[j + 1];
                        x[j] = (float)(x[j] - (current + next));
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }

    // Section 3.8.1 Reversible 5-3 filter
    internal class ReversibleTransform : Transform
    {
        // eslint-disable-next-line no-shadow
        public ReversibleTransform()
        {
            //call(this);
        }

        public override void Filter(float[] x, int offset, int length)
        {
            var len = length >> 1;
            offset = offset | 0;
            var j = 0;
            var n = 0;

            for (j = offset, n = len + 1; n-- != 0; j += 2)
            {
                x[j] -= (int)(x[j - 1] + x[j + 1] + 2) >> 2;
            }

            for (j = offset + 1, n = len; n-- != 0; j += 2)
            {
                x[j] += (int)(x[j - 1] + x[j + 1]) >> 1;
            }
        }

    }

    //Classes

    internal class Component
    {
        internal int precision;
        internal bool isSigned;
        internal byte XRsiz;
        internal byte YRsiz;
        internal int x0;
        internal int x1;
        internal int y0;
        internal int y1;
        internal int width;
        internal int height;
        internal int tcx0;
        internal int tcy0;
        internal int tcx1;
        internal int tcy1;
        internal List<Resolution> resolutions;
        internal List<SubBand> subbands;
        internal Quantization quantizationParameters;
        internal Cod codingStyleParameters;

        public Component()
        {
        }

        public Component(int precision, bool isSigned, byte XRsiz, byte YRsiz)
        {
            this.precision = precision;
            this.isSigned = isSigned;
            this.XRsiz = XRsiz;
            this.YRsiz = YRsiz;
        }
    }

    internal class TileResultF
    {
        internal readonly int left;
        internal readonly int top;
        internal readonly int width;
        internal readonly int height;
        internal readonly float[] items;

        public TileResultF(int left, int top, int width, int height, float[] items)
        {
            this.left = left;
            this.top = top;
            this.width = width;
            this.height = height;
            this.items = items;
        }
    }

    internal class TileResultB
    {
        internal readonly int left;
        internal readonly int top;
        internal readonly int width;
        internal readonly int height;
        internal readonly byte[] items;

        public TileResultB(int left, int top, int width, int height, byte[] items)
        {
            this.left = left;
            this.top = top;
            this.width = width;
            this.height = height;
            this.items = items;
        }
    }

    internal class Tile
    {
        internal int tx0;
        internal int ty0;
        internal int tx1;
        internal int ty1;
        internal int width;
        internal int height;
        internal Dictionary<int, Component> components;
        internal ushort index;
        internal int length;
        internal int dataEnd;
        internal byte partIndex;
        internal byte partsCount;
        internal Quantization QCD;
        internal Cod COD;
        internal Dictionary<int, Quantization> QCC;
        internal Dictionary<int, Cod> COC;
        internal JpxImage.Iterator packetsIterator;
        internal Cod codingStyleDefaultParameters;

        public Tile()
        {
        }

        public Tile(ushort index, int length, byte partIndex, byte partsCount)
        {
            this.index = index;
            this.length = length;
            this.partIndex = partIndex;
            this.partsCount = partsCount;
        }


    }

    internal class Context
    {
        internal bool mainHeader;
        internal SIZ SIZ;
        internal List<Component> components;
        internal List<Tile> tiles;
        internal Tile currentTile;
        internal Quantization QCD;
        internal Cod COD;
        internal Dictionary<int, Quantization> QCC;
        internal Dictionary<int, Cod> COC;

        public Context()
        {
        }
    }

    internal class Size
    {
        internal readonly int width;
        internal readonly int height;

        public Size(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    internal class PrecinctSize
    {
        private readonly int pPx;
        private readonly int pPy;

        public PrecinctSize(int PPx, int PPy)
        {
            pPx = PPx;
            pPy = PPy;
        }

        public int PPx => pPx;
        public int PPy => pPy;

    }

    internal class Quantization
    {
        internal bool noQuantization;
        internal int guardBits;
        internal bool scalarExpounded;
        internal List<EpsilonMU> SPqcds;

        public Quantization()
        {
        }
    }

    internal class Cod
    {
        //TODO Enum
        //{
        internal bool entropyCoderWithCustomPrecincts;
        internal bool sopMarkerUsed;
        internal bool ephMarkerUsed;
        //}

        internal byte progressionOrder;
        internal ushort layersCount;
        internal bool multipleComponentTransform;
        internal byte decompositionLevelsCount;
        internal int xcb;
        internal int ycb;
        //TODO Enum
        //{
        internal bool selectiveArithmeticCodingBypass;
        internal bool resetContextProbabilities;
        internal bool terminationOnEachCodingPass;
        internal bool verticallyStripe;
        internal bool predictableTermination;
        internal bool segmentationSymbolUsed;
        //}
        internal bool reversibleTransformation;
        internal List<PrecinctSize> precinctsSizes;



        public Cod()
        {
        }
    }

    internal class EpsilonMU
    {
        internal int epsilon;
        internal int mu;

        public EpsilonMU()
        {
        }
    }

    internal class SubbandsGainLog
    {
        internal int LL;
        internal int LH;
        internal int HL;
        internal int HH;

        public SubbandsGainLog(int LL, int LH, int HL, int HH)
        {
            this.LL = LL;
            this.LH = LH;
            this.HL = HL;
            this.HH = HH;
        }
    }

    internal class SIZ
    {
        internal ushort Csiz;
        internal uint Xsiz;
        internal uint Ysiz;
        internal uint XOsiz;
        internal uint YOsiz;
        internal uint XTsiz;
        internal uint YTsiz;
        internal uint XTOsiz;
        internal uint YTOsiz;

        public SIZ(uint Xsiz, uint Ysiz, uint XOsiz, uint YOsiz, uint XTsiz, uint YTsiz, uint XTOsiz, uint YTOsiz)
        {
            this.Xsiz = Xsiz;
            this.Ysiz = Ysiz;
            this.XOsiz = XOsiz;
            this.YOsiz = YOsiz;
            this.XTsiz = XTsiz;
            this.YTsiz = YTsiz;
            this.XTOsiz = XTOsiz;
            this.YTOsiz = YTOsiz;
        }
    }
}