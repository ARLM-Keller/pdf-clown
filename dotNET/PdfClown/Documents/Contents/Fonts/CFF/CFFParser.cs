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
using PdfClown.Tokens;
using PdfClown.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfClown.Documents.Contents.Fonts.CCF
{
    /**
     * This class represents a parser for a CFF font. 
     * //@author Villu Ruusmann
     */
    public class CFFParser
    {
        private static readonly List<float> DefaultMatrixArray = new List<float>(6) { 0.001F, 0F, 0F, 0.001F, 0F, 0F };
        private static readonly List<float> DefaultBBoxArray = new List<float>(4) { 0, 0, 0, 0 };
        private const string TAG_OTTO = "OTTO";
        private const string TAG_TTCF = "ttcf";
        private const string TAG_TTFONLY = "\u0000\u0001\u0000\u0000";
        private const string KeySubRS = "Subrs";
        private string[] stringIndex = null;
        private IByteSource source;

        // for debugging only
        private string debugFontName;

        /**
		 * Source from which bytes may be read in the future.
		 */
        public interface IByteSource
        {
            /**
             * Returns the source bytes. May be called more than once.
             */
            Memory<byte> GetBytes();
        }

        /**
		 * Parse CFF font using byte array, also passing in a byte source for future use.
		 * 
		 * //@param bytes source bytes
		 * //@param source source to re-read bytes from in the future
		 * //@return the parsed CFF fonts
		 * //@throws IOException If there is an error reading from the stream
		 */
        public List<CFFFont> Parse(Memory<byte> bytes, IByteSource source) => Parse(new ByteStream(bytes), source);

        public List<CFFFont> Parse(Memory<byte> bytes) => Parse(new ByteStream(bytes));

        public List<CFFFont> Parse(IInputStream input, IByteSource source)
        {
            this.source = source;
            return Parse(input);
        }
        /**
		 * Parse CFF font using a byte array as input.
		 * 
		 * //@param bytes the given byte array
		 * //@return the parsed CFF fonts
		 * //@throws IOException If there is an error reading from the stream
		 */
        public List<CFFFont> Parse(IInputStream input)
        {
            string firstTag = ReadTagName(input);
            // try to determine which kind of font we have
            switch (firstTag)
            {
                case TAG_OTTO:
                    input = CreateTaggedCFFStream(input);
                    break;
                case TAG_TTCF:
                    throw new IOException("True Type Collection fonts are not supported.");
                case TAG_TTFONLY:
                    throw new IOException("OpenType fonts containing a true type font are not supported.");
                default:
                    input.Position = 0;
                    break;
            }

            //@SuppressWarnings("unused")
            var header = ReadHeader(input);
            var nameIndex = ReadStringIndexData(input);
            if (nameIndex.Length == 0)
            {
                throw new IOException("Name index missing in CFF font");
            }
            var topDictIndex = ReadIndexData(input);
            if (topDictIndex.Length == 0)
            {
                throw new IOException("Top DICT INDEX missing in CFF font");
            }

            stringIndex = ReadStringIndexData(input);
            var globalSubrIndex = ReadIndexData(input);

            var fonts = new List<CFFFont>(nameIndex.Length);
            for (int i = 0; i < nameIndex.Length; i++)
            {
                CFFFont font = ParseFont(input, nameIndex[i], topDictIndex[i]);
                font.GlobalSubrIndex = globalSubrIndex;
                font.SetData(source);
                fonts.Add(font);
            }
            return fonts;
        }

        private IInputStream CreateTaggedCFFStream(IInputStream input)
        {
            // this is OpenType font containing CFF data
            // so find CFF tag
            short numTables = input.ReadInt16();
            short searchRange = input.ReadInt16();
            short entrySelector = input.ReadInt16();
            short rangeShift = input.ReadInt16();
            for (int q = 0; q < numTables; q++)
            {
                string tagName = ReadTagName(input);
                //@SuppressWarnings("unused")
                long checksum = ReadLong(input);
                long offset = ReadLong(input);
                long Length = ReadLong(input);
                if ("CFF ".Equals(tagName, StringComparison.Ordinal))
                {
                    var bytes2 = input.AsMemory().Slice((int)offset, (int)Length);
                    return new ByteStream(bytes2);
                }
            }
            throw new IOException("CFF tag not found in this OpenType font.");
        }

        private static string ReadTagName(IInputStream input)
        {
            return input.ReadString(4, Charset.ISO88591);
        }

        private static long ReadLong(IInputStream input)
        {
            return (input.ReadUInt16() << 16) | input.ReadUInt16();
        }

        private static Header ReadHeader(IInputStream input)
        {
            return new Header
            {
                major = input.ReadUByte(),
                minor = input.ReadUByte(),
                hdrSize = input.ReadUByte(),
                offSize = input.ReadUByte()
            };
        }

        private static int[] ReadIndexDataOffsets(IInputStream input)
        {
            int count = input.ReadUInt16();
            if (count == 0)
            {
                return Array.Empty<int>();
            }
            int offSize = input.ReadUByte();
            int[] offsets = new int[count + 1];
            for (int i = 0; i <= count; i++)
            {
                int offset = input.ReadOffset(offSize);
                if (offset > input.Length)
                {
                    throw new IOException("illegal offset value " + offset + " in CFF font");
                }
                offsets[i] = offset;
            }
            return offsets;
        }

        private static Memory<byte>[] ReadIndexData(IInputStream input)
        {
            int[] offsets = ReadIndexDataOffsets(input);
            if (offsets.Length == 0)
            {
                return Array.Empty<Memory<byte>>();
            }
            int count = offsets.Length - 1;
            var indexDataValues = new Memory<byte>[count];
            for (int i = 0; i < count; i++)
            {
                int length = offsets[i + 1] - offsets[i];
                indexDataValues[i] = input.ReadMemory(length);
            }
            return indexDataValues;
        }

        private static string[] ReadStringIndexData(IInputStream input)
        {
            int[] offsets = ReadIndexDataOffsets(input);
            if (offsets.Length == 0)
            {
                return Array.Empty<string>();
            }
            int count = offsets.Length - 1;
            string[] indexDataValues = new string[count];
            for (int i = 0; i < count; i++)
            {
                int Length = offsets[i + 1] - offsets[i];
                if (Length < 0)
                {
                    throw new IOException("Negative index data Length + " + Length + " at " +
                            i + ": offsets[" + (i + 1) + "]=" + offsets[i + 1] +
                            ", offsets[" + i + "]=" + offsets[i]);
                }
                indexDataValues[i] = input.ReadString(Length, Charset.ISO88591);
            }
            return indexDataValues;
        }

        private static DictData ReadDictData(IInputStream input)
        {
            DictData dict = new DictData();
            while (input.HasRemaining())
            {
                dict.Add(ReadEntry(input));
            }
            return dict;
        }

        private static DictData ReadDictData(IInputStream input, int offset, int dictSize)
        {
            DictData dict = new DictData();
            if (dictSize > 0)
            {
                input.Seek(offset);
                int endPosition = offset + dictSize;
                while (input.Position < endPosition)
                {
                    dict.Add(ReadEntry(input));
                }
            }
            return dict;
        }

        private static DictData.Entry ReadEntry(IInputStream input)
        {
            var entry = new DictData.Entry();
            while (true)
            {
                byte b0 = input.ReadUByte();

                if (b0 >= 0 && b0 <= 21)
                {
                    entry.operatorName = ReadOperator(input, b0);
                    break;
                }
                else if (b0 == 28 || b0 == 29)
                {
                    entry.Operands.Add(ReadIntegerNumber(input, b0));
                }
                else if (b0 == 30)
                {
                    entry.Operands.Add(ReadRealNumber(input));
                }
                else if (b0 >= 32 && b0 <= 254)
                {
                    entry.Operands.Add(ReadIntegerNumber(input, b0));
                }
                else
                {
                    Debug.WriteLine("invalid DICT data b0 byte: " + b0);
                    break;
                }
            }
            return entry;
        }

        private static string ReadOperator(IInputStream input, byte b0)
        {
            if (b0 == 12)
            {
                byte b1 = input.ReadUByte();
                return CFFOperator.GetOperator(b0, b1);
            }
            return CFFOperator.GetOperator(b0);
        }

        private static int ReadIntegerNumber(IInputStream input, byte b0)
        {
            if (b0 == 28)
            {
                return (int)input.ReadInt16();
            }
            else if (b0 == 29)
            {
                return input.ReadInt32();
            }
            else if (b0 >= 32 && b0 <= 246)
            {
                return b0 - 139;
            }
            else if (b0 >= 247 && b0 <= 250)
            {
                int b1 = input.ReadUByte();
                return (b0 - 247) * 256 + b1 + 108;
            }
            else if (b0 >= 251 && b0 <= 254)
            {
                int b1 = input.ReadUByte();
                return -(b0 - 251) * 256 - b1 - 108;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        /**
         * //@param b0  
         */
        private static float ReadRealNumber(IInputStream input)
        {
            var sb = new StringBuilder();
            bool done = false;
            bool exponentMissing = false;
            bool hasExponent = false;
            Span<int> nibbles = stackalloc int[2];
            while (!done)
            {
                byte b = input.ReadUByte();
                nibbles[0] = b / 16;
                nibbles[1] = b % 16;
                foreach (int nibble in nibbles)
                {
                    switch (nibble)
                    {
                        case 0x0:
                        case 0x1:
                        case 0x2:
                        case 0x3:
                        case 0x4:
                        case 0x5:
                        case 0x6:
                        case 0x7:
                        case 0x8:
                        case 0x9:
                            sb.Append(nibble);
                            exponentMissing = false;
                            break;
                        case 0xa:
                            sb.Append('.');
                            break;
                        case 0xb:
                            if (hasExponent)
                            {
                                Debug.WriteLine("warn: duplicate 'E' ignored after " + sb);
                                break;
                            }
                            sb.Append('E');
                            exponentMissing = true;
                            hasExponent = true;
                            break;
                        case 0xc:
                            if (hasExponent)
                            {
                                Debug.WriteLine("warn: duplicate 'E-' ignored after " + sb);
                                break;
                            }
                            sb.Append('E').Append('-');
                            exponentMissing = true;
                            hasExponent = true;
                            break;
                        case 0xd:
                            break;
                        case 0xe:
                            sb.Append('-');
                            break;
                        case 0xf:
                            done = true;
                            break;
                        default:
                            throw new ArgumentException();
                    }
                }
            }
            if (exponentMissing)
            {
                // the exponent is missing, just Append "0" to avoid an exception
                // not sure if 0 is the correct value, but it seems to fit
                // see PDFBOX-1522
                sb.Append('0');
            }
            if (sb.Length > 1
                && sb[^1] == '-')
            {
                sb.Length--;
            }
            var text = sb.ToString();
            if (text.Length == 0 || text == "E-0")
            {
                return 0f;
            }

            try
            {
                return float.Parse(sb.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private CFFFont ParseFont(IInputStream input, string name, Memory<byte> topDictIndex)
        {
            // top dict
            var topDictInput = new ByteStream(topDictIndex);
            var topDict = ReadDictData(topDictInput);

            // we don't support synthetic fonts
            var syntheticBaseEntry = topDict.GetEntry("SyntheticBase");
            if (syntheticBaseEntry != null)
            {
                throw new IOException("Synthetic Fonts are not supported");
            }

            // determine if this is a Type 1-equivalent font or a CIDFont
            CFFFont font;
            bool isCIDFont = topDict.GetEntry("ROS") != null;
            if (isCIDFont)
            {
                var rosEntry = topDict.GetEntry("ROS");
                var cidFont = new CFFCIDFont
                {
                    Registry = ReadString((int)rosEntry.GetNumber(0)),
                    Ordering = ReadString((int)rosEntry.GetNumber(1)),
                    Supplement = (int)rosEntry.GetNumber(2)
                };
                font = cidFont;
            }
            else
            {
                font = new CFFType1Font();
            }

            // name
            debugFontName = name;
            font.FontName = name;

            // top dict
            font.AddValueToTopDict("version", GetString(topDict, "version"));
            font.AddValueToTopDict("Notice", GetString(topDict, "Notice"));
            font.AddValueToTopDict("Copyright", GetString(topDict, "Copyright"));
            font.AddValueToTopDict("FullName", GetString(topDict, "FullName"));
            font.AddValueToTopDict("FamilyName", GetString(topDict, "FamilyName"));
            font.AddValueToTopDict("Weight", GetString(topDict, "Weight"));
            font.AddValueToTopDict("isFixedPitch", topDict.GetBoolean("isFixedPitch", false));
            font.AddValueToTopDict("ItalicAngle", topDict.GetNumber("ItalicAngle", 0));
            font.AddValueToTopDict("UnderlinePosition", topDict.GetNumber("UnderlinePosition", -100));
            font.AddValueToTopDict("UnderlineThickness", topDict.GetNumber("UnderlineThickness", 50));
            font.AddValueToTopDict("PaintType", topDict.GetNumber("PaintType", 0));
            font.AddValueToTopDict("CharstringType", topDict.GetNumber("CharstringType", 2));
            font.AddValueToTopDict("FontMatrix", topDict.GetArray("FontMatrix", DefaultMatrixArray));
            font.AddValueToTopDict("UniqueID", topDict.GetNumber("UniqueID", null));
            font.AddValueToTopDict("FontBBox", topDict.GetArray("FontBBox", DefaultBBoxArray));
            font.AddValueToTopDict("StrokeWidth", topDict.GetNumber("StrokeWidth", 0));
            font.AddValueToTopDict("XUID", topDict.GetArray("XUID", null));

            // charstrings index
            var charStringsEntry = topDict.GetEntry("CharStrings");
            int charStringsOffset = (int)charStringsEntry.GetNumber(0);
            input.Position = charStringsOffset;
            var charStringsIndex = ReadIndexData(input);

            // charset
            var charsetEntry = topDict.GetEntry("charset");
            CFFCharset charset;
            if (charsetEntry != null)
            {
                int charsetId = (int)charsetEntry.GetNumber(0);
                if (!isCIDFont && charsetId == 0)
                {
                    charset = CFFISOAdobeCharset.Instance;
                }
                else if (!isCIDFont && charsetId == 1)
                {
                    charset = CFFExpertCharset.Instance;
                }
                else if (!isCIDFont && charsetId == 2)
                {
                    charset = CFFExpertSubsetCharset.Instance;
                }
                else if (charStringsIndex.Length > 0)
                {
                    input.Position = charsetId;
                    charset = ReadCharset(input, charStringsIndex.Length, isCIDFont);
                }
                // that should not happen
                else
                {
                    Debug.WriteLine("debug: Couldn't read CharStrings index - returning empty charset instead");
                    charset = new EmptyCharsetType1();
                }

            }
            else
            {
                if (isCIDFont)
                {
                    // a CID font with no charset does not default to any predefined charset
                    charset = new EmptyCharsetCID(charStringsIndex.Length);
                }
                else
                {
                    charset = CFFISOAdobeCharset.Instance;
                }
            }
            font.Charset = charset;

            // charstrings dict
            font.CharStringBytes = charStringsIndex;

            // format-specific dictionaries
            if (isCIDFont)
            {

                // CharStrings index could be null if the index data couldn't be read
                int numEntries = 0;
                if (charStringsIndex.Length == 0)
                {
                    Debug.WriteLine("debug: Couldn't read CharStrings index - parsing CIDFontDicts with number of char strings set to 0");
                }
                else
                {
                    numEntries = charStringsIndex.Length;
                }

                ParseCIDFontDicts(input, topDict, (CFFCIDFont)font, numEntries);

                List<float> privMatrix = null;
                List<Dictionary<string, object>> fontDicts = ((CFFCIDFont)font).FontDicts;
                if (fontDicts.Count > 0 && fontDicts[0].ContainsKey("FontMatrix"))
                {
                    privMatrix = (List<float>)fontDicts[0]["FontMatrix"];
                }
                // some malformed fonts have FontMatrix in their Font DICT, see PDFBOX-2495
                List<float> matrix = topDict.GetArray("FontMatrix", null);
                if (matrix == null)
                {
                    if (privMatrix != null)
                    {
                        font.AddValueToTopDict("FontMatrix", privMatrix);
                    }
                    else
                    {
                        // default
                        font.AddValueToTopDict("FontMatrix", topDict.GetArray("FontMatrix", DefaultMatrixArray));
                    }
                }
                else if (privMatrix != null)
                {
                    // we have to multiply the font matrix from the top directory with the font matrix
                    // from the private directory. This should be done for synthetic fonts only but in
                    // case of PDFBOX-3579 it's needed as well to get the right scaling
                    concatenateMatrix(matrix, privMatrix);
                }

            }
            else
            {
                ParseType1Dicts(input, topDict, (CFFType1Font)font, charset);
            }

            return font;
        }

        private void concatenateMatrix(List<float> matrixDest, List<float> matrixConcat)
        {
            // concatenate matrices
            // (a b 0)
            // (c d 0)
            // (x y 1)
            double a1 = matrixDest[0];
            double b1 = matrixDest[1];
            double c1 = matrixDest[2];
            double d1 = matrixDest[3];
            double x1 = matrixDest[4];
            double y1 = matrixDest[5];

            double a2 = matrixConcat[0];
            double b2 = matrixConcat[1];
            double c2 = matrixConcat[2];
            double d2 = matrixConcat[3];
            double x2 = matrixConcat[4];
            double y2 = matrixConcat[5];

            matrixDest[0] = (float)(a1 * a2 + b1 * c2);
            matrixDest[1] = (float)(a1 * b2 + b1 * d1);
            matrixDest[2] = (float)(c1 * a2 + d1 * c2);
            matrixDest[3] = (float)(c1 * b2 + d1 * d2);
            matrixDest[4] = (float)(x1 * a2 + y1 * c2 + x2);
            matrixDest[5] = (float)(x1 * b2 + y1 * d2 + y2);
        }

        /**
         * Parse dictionaries specific to a CIDFont.
         */
        private void ParseCIDFontDicts(IInputStream input, DictData topDict, CFFCIDFont font, int nrOfcharStrings)
        {
            // In a CIDKeyed Font, the Private dictionary isn't in the Top Dict but in the Font dict
            // which can be accessed by a lookup using FDArray and FDSelect
            DictData.Entry fdArrayEntry = topDict.GetEntry("FDArray");
            if (fdArrayEntry == null)
            {
                throw new IOException("FDArray is missing for a CIDKeyed Font.");
            }

            // font dict index
            int fontDictOffset = (int)fdArrayEntry.GetNumber(0);
            input.Position = fontDictOffset;
            var fdIndex = ReadIndexData(input);
            if (fdIndex.Length == 0)
            {
                throw new IOException("Font dict index is missing for a CIDKeyed Font");
            }

            List<Dictionary<string, object>> privateDictionaries = new List<Dictionary<string, object>>();
            List<Dictionary<string, object>> fontDictionaries = new List<Dictionary<string, object>>();

            foreach (var bytes in fdIndex)
            {
                var fontDictInput = new ByteStream(bytes);
                DictData fontDict = ReadDictData(fontDictInput);

                // read private dict
                DictData.Entry privateEntry = fontDict.GetEntry("Private");
                if (privateEntry == null)
                {
                    throw new IOException("Font DICT invalid without \"Private\" entry");
                }

                // font dict
                Dictionary<string, object> fontDictMap = new Dictionary<string, object>(4, StringComparer.Ordinal);
                fontDictMap["FontName"] = GetString(fontDict, "FontName");
                fontDictMap["FontType"] = fontDict.GetNumber("FontType", 0);
                fontDictMap["FontBBox"] = fontDict.GetArray("FontBBox", null);
                fontDictMap["FontMatrix"] = fontDict.GetArray("FontMatrix", null);
                // TODO OD-4 : Add here other keys
                fontDictionaries.Add(fontDictMap);

                int privateOffset = (int)privateEntry.GetNumber(1);
                int privateSize = (int)privateEntry.GetNumber(0);
                DictData privateDict = ReadDictData(input, privateOffset, privateSize);

                // populate private dict
                Dictionary<string, object> privDict = ReadPrivateDict(privateDict);
                privateDictionaries.Add(privDict);

                // local subrs
                int localSubrOffset = (int)privateDict.GetNumber(KeySubRS, 0);
                if (localSubrOffset > 0)
                {
                    input.Position = privateOffset + localSubrOffset;
                    privDict[KeySubRS] = ReadIndexData(input);
                }
            }

            // font-dict (FD) select
            DictData.Entry fdSelectEntry = topDict.GetEntry("FDSelect");
            int fdSelectPos = (int)fdSelectEntry.GetNumber(0);
            input.Position = fdSelectPos;
            FDSelect fdSelect = ReadFDSelect(input, nrOfcharStrings);

            // TODO almost certainly erroneous - CIDFonts do not have a top-level private dict
            // font.addValueToPrivateDict("defaultWidthX", 1000);
            // font.addValueToPrivateDict("nominalWidthX", 0);

            font.FontDicts = fontDictionaries;
            font.PrivDicts = privateDictionaries;
            font.FdSelect = fdSelect;
        }

        private Dictionary<string, object> ReadPrivateDict(DictData privateDict)
        {
            Dictionary<string, object> privDict = new Dictionary<string, object>(17, StringComparer.Ordinal);
            privDict["BlueValues"] = privateDict.GetDelta("BlueValues", null);
            privDict["OtherBlues"] = privateDict.GetDelta("OtherBlues", null);
            privDict["FamilyBlues"] = privateDict.GetDelta("FamilyBlues", null);
            privDict["FamilyOtherBlues"] = privateDict.GetDelta("FamilyOtherBlues", null);
            privDict["BlueScale"] = privateDict.GetNumber("BlueScale", 0.039625f);
            privDict["BlueShift"] = privateDict.GetNumber("BlueShift", 7);
            privDict["BlueFuzz"] = privateDict.GetNumber("BlueFuzz", 1);
            privDict["StdHW"] = privateDict.GetNumber("StdHW", null);
            privDict["StdVW"] = privateDict.GetNumber("StdVW", null);
            privDict["StemSnapH"] = privateDict.GetDelta("StemSnapH", null);
            privDict["StemSnapV"] = privateDict.GetDelta("StemSnapV", null);
            privDict["ForceBold"] = privateDict.GetBoolean("ForceBold", false);
            privDict["LanguageGroup"] = privateDict.GetNumber("LanguageGroup", 0);
            privDict["ExpansionFactor"] = privateDict.GetNumber("ExpansionFactor", 0.06f);
            privDict["initialRandomSeed"] = privateDict.GetNumber("initialRandomSeed", 0);
            privDict["defaultWidthX"] = privateDict.GetNumber("defaultWidthX", 0);
            privDict["nominalWidthX"] = privateDict.GetNumber("nominalWidthX", 0);
            return privDict;
        }

        /**
         * Parse dictionaries specific to a Type 1-equivalent font.
         */
        private void ParseType1Dicts(IInputStream input, DictData topDict, CFFType1Font font, CFFCharset charset)
        {
            // encoding
            DictData.Entry encodingEntry = topDict.GetEntry("Encoding");
            CFFEncoding encoding;
            int encodingId = (int)(encodingEntry?.GetNumber(0) ?? 0);
            switch (encodingId)
            {
                case 0:
                    encoding = CFFStandardEncoding.Instance;
                    break;
                case 1:
                    encoding = CFFExpertEncoding.Instance;
                    break;
                default:
                    input.Position = encodingId;
                    encoding = ReadEncoding(input, charset);
                    break;
            }
            font.Encoding = encoding;

            // read private dict
            DictData.Entry privateEntry = topDict.GetEntry("Private");
            if (privateEntry == null)
            {
                throw new IOException("Private dictionary entry missing for font " + font.Name);
            }
            int privateOffset = (int)privateEntry.GetNumber(1);
            int privateSize = (int)privateEntry.GetNumber(0);
            DictData privateDict = ReadDictData(input, privateOffset, privateSize);

            // populate private dict
            Dictionary<string, object> privDict = ReadPrivateDict(privateDict);
            foreach (var entry in privDict) font.AddToPrivateDict(entry.Key, entry.Value);

            // local subrs
            int localSubrOffset = (int)privateDict.GetNumber(KeySubRS, 0);
            if (localSubrOffset > 0)
            {
                input.Position = privateOffset + localSubrOffset;
                font.AddToPrivateDict(KeySubRS, ReadIndexData(input));
            }
        }

        private string ReadString(int index)
        {
            if (index < 0)
            {
                throw new IOException("Invalid negative index when reading a string");
            }
            if (index <= 390)
            {
                return CFFStandardString.GetName(index);
            }
            if (stringIndex != null && index - 391 < stringIndex.Length)
            {
                return stringIndex[index - 391];
            }
            else
            {
                // technically this maps to .notdef, but we need a unique sid name
                return "SID" + index;
            }
        }

        private string GetString(DictData dict, string name)
        {
            DictData.Entry entry = dict.GetEntry(name);
            return entry != null ? ReadString((int)entry.GetNumber(0)) : null;
        }

        private CFFEncoding ReadEncoding(IInputStream dataInput, CFFCharset charset)
        {
            int format = dataInput.ReadUByte();
            int baseFormat = format & 0x7f;

            switch (baseFormat)
            {
                case 0:
                    return ReadFormat0Encoding(dataInput, charset, format);
                case 1:
                    return ReadFormat1Encoding(dataInput, charset, format);
                default:
                    throw new IOException($"Invalid encoding base format {baseFormat}");
            }
        }

        private Format0Encoding ReadFormat0Encoding(IInputStream dataInput, CFFCharset charset, int format)
        {
            var encoding = new Format0Encoding
            {
                nCodes = dataInput.ReadUByte()
            };
            encoding.Add(0, 0, ".notdef");
            for (int gid = 1; gid <= encoding.nCodes; gid++)
            {
                int code = dataInput.ReadUByte();
                int sid = charset.GetSIDForGID(gid);
                encoding.Add(code, sid, ReadString(sid));
            }
            if ((format & 0x80) != 0)
            {
                ReadSupplement(dataInput, encoding);
            }
            return encoding;
        }

        private Format1Encoding ReadFormat1Encoding(IInputStream dataInput, CFFCharset charset, int format)
        {
            var encoding = new Format1Encoding
            {
                nRanges = dataInput.ReadUByte()
            };
            encoding.Add(0, 0, ".notdef");
            int gid = 1;
            for (int i = 0; i < encoding.nRanges; i++)
            {
                int rangeFirst = dataInput.ReadUByte();
                int rangeLeft = dataInput.ReadUByte();
                for (int j = 0; j <= rangeLeft; j++)
                {
                    int sid = charset.GetSIDForGID(gid);
                    encoding.Add(rangeFirst + j, sid, ReadString(sid));
                    gid++;
                }
            }
            if ((format & 0x80) != 0)
            {
                ReadSupplement(dataInput, encoding);
            }
            return encoding;
        }

        private void ReadSupplement(IInputStream dataInput, CFFBuiltInEncoding encoding)
        {
            var nSups = dataInput.ReadUByte();
            encoding.supplement = new CFFBuiltInEncoding.Supplement[nSups];
            for (int i = 0; i < encoding.supplement.Length; i++)
            {
                var code = dataInput.ReadUByte();
                var sid = dataInput.ReadUInt16();
                var supplement = new CFFBuiltInEncoding.Supplement
                {
                    code = code,
                    sid = sid,
                    name = ReadString(sid)
                };
                encoding.supplement[i] = supplement;
                encoding.Add(supplement);
            }
        }

        /**
         * Read the FDSelect Data according to the format.
         * //@param dataInput
         * //@param nGlyphs
         * //@return the FDSelect data
         * //@throws IOException
         */
        private static FDSelect ReadFDSelect(IInputStream dataInput, int nGlyphs)
        {
            int format = dataInput.ReadUByte();
            switch (format)
            {
                case 0:
                    return ReadFormat0FDSelect(dataInput, nGlyphs);
                case 3:
                    return ReadFormat3FDSelect(dataInput);
                default:
                    throw new ArgumentException();
            }
        }

        /**
         * Read the Format 0 of the FDSelect data structure.
         * //@param dataInput
         * //@param format
         * //@param ros
         * //@return the Format 0 of the FDSelect data
         * //@throws IOException
         */
        private static Format0FDSelect ReadFormat0FDSelect(IInputStream dataInput, int nGlyphs)
        {
            var fds = new int[nGlyphs];
            for (int i = 0; i < fds.Length; i++)
            {
                fds[i] = dataInput.ReadUByte();
            }
            return new Format0FDSelect
            {
                fds = fds
            };
        }

        /**
         * Read the Format 3 of the FDSelect data structure.
         * 
         * //@param dataInput
         * //@param format
         * //@param nGlyphs
         * //@param ros
         * //@return the Format 3 of the FDSelect data
         * //@throws IOException
         */
        private static Format3FDSelect ReadFormat3FDSelect(IInputStream dataInput)
        {
            var nbRanges = dataInput.ReadUInt16();
            var range3 = new Range3[nbRanges];
            for (int i = 0; i < nbRanges; i++)
            {
                range3[i] = new Range3
                {
                    first = dataInput.ReadUInt16(),
                    fd = dataInput.ReadUByte()
                };
            }

            return new Format3FDSelect
            {
                range3 = range3,
                sentinel = dataInput.ReadUInt16(),
            };
        }

        /**
         *  Format 3 FDSelect data.
         */
        internal sealed class Format3FDSelect : FDSelect
        {
            internal Range3[] range3;
            internal int sentinel;

            public override int GetFDIndex(int gid)
            {
                for (int i = 0; i < range3.Length; ++i)
                {
                    if (range3[i].first <= gid)
                    {
                        if (i + 1 < range3.Length)
                        {
                            if (range3[i + 1].first > gid)
                            {
                                return range3[i].fd;
                            }
                            // go to next range
                        }
                        else
                        {
                            // last range reach, the sentinel must be greater than gid
                            if (sentinel > gid)
                            {
                                return range3[i].fd;
                            }
                            return -1;
                        }
                    }
                }
                return 0;
            }

            public override string ToString()
            {
                return $"{GetType().Name}[nbRanges={range3.Length}, range3={string.Join(", ", range3.Select(p => p.ToString()))} sentinel={sentinel}]";
            }
        }

        /**
         * Structure of a Range3 element.
         */
        internal sealed class Range3
        {
            internal int first;
            internal int fd;

            public override string ToString()
            {
                return $"{GetType().Name}[first={first}, fd={fd}]";
            }
        }

        /**
         *  Format 0 FDSelect.
         */
        internal class Format0FDSelect : FDSelect
        {
            //@SuppressWarnings("unused")
            internal int[] fds;

            public override int GetFDIndex(int gid)
            {
                if (gid < fds.Length)
                {
                    return fds[gid];
                }
                return 0;
            }

            public override string ToString()
            {
                return GetType().Name + "[fds=" + string.Join(", ", fds) + "]";
            }
        }

        private CFFCharset ReadCharset(IInputStream dataInput, int nGlyphs, bool isCIDFont)

        {
            int format = dataInput.ReadUByte();
            switch (format)
            {
                case 0:
                    return ReadFormat0Charset(dataInput, nGlyphs, isCIDFont);
                case 1:
                    return ReadFormat1Charset(dataInput, nGlyphs, isCIDFont);
                case 2:
                    return ReadFormat2Charset(dataInput, nGlyphs, isCIDFont);
                default:
                    throw new ArgumentException();
            }
        }

        private Format0Charset ReadFormat0Charset(IInputStream dataInput, int nGlyphs, bool isCIDFont)
        {
            var charset = new Format0Charset(isCIDFont);
            if (isCIDFont)
            {
                charset.AddCID(0, 0);
                for (int gid = 1; gid < nGlyphs; gid++)
                {
                    charset.AddCID(gid, dataInput.ReadUInt16());
                }
            }
            else
            {
                charset.AddSID(0, 0, ".notdef");
                for (int gid = 1; gid < nGlyphs; gid++)
                {
                    int sid = dataInput.ReadUInt16();
                    charset.AddSID(gid, sid, ReadString(sid));
                }
            }
            return charset;
        }

        private Format1Charset ReadFormat1Charset(IInputStream dataInput, int nGlyphs, bool isCIDFont)
        {
            var charset = new Format1Charset(isCIDFont);
            if (isCIDFont)
            {
                charset.AddCID(0, 0);
                int gid = 1;
                while (gid < nGlyphs)
                {
                    int rangeFirst = dataInput.ReadUInt16();
                    int rangeLeft = dataInput.ReadUByte();
                    charset.AddRangeMapping(new RangeMapping(gid, rangeFirst, rangeLeft));
                    gid += rangeLeft + 1;
                }
            }
            else
            {
                charset.AddSID(0, 0, ".notdef");
                int gid = 1;
                while (gid < nGlyphs)
                {
                    int rangeFirst = dataInput.ReadUInt16();
                    int rangeLeft = dataInput.ReadUByte() + 1;
                    for (int j = 0; j < rangeLeft; j++)
                    {
                        int sid = rangeFirst + j;
                        charset.AddSID(gid + j, sid, ReadString(sid));
                    }
                    gid += rangeLeft;
                }
            }
            return charset;
        }

        private Format2Charset ReadFormat2Charset(IInputStream dataInput, int nGlyphs, bool isCIDFont)
        {
            var charset = new Format2Charset(isCIDFont);
            if (isCIDFont)
            {
                charset.AddCID(0, 0);
                int gid = 1;
                while (gid < nGlyphs)
                {
                    int first = dataInput.ReadUInt16();
                    int nLeft = dataInput.ReadUInt16();
                    charset.AddRangeMapping(new RangeMapping(gid, first, nLeft));
                    gid += nLeft + 1;
                }
            }
            else
            {
                charset.AddSID(0, 0, ".notdef");
                int gid = 1;
                while (gid < nGlyphs)
                {
                    int first = dataInput.ReadUInt16();
                    int nLeft = dataInput.ReadUInt16() + 1;
                    for (int j = 0; j < nLeft; j++)
                    {
                        int sid = first + j;
                        charset.AddSID(gid + j, sid, ReadString(sid));
                    }
                    gid += nLeft;
                }
            }
            return charset;
        }

        /**
         * Inner class holding the header of a CFF font. 
         */
        private class Header
        {
            internal int major;
            internal int minor;
            internal int hdrSize;
            internal int offSize;

            public override string ToString()
            {
                return $"{GetType().Name}[major={major}, minor={minor}, hdrSize={hdrSize}, offSize={offSize}]";
            }
        }

        /**
         * Inner class holding the DictData of a CFF font. 
         */
        internal class DictData
        {
            private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

            public void Add(Entry entry)
            {
                if (entry.operatorName != null)
                {
                    entries[entry.operatorName] = entry;
                }
            }

            public Entry GetEntry(string name)
            {
                return entries.TryGetValue(name, out var entry) ? entry : null;
            }

            public bool GetBoolean(string name, bool defaultValue)
            {
                Entry entry = GetEntry(name);
                return entry != null && entry.HasOperands ? entry.GetBool(0, defaultValue) : defaultValue;
            }

            public List<float> GetArray(string name, List<float> defaultValue)
            {
                Entry entry = GetEntry(name);
                return entry != null && entry.HasOperands ? entry.Operands : defaultValue;
            }

            public float? GetNumber(string name, float? defaultValue)
            {
                Entry entry = GetEntry(name);
                return entry != null && entry.HasOperands ? entry.GetNumber(0) : defaultValue;
            }

            public List<float> GetDelta(string name, List<float> defaultValue)
            {
                Entry entry = GetEntry(name);
                return entry != null && entry.HasOperands ? entry.GetDelta() : defaultValue;
            }

            /**
             * {//@inheritDoc} 
             */
            public override string ToString()
            {
                return GetType().Name + "[entries=" + entries + "]";
            }

            /**
             * Inner class holding an operand of a CFF font. 
             */
            internal class Entry
            {
                internal List<float> operands = new List<float>();
                internal string operatorName = null;

                public List<float> Operands
                {
                    get => operands;
                }

                public bool HasOperands
                {
                    get => operands.Count > 0;
                }

                public float GetNumber(int index)
                {
                    return operands[index];
                }

                public bool GetBool(int index, bool defaultValue)
                {
                    float operand = operands[index];
                    switch (operand)
                    {
                        case 0F:
                            return false;
                        case 1F:
                            return true;
                        default:
                            Debug.WriteLine($"warn: Expected boolean, got {operand}, returning default {defaultValue}");
                            return defaultValue;
                    }
                    throw new ArgumentException();
                }

                public void AddOperand(float operand)
                {
                    operands.Add(operand);
                }

                public List<float> GetDelta()
                {
                    var result = new List<float>(operands);
                    for (int i = 1; i < result.Count; i++)
                    {
                        float previous = result[i - 1];
                        float current = result[i];
                        int sum = (int)previous + (int)current;
                        result[i] = sum;
                    }
                    return result;
                }

                public override string ToString()
                {
                    return $"{GetType().Name}[operands={Operands}, operator={operatorName}]";
                }
            }
        }

        /**
         * Inner class representing a font's built-in CFF encoding. 
         */
        internal abstract class CFFBuiltInEncoding : CFFEncoding
        {
            internal Supplement[] supplement;

            /**
             * Inner class representing a supplement for an encoding. 
             */
            internal class Supplement
            {
                internal int code;
                internal int sid;
                internal string name;

                public int Code
                {
                    get => code;
                }

                public int SID
                {
                    get => sid;
                }

                public string Name
                {
                    get => name;
                }

                public override string ToString()
                {
                    return $"{GetType().Name}[code={code}, sid={sid}]";
                }
            }

            public void Add(Supplement supplement)
            {
                Add(supplement.code, supplement.sid, supplement.name);
            }
        }

        /**
         * Inner class representing a Format0 encoding. 
         */
        internal class Format0Encoding : CFFBuiltInEncoding
        {
            internal int nCodes;

            public override string ToString()
            {
                return $"{GetType().Name}[nCodes={nCodes}, supplement={string.Join(", ", base.supplement.Select(p => p.ToString()))}]";
            }
        }

        /**
         * Inner class representing a Format1 encoding. 
         */
        internal class Format1Encoding : CFFBuiltInEncoding
        {
            internal int nRanges;

            public override string ToString()
            {
                return $"{GetType().Name}[nRanges={nRanges}, supplement={string.Join(", ", base.supplement.Select(p => p.ToString()))}]";
            }
        }

        /**
        * An empty charset in a malformed CID font.
        */
        private class EmptyCharsetCID : CFFCharsetCID
        {
            public EmptyCharsetCID(int numCharStrings)
            {
                AddCID(0, 0); // .notdef

                // Adobe Reader treats CID as GID, PDFBOX-2571 p11.
                for (int i = 1; i <= numCharStrings; i++)
                {
                    AddCID(i, i);
                }
            }

            public override String ToString()
            {
                return GetType().Name;
            }
        }

        /**
        * An empty charset in a malformed Type1 font.
        */
        private class EmptyCharsetType1 : CFFCharsetType1
        {
            public EmptyCharsetType1()
            {
                AddSID(0, 0, ".notdef");
            }

            public override string ToString()
            {
                return GetType().Name;
            }
        }

        /**
         * Inner class representing a Format0 charset. 
         */
        internal class Format0Charset : EmbeddedCharset
        {
            internal Format0Charset(bool isCIDFont)
                : base(isCIDFont)
            {
            }
        }

        /**
         * Inner class representing a Format1 charset. 
         */
        internal class Format1Charset : EmbeddedCharset
        {

            private List<RangeMapping> rangesCID2GID;

            public Format1Charset(bool isCIDFont)
                : base(isCIDFont)
            {
                rangesCID2GID = new List<RangeMapping>();
            }

            public override int GetCIDForGID(int gid)
            {
                if (IsCIDFont)
                {
                    foreach (RangeMapping mapping in rangesCID2GID)
                    {
                        if (mapping.IsInRange(gid))
                        {
                            return mapping.MapValue(gid);
                        }
                    }
                }
                return base.GetCIDForGID(gid);
            }

            public override int GetGIDForCID(int cid)
            {
                if (IsCIDFont)
                {
                    foreach (RangeMapping mapping in rangesCID2GID)
                    {
                        if (mapping.IsInReverseRange(cid))
                        {
                            return mapping.MapReverseValue(cid);
                        }
                    }
                }
                return base.GetGIDForCID(cid);
            }

            public void AddRangeMapping(RangeMapping rangeMapping)
            {
                rangesCID2GID.Add(rangeMapping);
            }
        }

        /**
         * Inner class representing a Format2 charset. 
         */
        internal class Format2Charset : EmbeddedCharset
        {
            private List<RangeMapping> rangesCID2GID;

            internal Format2Charset(bool isCIDFont)
                : base(isCIDFont)
            {
                rangesCID2GID = new List<RangeMapping>();
            }

            public override int GetCIDForGID(int gid)
            {
                foreach (RangeMapping mapping in rangesCID2GID)
                {
                    if (mapping.IsInRange(gid))
                    {
                        return mapping.MapValue(gid);
                    }
                }
                return base.GetCIDForGID(gid);
            }

            public override int GetGIDForCID(int cid)
            {
                foreach (RangeMapping mapping in rangesCID2GID)
                {
                    if (mapping.IsInReverseRange(cid))
                    {
                        return mapping.MapReverseValue(cid);
                    }
                }
                return base.GetGIDForCID(cid);
            }

            public void AddRangeMapping(RangeMapping rangeMapping)
            {
                rangesCID2GID.Add(rangeMapping);
            }
        }

        /**
         * Inner class representing a rang mapping for a CID charset. 
         */
        internal readonly struct RangeMapping
        {
            private readonly int startValue;
            private readonly int endValue;
            private readonly int startMappedValue;
            private readonly int endMappedValue;

            public RangeMapping(int startGID, int first, int nLeft)
            {
                this.startValue = startGID;
                endValue = startValue + nLeft;
                this.startMappedValue = first;
                endMappedValue = startMappedValue + nLeft;
            }

            public bool IsInRange(int value)
            {
                return value >= startValue && value <= endValue;
            }

            public bool IsInReverseRange(int value)
            {
                return value >= startMappedValue && value <= endMappedValue;
            }

            public int MapValue(int value)
            {
                return IsInRange(value) ? startMappedValue + (value - startValue) : 0;
            }

            public int MapReverseValue(int value)
            {
                return IsInReverseRange(value) ? startValue + (value - startMappedValue) : 0;
            }

            public override string ToString()
            {
                return GetType().Name + "[start value=" + startValue + ", end value=" + endValue + ", start mapped-value=" + startMappedValue + ", end mapped-value=" + endMappedValue + "]";
            }
        }

        public override string ToString()
        {
            return GetType().Name + "[" + debugFontName + "]";
        }
    }
}
