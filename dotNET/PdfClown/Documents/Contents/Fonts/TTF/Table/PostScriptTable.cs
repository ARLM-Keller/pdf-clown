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
using System.IO;
using System.Diagnostics;
using System;
using PdfClown.Bytes;

namespace PdfClown.Documents.Contents.Fonts.TTF
{
    /**
     * A table in a true type font.
     * 
     * @author Ben Litchfield
     */
    public class PostScriptTable : TTFTable
    {
        //private static readonly Log LOG = LogFactory.getLog(PostScriptTable.class);
        private float formatType;
        private float italicAngle;
        private short underlinePosition;
        private short underlineThickness;
        private long isFixedPitch;
        private long minMemType42;
        private long maxMemType42;
        private long mimMemType1;
        private long maxMemType1;
        private string[] glyphNames = null;

        /**
         * A tag that identifies this table type.
         */
        public const string TAG = "post";

        public PostScriptTable()
        { }

        /**
         * This will read the required data from the stream.
         * 
         * @param ttf The font that is being read.
         * @param data The stream to read the data from.
         * @ If there is an error reading the data.
         */
        public override void Read(TrueTypeFont ttf, IInputStream data)
        {
            formatType = data.Read32Fixed();
            italicAngle = data.Read32Fixed();
            underlinePosition = data.ReadInt16();
            underlineThickness = data.ReadInt16();
            isFixedPitch = data.ReadUInt32();
            minMemType42 = data.ReadUInt32();
            maxMemType42 = data.ReadUInt32();
            mimMemType1 = data.ReadUInt32();
            maxMemType1 = data.ReadUInt32();

            if (formatType.CompareTo(1.0f) == 0)
            {
                /*
                 * This TrueType font file contains exactly the 258 glyphs in the standard Macintosh TrueType.
                 */
                glyphNames = WGL4Names.GetAllNames();
            }
            else if (formatType.CompareTo(2.0f) == 0)
            {
                int numGlyphs = data.ReadUInt16();
                var glyphNameIndex = new ushort[numGlyphs];
                glyphNames = new string[numGlyphs];
                var maxIndex = ushort.MinValue;
                for (int i = 0; i < numGlyphs; i++)
                {
                    var index = data.ReadUInt16();
                    glyphNameIndex[i] = index;
                    // PDFBOX-808: Index numbers between 32768 and 65535 are
                    // reserved for future use, so we should just ignore them
                    if (index <= 32767)
                    {
                        maxIndex = Math.Max(maxIndex, index);
                    }
                }
                string[] nameArray = null;
                if (maxIndex >= WGL4Names.NUMBER_OF_MAC_GLYPHS)
                {
                    nameArray = new string[maxIndex - WGL4Names.NUMBER_OF_MAC_GLYPHS + 1];
                    for (int i = 0; i < maxIndex - WGL4Names.NUMBER_OF_MAC_GLYPHS + 1; i++)
                    {
                        int numberOfChars = data.ReadByte();
                        // PDFBOX-4851: EOF
                        if (numberOfChars > -1 && data.Position + numberOfChars < data.Length)
                        {
                            nameArray[i] = data.ReadString(numberOfChars);
                        }
                        else
                        {
                            nameArray[i] = ".notdef";
                        }
                    }
                }
                for (int i = 0; i < numGlyphs; i++)
                {
                    int index = glyphNameIndex[i];
                    if (index >= 0 && index < WGL4Names.NUMBER_OF_MAC_GLYPHS)
                    {
                        glyphNames[i] = WGL4Names.GetGlyphName(index);
                    }
                    else if (index >= WGL4Names.NUMBER_OF_MAC_GLYPHS && index <= 32767 && nameArray != null)
                    {
                        glyphNames[i] = nameArray[index - WGL4Names.NUMBER_OF_MAC_GLYPHS];
                    }
                    else
                    {
                        // PDFBOX-808: Index numbers between 32768 and 65535 are
                        // reserved for future use, so we should just ignore them
                        glyphNames[i] = ".undefined";
                    }
                }
            }
            else if (formatType.CompareTo(2.5f) == 0)
            {
                var glyphNameIndex = new ushort[ttf.NumberOfGlyphs];
                for (int i = 0; i < glyphNameIndex.Length; i++)
                {
                    var offset = data.ReadSByte();
                    glyphNameIndex[i] = (ushort)(i + 1 + offset);
                }
                glyphNames = new string[glyphNameIndex.Length];
                for (int i = 0; i < glyphNames.Length; i++)
                {
                    int index = glyphNameIndex[i];
                    if (index >= 0 && index < WGL4Names.NUMBER_OF_MAC_GLYPHS)
                    {
                        string name = WGL4Names.GetGlyphName(index);
                        if (name != null)
                        {
                            glyphNames[i] = name;
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"debug: incorrect glyph name index {index}, valid numbers 0..{WGL4Names.NUMBER_OF_MAC_GLYPHS}");
                    }
                }
            }
            else if (formatType.CompareTo(3.0f) == 0)
            {
                // no postscript information is provided.
                //Debug.WriteLine($"debug: No PostScript name information is provided for the font {font.Name}");
            }
            initialized = true;
        }

        /**
         * @return Returns the formatType.
         */
        public float FormatType
        {
            get => formatType;
            set => formatType = value;
        }

        /**
         * @return Returns the isFixedPitch.
         */
        public long IsFixedPitch
        {
            get => isFixedPitch;
            set => isFixedPitch = value;
        }

        /**
         * @return Returns the italicAngle.
         */
        public float ItalicAngle
        {
            get => italicAngle;
            set => italicAngle = value;
        }

        /**
         * @return Returns the maxMemType1.
         */
        public long MaxMemType1
        {
            get => maxMemType1;
            set => maxMemType1 = value;
        }

        /**
         * @return Returns the maxMemType42.
         */
        public long MaxMemType42
        {
            get => maxMemType42;
            set => maxMemType42 = value;
        }

        /**
         * @return Returns the mimMemType1.
         */
        public long MinMemType1
        {
            get => mimMemType1;
            set => mimMemType1 = value;
        }

        /**
         * @return Returns the minMemType42.
         */
        public long MinMemType42
        {
            get => minMemType42;
            set => minMemType42 = value;
        }

        /**
         * @return Returns the underlinePosition.
         */
        public short UnderlinePosition
        {
            get => underlinePosition;
            set => underlinePosition = value;
        }

        /**
         * @return Returns the underlineThickness.
         */
        public short UnderlineThickness
        {
            get => underlineThickness;
            set => underlineThickness = value;
        }

        /**
         * @return Returns the glyphNames.
         */
        public string[] GlyphNames
        {
            get => glyphNames;
            set => glyphNames = value;
        }

        /**
         * @return Returns the glyph name.
         */
        public string GetName(int gid)
        {
            if (gid < 0 || glyphNames == null || gid >= glyphNames.Length)
            {
                return null;
            }
            return glyphNames[gid];
        }
    }
}
