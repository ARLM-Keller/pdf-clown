/*
  Copyright 2010-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using bytes = PdfClown.Bytes;
using PdfClown.Objects;
using PdfClown.Util;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PdfClown.Bytes;
using System.Diagnostics;
using PdfClown.Util.IO;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
      <summary>Character map [PDF:1.6:5.6.4].</summary>
    */
    public sealed class CMap
    {
        private static readonly int SPACE = ' ';
        private static readonly ConcurrentDictionary<string, CMap> cMapCache = new();
        /**
          <summary>Gets the character map extracted from the given data.</summary>
          <param name="stream">Character map data.</param>
        */
        public static CMap Get(IInputStream stream)
        {
            CMapParser parser = new CMapParser(stream);
            return parser.Parse();
        }

        /**
          <summary>Gets the character map extracted from the given encoding object.</summary>
          <param name="encodingObject">Encoding object.</param>
        */
        public static CMap Get(PdfDataObject encodingObject)
        {
            if (encodingObject == null)
                return null;

            if (encodingObject is PdfName pdfName) // Predefined CMap.
                return Get(pdfName);
            else if (encodingObject is PdfStream pdfStream) // Embedded CMap file.
                return Get(pdfStream);
            else
                throw new NotSupportedException("Unknown encoding object type: " + encodingObject.GetType().Name);
        }

        /**
          <summary>Gets the character map extracted from the given data.</summary>
          <param name="stream">Character map data.</param>
        */
        public static CMap Get(PdfStream stream) => Get(stream.Body);

        /**
          <summary>Gets the character map corresponding to the given name.</summary>
          <param name="name">Predefined character map name.</param>
          <returns>null, in case no name matching occurs.</returns>
        */
        public static CMap Get(PdfName name) => Get(name.ToString());

        /**
          <summary>Gets the character map corresponding to the given name.</summary>
          <param name="name">Predefined character map name.</param>
          <returns>null, in case no name matching occurs.</returns>
        */
        public static CMap Get(string name) => cMapCache.GetOrAdd(name, Load);

        private static CMap Load(string name)
        {
            using (var cmapResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("fonts.cmap." + name))
            {
                if (cmapResourceStream == null)
                    return null;

                return Get(new ByteStream(cmapResourceStream));
            }
        }

        private int minCodeLength = 4;
        private int maxCodeLength;
        private string cmapName;
        //private string cmapVersion;
        private int cMapType = -1;
        private string registry;
        private string ordering;
        //private int supplement;
        private int wMode;
        private int spaceMapping;
        private int minCidLength = 4;
        private int maxCidLength = 0;

        // code lengths
        private readonly List<CodespaceRange> codespaceRanges = new List<CodespaceRange>();
        // Unicode mappings
        private readonly Dictionary<int, int> charToUnicode = new Dictionary<int, int>();
        private readonly Dictionary<int, byte[]> unicodeToByteCodes = new Dictionary<int, byte[]>();
        // CID mappings
        private readonly Dictionary<int, Dictionary<int, int>> codeToCid = new();
        private readonly List<CIDRange> codeToCidRanges = new();

        public CMap()
        { }

        public int MinCodeLength
        {
            get => minCodeLength;
            set => minCodeLength = value;
        }

        public int MaxCodeLength
        {
            get => maxCodeLength;
            set => maxCodeLength = value;
        }

        public string CMapName
        {
            get => cmapName;
            set => cmapName = value;
        }

        public int CMapType
        {
            get => cMapType;
            set => cMapType = value;
        }

        public string Registry
        {
            get => registry;
            set => registry = value;
        }

        public string Ordering
        {
            get => ordering;
            set => ordering = value;
        }

        public int WMode
        {
            get => wMode;
            set => wMode = value;
        }

        public int SpaceMapping
        {
            get => spaceMapping;
            set => spaceMapping = value;
        }
        /**
         * This will tell if this cmap has any CID mappings.
         * 
         * @return true If there are any CID mappings, false otherwise.
         */
        public bool HasCIDMappings
        {
            get => codeToCid.Count > 0 || codeToCidRanges.Count > 0;
        }

        /**
         * This will tell if this cmap has any Unicode mappings.
         *
         * @return true If there are any Unicode mappings, false otherwise.
         */
        public bool HasUnicodeMappings
        {
            get => charToUnicode.Count > 0;
        }

        /**
         * Returns the sequence of Unicode characters for the given character code.
         *
         * @param code character code
         * @return Unicode characters (may be more than one, e.g "fi" ligature)
         */
        public int? ToUnicode(int code)
        {
            return charToUnicode.TryGetValue(code, out var unicode) ? unicode : null;
        }

        public byte[] ToCode(int unicode)
        {
            return unicodeToByteCodes.TryGetValue(unicode, out var codes) ? codes : null;
        }

        /**
       * Returns the CID for the given character code.
     *
     * @param code character code as byte array
     * @return CID
     */
        public int? ToCID(ReadOnlySpan<byte> code)
        {
            if (!HasCIDMappings || code.Length < minCidLength || code.Length > maxCidLength)
            {
                return 0;
            }
            int? cid = null;
            if (codeToCid.TryGetValue(code.Length, out var subSid)
                && subSid.TryGetValue(code.ReadIntOffset(), out var exist))
            {
                cid = exist;
            }
            return cid ?? ToCIDFromRanges(code);
        }

        /**
         * Returns the CID for the given character code.
         *
         * @param code character code
         * @return CID
         */
        public int? ToCID(int code)
        {
            if (!HasCIDMappings)
            {
                return 0;
            }
            int? cid = null;
            int length = minCidLength;
            while ((cid ?? 0) == 0 && (length <= maxCidLength))
            {
                cid = ToCID(code, length++);
            }
            return cid;
        }

        /**
     * Returns the CID for the given character code.
     *
     * @param code   character code
     * @param length the origin byte length of the code
     * @return CID
     */
        public int? ToCID(int code, int length)
        {
            if (!HasCIDMappings || length < minCidLength || length > maxCidLength)
            {
                return 0;
            }
            int? cid = null;
            if (codeToCid.TryGetValue(length, out var subCid))
            {
                if (subCid.TryGetValue(code, out var exist))
                    cid = exist;
            }
            return cid ?? ToCIDFromRanges(code, length);
        }

        /**
     * Returns the CID for the given character code.
     *
     * @param code character code
     * @return CID
     */

        private int ToCIDFromRanges(int code, int length)
        {
            foreach (CIDRange range in codeToCidRanges)
            {
                int ch = range.Map(code, length);
                if (ch != -1)
                {
                    return ch;
                }
            }
            return 0;
        }

        /**
     * Returns the CID for the given character code.
     *
     * @param code character code
     * @return CID
     */
        private int ToCIDFromRanges(ReadOnlySpan<byte> code)
        {
            foreach (CIDRange range in codeToCidRanges)
            {
                int ch = range.Map(code);
                if (ch != -1)
                {
                    return ch;
                }
            }
            return 0;
        }

        /**
     * Convert the given part of a byte array to an integer.
     * 
     * @param data   the byte array
     * @param offset The offset into the byte array.
     * @param length The length of the data we are getting.
     * @return the resulting integer
     */
        private int GetCodeFromArray(ReadOnlySpan<byte> data)
        {
            int code = 0;
            for (int i = 0; i < data.Length; i++)
            {
                code <<= 8;
                code |= (data[i] + 256) % 256;
            }
            return code;
        }


        //private int? ToCIDFromRanges(int code)
        //{
        //    foreach (CIDRange range in codeToCidRanges)
        //    {
        //        int ch = range.Map((char)code);
        //        if (ch != -1)
        //        {
        //            return ch;
        //        }
        //    }
        //    return null;
        //}

        /**
         * Reads a character code from a string in the content stream.
         * <p>See "CMap Mapping" and "Handling Undefined Characters" in PDF32000 for more details.
         *
         * @param in string stream
         * @return character code
         * @throws IOException if there was an error reading the stream or CMap
         */
        public int ReadCode(IInputStream input, out ReadOnlySpan<byte> bytes)
        {
            var temp = input.Position;
            bytes = input.ReadSpan(maxCodeLength);
            var code = ReadCode(bytes, out var byteCount);
            if ((temp + byteCount) < input.Position)
            {
                input.Skip((temp + byteCount) - input.Position);
            }
            return code;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadCode(ReadOnlySpan<byte> bytes, out int byteCount)
        {
            byteCount = 0;
            for (int i = minCodeLength - 1; i < maxCodeLength; i++)
            {
                byteCount = i + 1;
                foreach (CodespaceRange range in codespaceRanges)
                {
                    if (range.IsFullMatch(bytes, byteCount))
                    {
                        return bytes.Slice(0, byteCount).ReadIntOffset();
                    }
                }
            }
#if DEBUG
            MissCode(bytes);
#endif
            return bytes.Slice(0, byteCount).ReadIntOffset();
        }

        private void MissCode(ReadOnlySpan<byte> bytes)
        {
            string seq = "";
            for (int i = 0; i < maxCodeLength; ++i)
            {
                seq += $"{bytes[i]} ({bytes[i]:x2}) ";
            }
            Debug.WriteLine($"warn: Invalid character code sequence {seq}in CMap {cmapName}");
        }

        public int ReadCode(byte[] code)
        {
            byte[] bytes = new byte[maxCodeLength];
            for (int i = 0; i < minCodeLength; i++)
                bytes[i] = code[i];

            for (int i = minCodeLength - 1; i < maxCodeLength; i++)
            {
                int byteCount = i + 1;
                foreach (CodespaceRange range in codespaceRanges)
                {
                    if (range.IsFullMatch(bytes, byteCount))
                    {
                        return ConvertUtils.ReadIntOffset(bytes, 0, byteCount, ByteOrderEnum.BigEndian);
                    }
                }
                if (byteCount < maxCodeLength)
                {
                    bytes[byteCount] = code[byteCount];
                }
            }
            MissCode(bytes);
            return 0;
        }

        /**
         * Implementation of the usecmap operator.  This will
         * copy all of the mappings from one cmap to another.
         * @param cmap The cmap to load mappings from.
         * */
        public void UseCmap(CMap cmap)
        {
            foreach (var spaceRange in cmap.codespaceRanges)
            {
                AddCodespaceRange(spaceRange);
            }
            foreach (var charUnicode in cmap.charToUnicode)
            {
                charToUnicode[charUnicode.Key] = charUnicode.Value;
            }
            foreach (var codeCid in cmap.codeToCid)
            {
                if (!codeToCid.TryGetValue(codeCid.Key, out var existingMapping))
                    codeToCid[codeCid.Key] = existingMapping = new Dictionary<int, int>();
                foreach (var sub in codeCid.Value)
                {
                    existingMapping[sub.Key] = sub.Value;
                }
            }
            foreach (var unicideBytes in cmap.unicodeToByteCodes)
            {
                unicodeToByteCodes[unicideBytes.Key] = unicideBytes.Value;
            }
            codeToCidRanges.AddRange(cmap.codeToCidRanges);
            maxCodeLength = Math.Max(maxCodeLength, cmap.maxCodeLength);
            minCodeLength = Math.Min(minCodeLength, cmap.minCodeLength);
            maxCidLength = Math.Max(maxCidLength, cmap.maxCidLength);
            minCidLength = Math.Min(minCidLength, cmap.minCidLength);
        }

        /**
         * This will add a character code to Unicode character sequence mapping.
         * @param codes The character codes to map from.
         * @param unicode The Unicode characters to map to.
          */
        internal void AddCharMapping(ReadOnlySpan<byte> codes, int unicode)
        {
            unicodeToByteCodes[unicode] = codes.ToArray();
            int code = codes.ReadIntOffset();
            charToUnicode[code] = unicode;

            // fixme: ugly little hack
            if (SPACE.Equals(unicode))
            {
                spaceMapping = code;
            }
        }

        /**
         * This will add a CID mapping.
         *
         * @param code character code
         * @param cid CID
         */
        internal void AddCIDMapping(ReadOnlySpan<byte> code, int cid)
        {
            if (!codeToCid.TryGetValue(code.Length, out var codeToCidMap))
            {
                codeToCidMap = new();
                codeToCid[code.Length] = codeToCidMap;
                minCidLength = Math.Min(minCidLength, code.Length);
                maxCidLength = Math.Max(maxCidLength, code.Length);
            }
            codeToCidMap[code.ReadIntOffset()] = cid;
        }

        /**
     * This will add a CID Range.
     *
     * @param from starting character of the CID range.
     * @param to ending character of the CID range.
     * @param cid the cid to be started with.
     *
     */
        public void AddCIDRange(ReadOnlySpan<byte> from, ReadOnlySpan<byte> to, int cid)
        {
            AddCIDRange(codeToCidRanges, from.ReadIntOffset(), to.ReadIntOffset(), cid, from.Length);
        }

        private void AddCIDRange(List<CIDRange> cidRanges, int from, int to, int cid, int length)
        {
            CIDRange lastRange = null;
            if (cidRanges.Count > 0)
            {
                lastRange = cidRanges[cidRanges.Count - 1];
            }
            if (lastRange == null || !lastRange.Extend(from, to, cid, length))
            {
                cidRanges.Add(new CIDRange(from, to, cid, length));
                minCidLength = Math.Min(minCidLength, length);
                maxCidLength = Math.Max(maxCidLength, length);
            }
        }

        /**
         * This will add a codespace range.
         *
         * @param range A single codespace range.
         */
        internal void AddCodespaceRange(CodespaceRange range)
        {
            codespaceRanges.Add(range);
            maxCodeLength = Math.Max(maxCodeLength, range.CodeLength);
            minCodeLength = Math.Min(minCodeLength, range.CodeLength);
        }
    }
}