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
using System.IO;

namespace PdfClown.Documents.Contents.Fonts.TTF
{
    /**
     * A vertical metrics 'vmtx' table in a TrueType or OpenType font.
     * 
     * This table is required by the OpenType CJK Font Guidelines for "all
     * OpenType fonts that are used for vertical writing".
     * 
     * This table is specified in both the TrueType and OpenType specifications.
     * 
     * @author Glenn Adams
     * 
     */
    public class VerticalMetricsTable : TTFTable
    {
        /**
         * A tag that identifies this table type.
         */
        public const string TAG = "vmtx";

        private int[] advanceHeight;
        private short[] topSideBearing;
        private short[] additionalTopSideBearing;
        private int numVMetrics;

        public VerticalMetricsTable()
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
            VerticalHeaderTable vHeader = ttf.VerticalHeader;
            if (vHeader == null)
            {
                throw new IOException("Could not get vhea table");
            }
            numVMetrics = vHeader.NumberOfVMetrics;
            int numGlyphs = ttf.NumberOfGlyphs;

            int bytesRead = 0;
            advanceHeight = new int[numVMetrics];
            topSideBearing = new short[numVMetrics];
            for (int i = 0; i < numVMetrics; i++)
            {
                advanceHeight[i] = data.ReadUInt16();
                topSideBearing[i] = data.ReadInt16();
                bytesRead += 4;
            }

            if (bytesRead < Length)
            {
                int numberNonVertical = numGlyphs - numVMetrics;

                // handle bad fonts with too many vmetrics
                if (numberNonVertical < 0)
                {
                    numberNonVertical = numGlyphs;
                }

                additionalTopSideBearing = new short[numberNonVertical];
                for (int i = 0; i < numberNonVertical; i++)
                {
                    if (bytesRead < Length)
                    {
                        additionalTopSideBearing[i] = data.ReadInt16();
                        bytesRead += 2;
                    }
                }
            }

            initialized = true;
        }

        /**
         * Returns the top sidebearing for the given GID
         *
         * @param gid GID
         */
        public int GetTopSideBearing(int gid)
        {
            if (gid < numVMetrics)
            {
                return topSideBearing[gid];
            }
            else
            {
                return additionalTopSideBearing[gid - numVMetrics];
            }
        }

        /**
         * Returns the advance height for the given GID.
         *
         * @param gid GID
         */
        public int GetAdvanceHeight(int gid)
        {
            if (gid < numVMetrics)
            {
                return advanceHeight[gid];
            }
            else
            {
                // monospaced fonts may not have a height for every glyph
                // the last one is for subsequent glyphs
                return advanceHeight[advanceHeight.Length - 1];
            }
        }
    }
}