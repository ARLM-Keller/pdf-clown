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

using PdfClown.Documents.Contents.Fonts.Type1;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents.Fonts.CCF
{

    /**
     * A Type 0 CIDFont represented in a CFF file. Thread safe.
     *
     * @author Villu Ruusmann
     * @author John Hewson
     */
    public class CFFCIDFont : CFFFont, IType1CharStringReader
    {
        private string registry;
        private string ordering;
        private int supplement;

        private List<Dictionary<string, object>> fontDictionaries = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> privateDictionaries = new List<Dictionary<string, object>>();
        private FDSelect fdSelect;

        private readonly Dictionary<int, CIDKeyedType2CharString> charStringCache = new Dictionary<int, CIDKeyedType2CharString>();
        private Type2CharStringParser charStringParser = null;

        /**
		 * Returns the registry value.
		 * * @return the registry
		 */
        public string Registry
        {
            get => registry;
            set => registry = value;
        }

        /**
		 * Returns the ordering value.
		 *
		 * @return the ordering
		 */
        public string Ordering
        {
            get => ordering;
            set => this.ordering = value;
        }

        /**
		 * Returns the supplement value.
		 *
		 * @return the supplement
		 */
        public int Supplement
        {
            get => supplement;
            set => supplement = value;
        }

        /**
		 * Returns the font dictionaries.
		 *
		 * @return the fontDict
		 */
        public virtual List<Dictionary<string, object>> FontDicts
        {
            get => fontDictionaries;
            set => fontDictionaries = value;
        }

        /**
		 * Returns the private dictionary.
		 *
		 * @return the privDict
		 */
        public virtual List<Dictionary<string, object>> PrivDicts
        {
            get => privateDictionaries;
            set => privateDictionaries = value;
        }

        /**
		 * Returns the fdSelect value.
		 *
		 * @return the fdSelect
		 */
        public virtual FDSelect FdSelect
        {
            get => fdSelect;
            set => fdSelect = value;
        }

        protected Dictionary<string, object> GetPrivateDictionary(int gid)
        {
            int fdArrayIndex = fdSelect.GetFDIndex(gid);
            return fdArrayIndex == -1
                || fdArrayIndex >= privateDictionaries.Count
                ? null
                : privateDictionaries[fdArrayIndex];
        }

        /**
		 * Returns the defaultWidthX for the given GID.
		 *
		 * @param gid GID
		 */
        protected virtual int GetDefaultWidthX(int gid)
        {
            var privDict = GetPrivateDictionary(gid);
            return privDict == null ? 1000 : privDict.TryGetValue("defaultWidthX", out var defaultWidthX) ? (int)(float)defaultWidthX : 1000;
        }

        /**
		 * Returns the nominalWidthX for the given GID.
		 *
		 * @param gid GID
		 */
        protected virtual int GetNominalWidthX(int gid)
        {
            var privDict = GetPrivateDictionary(gid);
            return privDict == null ? 0 : privDict.TryGetValue("nominalWidthX", out var nominalWidthX) ? (int)(float)nominalWidthX : 0;
        }

        /**
		 * Returns the LocalSubrIndex for the given GID.
		 *
		 * @param gid GID
		 */
        private Memory<byte>[] GetLocalSubrIndex(int gid)
        {
            var privDict = GetPrivateDictionary(gid);
            return privDict == null ? null : privDict.TryGetValue("Subrs", out var subrs) ? (Memory<byte>[])subrs : null;
        }

        public Type1CharString GetType1CharString(string name)
        {
            return GetType2CharString(0);
        }
        /**
		 * Returns the Type 2 charstring for the given CID.
		 *
		 * @param cid CID
		 * @throws IOException if the charstring could not be read
		 */
        public override Type2CharString GetType2CharString(int cid)
        {
            if (!charStringCache.TryGetValue(cid, out CIDKeyedType2CharString type2))
            {
                int gid = Charset.GetGIDForCID(cid);
                //var glyphName = Charset.GetNameForGID(gid);
                var bytes = gid < charStrings.Length
                    ? charStrings[gid]
                    : charStrings[0];// .notdef
                var type2seq = Parser.Parse(bytes, globalSubrIndex, GetLocalSubrIndex(gid), cid.ToString());
                type2 = new CIDKeyedType2CharString(this, Name, cid, gid, type2seq, GetDefaultWidthX(gid), GetNominalWidthX(gid));
                charStringCache[cid] = type2;
            }
            return type2;
        }

        private Type2CharStringParser Parser
        {
            get => charStringParser ??= new Type2CharStringParser(Name);
        }

        public override SKPath GetPath(string selector)
        {
            int cid = SelectorToCID(selector);
            return GetType2CharString(cid).Path;
        }

        public override float GetWidth(string selector)
        {
            int cid = SelectorToCID(selector);
            return GetType2CharString(cid).Width;
        }

        public override bool HasGlyph(string selector)
        {
            int cid = SelectorToCID(selector);
            return cid != 0;
        }

        /**
         * Parses a CID selector of the form \ddddd.
         */
        private int SelectorToCID(string selector)
        {
            if (!selector.StartsWith("\\"))
            {
                throw new ArgumentException("Invalid selector");
            }
            return int.Parse(selector.Substring(1));
        }

        ///**
        // * Private implementation of Type1CharStringReader, because only CFFType1Font can
        // * expose this publicly, as CIDFonts only support this for legacy 'seac' commands.
        // */
        //private class PrivateType1CharStringReader : IType1CharStringReader
        //{
        //    //@Override

        //    public Type1CharString getType1CharString(string name)
        //    {
        //        return CFFCIDFont.this.getType2CharString(0); // .notdef
        //    }
        //}
    }
}
