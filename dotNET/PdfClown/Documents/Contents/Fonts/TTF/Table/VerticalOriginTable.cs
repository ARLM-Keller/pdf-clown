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
using PdfClown.Bytes;
using System.Collections.Generic;
using System.IO;

namespace PdfClown.Documents.Contents.Fonts.TTF
{

    /**
     * A vertical origin 'VORG' table in an OpenType font.
     *
     * The purpose of this table is to improve the efficiency of determining
     * vertical origins in CFF fonts where absent this information the bounding
     * box would have to be extracted from CFF charstring data.
     *
     * This table is strongly recommended by the OpenType CJK Font Guidelines
     * for "CFF OpenType fonts that are used for vertical writing".
     * 
     * This table is specified only in the OpenType specification (1.3 and later).
     * 
     * @author Glenn Adams
     * 
     */
    public class VerticalOriginTable : TTFTable
    {
        /**
         * A tag that identifies this table type.
         */
        public const string TAG = "VORG";

        private float version;
        private int defaultVertOriginY;
        private Dictionary<int, int> origins;

        public VerticalOriginTable()
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
            version = data.Read32Fixed();
            defaultVertOriginY = data.ReadInt16();
            int numVertOriginYMetrics = data.ReadUInt16();
            origins = new Dictionary<int, int>(numVertOriginYMetrics);
            for (int i = 0; i < numVertOriginYMetrics; ++i)
            {
                int g = data.ReadUInt16();
                int y = data.ReadInt16();
                origins[g] = y;
            }
            initialized = true;
        }

        /**
         * @return Returns the version.
         */
        public float Version
        {
            get => version;
        }

        /**
         * Returns the y-coordinate of the vertical origin for the given GID if known,
         * or returns the default value if not specified in table data.
         *
         * @param gid GID
         * @return Returns the y-coordinate of the vertical origin.
         */
        public int GetOriginY(int gid)
        {
            if (origins.TryGetValue(gid, out var origin))
            {
                return origin;
            }
            else
            {
                return defaultVertOriginY;
            }
        }
    }
}
