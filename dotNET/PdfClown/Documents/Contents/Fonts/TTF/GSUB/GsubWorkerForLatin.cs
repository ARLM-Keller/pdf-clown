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

using Org.BouncyCastle.Bcpg.Sig;
using Org.BouncyCastle.Utilities;
using PdfClown.Documents.Contents.Fonts.TTF.Model;
using PdfClown.Documents.Contents.Fonts.TTF;
using PdfClown.Documents.Contents.Fonts.Type1;
using PdfClown.Tokens;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace PdfClown.Documents.Contents.Fonts.TTF.GSUB
{

    /**
     * 
     * Latin-specific implementation of GSUB system
     * 
     * @author Palash Ray
     * @author Tilman Hausherr
     *
     */
    public class GsubWorkerForLatin : GsubWorker
    {
        /**
         * This sequence is very important. This has been taken from <a href=
         * "https://docs.microsoft.com/en-us/typography/script-development/standard">https://docs.microsoft.com/en-us/typography/script-development/standard</a>
         */
        private static readonly List<string> FEATURES_IN_ORDER = new() { "ccmp", "liga", "clig" };

        private readonly ICmapLookup cmapLookup;
        private readonly GsubData gsubData;

        public GsubWorkerForLatin(ICmapLookup cmapLookup, GsubData gsubData)
        {
            this.cmapLookup = cmapLookup;
            this.gsubData = gsubData;
        }

        public List<int> ApplyTransforms(List<int> originalGlyphIds)
        {
            List<int> intermediateGlyphsFromGsub = originalGlyphIds;

            foreach (string feature in FEATURES_IN_ORDER)
            {
                if (!gsubData.IsFeatureSupported(feature))
                {
                    Debug.WriteLine($"debug: the feature {feature} was not found");
                    continue;
                }

                var scriptFeature = gsubData.GetFeature(feature);

                intermediateGlyphsFromGsub = ApplyGsubFeature(scriptFeature, intermediateGlyphsFromGsub);
            }

            return intermediateGlyphsFromGsub;
        }

        private List<int> ApplyGsubFeature(ScriptFeature scriptFeature, List<int> originalGlyphs)
        {
            if (scriptFeature.AllGlyphIdsForSubstitution.Count == 0)
            {
                Debug.WriteLine($"debug: getAllGlyphIdsForSubstitution() for {scriptFeature.Name} is empty");
                return originalGlyphs;
            }
            var glyphArraySplitter = new GlyphArraySplitterRegexImpl(scriptFeature.AllGlyphIdsForSubstitution);

            var tokens = glyphArraySplitter.Split(originalGlyphs);
            List<int> gsubProcessedGlyphs = new();

            foreach (List<int> chunk in tokens)
            {
                if (scriptFeature.CanReplaceGlyphs(chunk))
                {
                    // gsub system kicks in, you get the glyphId directly
                    int glyphId = scriptFeature.GetReplacementForGlyphs(chunk);
                    gsubProcessedGlyphs.Add(glyphId);
                }
                else
                {
                    gsubProcessedGlyphs.AddRange(chunk);
                }
            }
            return gsubProcessedGlyphs;
        }
    }
}
