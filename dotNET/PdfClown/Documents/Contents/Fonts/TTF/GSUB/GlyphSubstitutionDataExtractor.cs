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
using PdfClown.Documents.Contents.Fonts.TTF.Model;
using PdfClown.Documents.Contents.Fonts.TTF.Table.Common;
using PdfClown.Documents.Contents.Fonts.TTF.Table.GSUB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PdfClown.Documents.Contents.Fonts.TTF.GSUB
{


    /**
     * This class has utility methods to extract meaningful data from the highly obfuscated GSUB Tables. This data is then
     * used to determine which combination of Glyphs or words have to be replaced.
     * 
     * @author Palash Ray
     * 
     */
    public class GlyphSubstitutionDataExtractor
    {

        //private static readonly Log LOG = LogFactory.getLog(GlyphSubstitutionDataExtractor.class);

        public GsubData GetGsubData(Dictionary<string, ScriptTable> scriptList, FeatureListTable featureListTable, LookupListTable lookupListTable)
        {
            var scriptTableDetails = GetSupportedLanguage(scriptList);

            if (scriptTableDetails == null)
            {
                return DefaultGsubData.NO_DATA_FOUND;
            }

            ScriptTable scriptTable = scriptTableDetails.ScriptTable;

            Dictionary<string, Dictionary<List<int>, int>> gsubData = new Dictionary<string, Dictionary<List<int>, int>>(StringComparer.Ordinal);
            // the starting point is really the scriptTags
            if (scriptTable.DefaultLangSysTable != null)
            {
                PopulateGsubData(gsubData, scriptTable.DefaultLangSysTable, featureListTable,
                        lookupListTable);
            }
            foreach (LangSysTable langSysTable in scriptTable.LangSysTables.Values)
            {
                PopulateGsubData(gsubData, langSysTable, featureListTable, lookupListTable);
            }
            return BuildMapBackedGsubData(featureListTable, lookupListTable, scriptTableDetails);
        }

        /**
         * Unlike {@link #getGsubData(Map, FeatureListTable, LookupListTable)}, this method doesn't iterate over supported
         * {@link Language}'s searching for the first match with the scripts of the font. Instead, it unconditionally
         * creates {@link ScriptTableDetails} instance with language left {@linkplain Language#UNSPECIFIED unspecified}.
         * 
         * @return {@link GsubData} instance built especially for the given {@code scriptName}
         */
        public GsubData GetGsubData(string scriptName, ScriptTable scriptTable,
                FeatureListTable featureListTable, LookupListTable lookupListTable)
        {
            ScriptTableDetails scriptTableDetails = new ScriptTableDetails(Language.UNSPECIFIED,
                    scriptName, scriptTable);

            return BuildMapBackedGsubData(featureListTable, lookupListTable, scriptTableDetails);
        }

        private MapBackedGsubData BuildMapBackedGsubData(FeatureListTable featureListTable,
                LookupListTable lookupListTable, ScriptTableDetails scriptTableDetails)
        {
            ScriptTable scriptTable = scriptTableDetails.ScriptTable;

            var gsubData = new Dictionary<string, Dictionary<List<int>, int>>();
            // the starting point is really the scriptTags
            if (scriptTable.DefaultLangSysTable != null)
            {
                PopulateGsubData(gsubData, scriptTable.DefaultLangSysTable, featureListTable,
                        lookupListTable);
            }
            foreach (LangSysTable langSysTable in scriptTable.LangSysTables.Values)
            {
                PopulateGsubData(gsubData, langSysTable, featureListTable, lookupListTable);
            }

            return new MapBackedGsubData(scriptTableDetails.Language,
                    scriptTableDetails.FeatureName, gsubData);
        }

        private ScriptTableDetails GetSupportedLanguage(Dictionary<string, ScriptTable> scriptList)
        {
            foreach (Language lang in Enum.GetValues(typeof(Language)))
            {
                foreach (var scriptName in lang.GetScriptNames())
                {
                    if (scriptList.TryGetValue(scriptName, out var scriptTable))
                    {
                        return new ScriptTableDetails(lang, scriptName, scriptTable);
                    }
                }
            }
            return null;
        }

        private void PopulateGsubData(Dictionary<string, Dictionary<List<int>, int>> gsubData,
                LangSysTable langSysTable, FeatureListTable featureListTable,
                LookupListTable lookupListTable)
        {
            FeatureRecord[] featureRecords = featureListTable.FeatureRecords;
            foreach (int featureIndex in langSysTable.FeatureIndices)
            {
                if (featureIndex < featureRecords.Length)
                {
                    PopulateGsubData(gsubData, featureRecords[featureIndex], lookupListTable);
                }
            }
        }

        private void PopulateGsubData(Dictionary<string, Dictionary<List<int>, int>> gsubData,
                FeatureRecord featureRecord, LookupListTable lookupListTable)
        {
            LookupTable[] lookups = lookupListTable.Lookups;
            Dictionary<List<int>, int> glyphSubstitutionMap = new Dictionary<List<int>, int>();
            foreach (int lookupIndex in featureRecord.FeatureTable.LookupListIndices)
            {
                if (lookupIndex < lookups.Length)
                {
                    ExtractData(glyphSubstitutionMap, lookups[lookupIndex]);
                }
            }

            gsubData[featureRecord.FeatureTag] = glyphSubstitutionMap;
        }

        private void ExtractData(Dictionary<List<int>, int> glyphSubstitutionMap, LookupTable lookupTable)
        {
            foreach (LookupSubTable lookupSubTable in lookupTable.SubTables)
            {
                if (lookupSubTable is LookupTypeLigatureSubstitutionSubstFormat1 subtitution)
                {
                    ExtractDataFromLigatureSubstitutionSubstFormat1Table(glyphSubstitutionMap, subtitution);
                }
                else if (lookupSubTable is LookupTypeSingleSubstFormat1 substFormat1)
                {
                    ExtractDataFromSingleSubstTableFormat1Table(glyphSubstitutionMap, substFormat1);
                }
                else if (lookupSubTable is LookupTypeSingleSubstFormat2 substFormat2)
                {
                    ExtractDataFromSingleSubstTableFormat2Table(glyphSubstitutionMap, substFormat2);
                }
                else if (lookupSubTable is LookupTypeMultipleSubstitutionFormat1 msubstFormat1)
                {
                    ExtractDataFromMultipleSubstitutionFormat1Table(glyphSubstitutionMap, msubstFormat1);
                }
                else
                {
                    // usually null, due to being skipped in GlyphSubstitutionTable.readLookupTable()
                    Debug.WriteLine($"debug: The type {lookupSubTable} is not yet supported, will be ignored");
                }
            }

        }

        private void ExtractDataFromSingleSubstTableFormat1Table(Dictionary<List<int>, int> glyphSubstitutionMap,
            LookupTypeSingleSubstFormat1 singleSubstTableFormat1)
        {
            CoverageTable coverageTable = singleSubstTableFormat1.CoverageTable;
            for (int i = 0; i < coverageTable.Size; i++)
            {
                int coverageGlyphId = coverageTable.GetGlyphId(i);
                int substituteGlyphId = coverageGlyphId + singleSubstTableFormat1.DeltaGlyphID;
                PutNewSubstitutionEntry(glyphSubstitutionMap, substituteGlyphId,
                        new List<int> { coverageGlyphId });
            }
        }

        private void ExtractDataFromSingleSubstTableFormat2Table(
                Dictionary<List<int>, int> glyphSubstitutionMap,
                LookupTypeSingleSubstFormat2 singleSubstTableFormat2)
        {

            CoverageTable coverageTable = singleSubstTableFormat2.CoverageTable;

            if (coverageTable.Size != singleSubstTableFormat2.SubstituteGlyphIDs.Length)
            {
                Debug.WriteLine("warn: The no. coverage table entries should be the same as the size of the substituteGlyphIDs");
                return;
            }

            for (int i = 0; i < coverageTable.Size; i++)
            {
                int coverageGlyphId = coverageTable.GetGlyphId(i);
                int substituteGlyphId = coverageGlyphId
                        + singleSubstTableFormat2.SubstituteGlyphIDs[i];
                PutNewSubstitutionEntry(glyphSubstitutionMap, substituteGlyphId,
                        new List<int> { coverageGlyphId });
            }
        }

        private void ExtractDataFromLigatureSubstitutionSubstFormat1Table(Dictionary<List<int>, int> glyphSubstitutionMap,
                LookupTypeLigatureSubstitutionSubstFormat1 ligatureSubstitutionTable)
        {
            foreach (LigatureSetTable ligatureSetTable in ligatureSubstitutionTable.LigatureSetTables)
            {
                foreach (LigatureTable ligatureTable in ligatureSetTable.getLigatureTables())
                {
                    ExtractDataFromLigatureTable(glyphSubstitutionMap, ligatureTable);
                }

            }
        }

        private void ExtractDataFromMultipleSubstitutionFormat1Table(Dictionary<List<int>, int> glyphSubstitutionMap,
            LookupTypeMultipleSubstitutionFormat1 multipleSubstFormat1Subtable)
        {
            var coverageTable = multipleSubstFormat1Subtable.CoverageTable;

            if (coverageTable.Size != multipleSubstFormat1Subtable.SequenceTables.Length)
            {
                Debug.WriteLine("warn: The no. coverage table entries should be the same as the size of the sequencce tables");
                return;
            }

            for (int i = 0; i < coverageTable.Size; i++)
            {
                int coverageGlyphId = coverageTable.GetGlyphId(i);
                SequenceTable sequenceTable = multipleSubstFormat1Subtable.SequenceTables[i];

                //TODO implement storing this data
                // (not possible at this time because the map value isn't a list)
            }

        }

        private void ExtractDataFromLigatureTable(Dictionary<List<int>, int> glyphSubstitutionMap,
                LigatureTable ligatureTable)
        {
            var glyphsToBeSubstituted = new List<int>(ligatureTable.ComponentGlyphIDs);


            PutNewSubstitutionEntry(glyphSubstitutionMap, ligatureTable.LigatureGlyph, glyphsToBeSubstituted);
        }

        private void PutNewSubstitutionEntry(Dictionary<List<int>, int> glyphSubstitutionMap,
                int newGlyph, List<int> glyphsToBeSubstituted)
        {
            if (glyphSubstitutionMap.TryGetValue(glyphsToBeSubstituted, out int oldValue))
            {
                Debug.WriteLine($"warning: For the newGlyph: {newGlyph}, newValue: {glyphsToBeSubstituted} is trying to override the oldValue: {oldValue}");
            }
            glyphSubstitutionMap[glyphsToBeSubstituted] = newGlyph;
        }

        private class ScriptTableDetails
        {
            private readonly Language language;
            private readonly string featureName;
            private readonly ScriptTable scriptTable;

            public ScriptTableDetails(Language language, string featureName, ScriptTable scriptTable)
            {
                this.language = language;
                this.featureName = featureName;
                this.scriptTable = scriptTable;
            }

            public Language Language
            {
                get => language;
            }

            public string FeatureName
            {
                get => featureName;
            }

            public ScriptTable ScriptTable
            {
                get => scriptTable;
            }

        }

    }

    public class ListComparer<T> : IEqualityComparer<List<T>>
    {
        public bool Equals(List<T> x, List<T> y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(List<T> obj)
        {
            int hashcode = 0;
            foreach (T t in obj)
            {
                hashcode ^= t.GetHashCode();
            }
            return hashcode;
        }
    }

    public class ArrayComparer<T> : IEqualityComparer<T[]>
    {
        public bool Equals(T[] x, T[] y)
        {
            return x.SequenceEqual(y);
        }

        public int GetHashCode(T[] obj)
        {
            int hashcode = 0;
            foreach (T t in obj)
            {
                hashcode ^= t.GetHashCode();
            }
            return hashcode;
        }
    }
}
