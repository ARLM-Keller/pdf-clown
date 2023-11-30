/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except input compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to input writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using PdfClown.Util;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfClown.Documents.Contents.Fonts.AFM
{

    /**
     * This class is used to parse AFM(Adobe Font Metrics) documents.
     *
     * @see <A href="http://partners.adobe.com/asn/developer/type/">AFM Documentation</A>
     *
     * @author Ben Litchfield
     * 
     */
    public class AFMParser
    {
        /**
         * This is a comment input a AFM file.
         */
        public const string COMMENT = "Comment";
        /**
         * This is the constant used input the AFM file to start a font metrics item.
         */
        public const string START_FONT_METRICS = "StartFontMetrics";
        /**
         * This is the constant used input the AFM file to end a font metrics item.
         */
        public const string END_FONT_METRICS = "EndFontMetrics";
        /**
         * This is the font name.
         */
        public const string FONT_NAME = "FontName";
        /**
         * This is the full name.
         */
        public const string FULL_NAME = "FullName";
        /**
         * This is the Family name.
         */
        public const string FAMILY_NAME = "FamilyName";
        /**
         * This is the weight.
         */
        public const string WEIGHT = "Weight";
        /**
         * This is the font bounding box.
         */
        public const string FONT_BBOX = "FontBBox";
        /**
         * This is the version of the font.
         */
        public const string VERSION = "Version";
        /**
         * This is the notice.
         */
        public const string NOTICE = "Notice";
        /**
         * This is the encoding scheme.
         */
        public const string ENCODING_SCHEME = "EncodingScheme";
        /**
         * This is the mapping scheme.
         */
        public const string MAPPING_SCHEME = "MappingScheme";
        /**
         * This is the escape character.
         */
        public const string ESC_CHAR = "EscChar";
        /**
         * This is the character set.
         */
        public const string CHARACTER_SET = "CharacterSet";
        /**
         * This is the characters attribute.
         */
        public const string CHARACTERS = "Characters";
        /**
         * This will determine if this is a base font.
         */
        public const string IS_BASE_FONT = "IsBaseFont";
        /**
         * This is the V Vector attribute.
         */
        public const string V_VECTOR = "VVector";
        /**
         * This will tell if the V is fixed.
         */
        public const string IS_FIXED_V = "IsFixedV";
        /**
         * This is the cap height attribute.
         */
        public const string CAP_HEIGHT = "CapHeight";
        /**
         * This is the X height.
         */
        public const string X_HEIGHT = "XHeight";
        /**
         * This is ascender attribute.
         */
        public const string ASCENDER = "Ascender";
        /**
         * This is the descender attribute.
         */
        public const string DESCENDER = "Descender";

        /**
         * The underline position.
         */
        public const string UNDERLINE_POSITION = "UnderlinePosition";
        /**
         * This is the Underline thickness.
         */
        public const string UNDERLINE_THICKNESS = "UnderlineThickness";
        /**
         * This is the italic angle.
         */
        public const string ITALIC_ANGLE = "ItalicAngle";
        /**
         * This is the char width.
         */
        public const string CHAR_WIDTH = "CharWidth";
        /**
         * This will determine if this is fixed pitch.
         */
        public const string IS_FIXED_PITCH = "IsFixedPitch";
        /**
         * This is the start of character metrics.
         */
        public const string START_CHAR_METRICS = "StartCharMetrics";
        /**
         * This is the end of character metrics.
         */
        public const string END_CHAR_METRICS = "EndCharMetrics";
        /**
         * The character metrics c value.
         */
        public const string CHARMETRICS_C = "C";
        /**
         * The character metrics c value.
         */
        public const string CHARMETRICS_CH = "CH";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_WX = "WX";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W0X = "W0X";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W1X = "W1X";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_WY = "WY";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W0Y = "W0Y";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W1Y = "W1Y";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W = "W";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W0 = "W0";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_W1 = "W1";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_VV = "VV";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_N = "N";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_B = "B";
        /**
         * The character metrics value.
         */
        public const string CHARMETRICS_L = "L";
        /**
         * The character metrics value.
         */
        public const string STD_HW = "StdHW";
        /**
         * The character metrics value.
         */
        public const string STD_VW = "StdVW";
        /**
         * This is the start of track kern data.
         */
        public const string START_TRACK_KERN = "StartTrackKern";
        /**
         * This is the end of track kern data.
         */
        public const string END_TRACK_KERN = "EndTrackKern";
        /**
         * This is the start of kern data.
         */
        public const string START_KERN_DATA = "StartKernData";
        /**
         * This is the end of kern data.
         */
        public const string END_KERN_DATA = "EndKernData";
        /**
         * This is the start of kern pairs data.
         */
        public const string START_KERN_PAIRS = "StartKernPairs";
        /**
         * This is the end of kern pairs data.
         */
        public const string END_KERN_PAIRS = "EndKernPairs";
        /**
         * This is the start of kern pairs data.
         */
        public const string START_KERN_PAIRS0 = "StartKernPairs0";
        /**
         * This is the start of kern pairs data.
         */
        public const string START_KERN_PAIRS1 = "StartKernPairs1";
        /**
         * This is the start compisites data section.
         */
        public const string START_COMPOSITES = "StartComposites";
        /**
         * This is the end compisites data section.
         */
        public const string END_COMPOSITES = "EndComposites";
        /**
         * This is a composite character.
         */
        public const string CC = "CC";
        /**
         * This is a composite character part.
         */
        public const string PCC = "PCC";
        /**
         * This is a kern pair.
         */
        public const string KERN_PAIR_KP = "KP";
        /**
         * This is a kern pair.
         */
        public const string KERN_PAIR_KPH = "KPH";
        /**
         * This is a kern pair.
         */
        public const string KERN_PAIR_KPX = "KPX";
        /**
         * This is a kern pair.
         */
        public const string KERN_PAIR_KPY = "KPY";

        private const int BITS_IN_HEX = 16;


        private readonly Stream input;

        /**
         * Constructor.
         *
         * @param input The input stream to read the AFM document from.
         */
        public AFMParser(Stream input)
        {
            this.input = input;
        }

        /**
         * This will parse the AFM document. The input stream is closed
         * when the parsing is finished.
         *
         * @return the parsed FontMetric
         * 
         * @throws IOException If there is an IO error reading the document.
         */
        public FontMetrics Parse()
        {
            return ParseFontMetric(false);
        }

        /**
         * This will parse the AFM document. The input stream is closed
         * when the parsing is finished.
         *
         * @param reducedDataset parse a reduced subset of data if set to true
         * @return the parsed FontMetric
         * 
         * @throws IOException If there is an IO error reading the document.
         */
        public FontMetrics Parse(bool reducedDataset)
        {
            return ParseFontMetric(reducedDataset);
        }

        /**
         * This will parse a font metrics item.
         *
         * @return The parse font metrics item.
         *
         * @throws IOException If there is an error reading the AFM file.
         */
        private FontMetrics ParseFontMetric(bool reducedDataset)
        {
            var fontMetrics = new FontMetrics();
            string startFontMetrics = ReadString();
            if (!START_FONT_METRICS.Equals(startFontMetrics, StringComparison.Ordinal))
            {
                throw new IOException($"Error: The AFM file should start with {START_FONT_METRICS} and not '{startFontMetrics}'");
            }
            fontMetrics.AFMVersion = Readfloat();
            string nextCommand;
            bool charMetricsRead = false;
            while (!END_FONT_METRICS.Equals(nextCommand = ReadString(), StringComparison.Ordinal))
            {
                switch (nextCommand)
                {
                    case FONT_NAME:
                        fontMetrics.FontName = ReadLine();
                        break;
                    case FULL_NAME:
                        fontMetrics.FullName = ReadLine();
                        break;
                    case FAMILY_NAME:
                        fontMetrics.FamilyName = ReadLine();
                        break;
                    case WEIGHT:
                        fontMetrics.Weight = ReadLine();
                        break;
                    case FONT_BBOX:
                        var bBox = new SKRect();
                        bBox.Left = Readfloat();
                        bBox.Top = Readfloat();
                        bBox.Right = Readfloat();
                        bBox.Bottom = Readfloat();
                        fontMetrics.FontBBox = bBox;
                        break;
                    case VERSION:
                        fontMetrics.FontVersion = ReadLine();
                        break;
                    case NOTICE:
                        fontMetrics.Notice = ReadLine();
                        break;
                    case ENCODING_SCHEME:
                        fontMetrics.EncodingScheme = ReadLine();
                        break;
                    case MAPPING_SCHEME:
                        fontMetrics.MappingScheme = ReadInt();
                        break;
                    case ESC_CHAR:
                        fontMetrics.EscChar = ReadInt();
                        break;
                    case CHARACTER_SET:
                        fontMetrics.CharacterSet = ReadLine();
                        break;
                    case CHARACTERS:
                        fontMetrics.Characters = ReadInt();
                        break;
                    case IS_BASE_FONT:
                        fontMetrics.IsBaseFont = ReadBoolean();
                        break;
                    case V_VECTOR:
                        float[] vector = new float[2];
                        vector[0] = Readfloat();
                        vector[1] = Readfloat();
                        fontMetrics.VVector = vector;
                        break;
                    case IS_FIXED_V:
                        fontMetrics.IsFixedV = ReadBoolean();
                        break;
                    case CAP_HEIGHT:
                        fontMetrics.CapHeight = Readfloat();
                        break;
                    case X_HEIGHT:
                        fontMetrics.XHeight = Readfloat();
                        break;
                    case ASCENDER:
                        fontMetrics.Ascender = Readfloat();
                        break;
                    case DESCENDER:
                        fontMetrics.Descender = Readfloat();
                        break;
                    case STD_HW:
                        fontMetrics.StandardHorizontalWidth = Readfloat();
                        break;
                    case STD_VW:
                        fontMetrics.StandardVerticalWidth = Readfloat();
                        break;
                    case COMMENT:
                        fontMetrics.AddComment(ReadLine());
                        break;
                    case UNDERLINE_POSITION:
                        fontMetrics.UnderlinePosition = Readfloat();
                        break;
                    case UNDERLINE_THICKNESS:
                        fontMetrics.UnderlineThickness = Readfloat();
                        break;
                    case ITALIC_ANGLE:
                        fontMetrics.ItalicAngle = Readfloat();
                        break;
                    case CHAR_WIDTH:
                        float[] widths = new float[2];
                        widths[0] = Readfloat();
                        widths[1] = Readfloat();
                        fontMetrics.CharWidth = widths;
                        break;
                    case IS_FIXED_PITCH:
                        fontMetrics.IsFixedPitch = ReadBoolean();
                        break;
                    case START_CHAR_METRICS:
                        charMetricsRead = ParseCharMetrics(fontMetrics);
                        break;
                    case START_KERN_DATA:
                        if (!reducedDataset)
                        {
                            ParseKernData(fontMetrics);
                        }
                        break;
                    case START_COMPOSITES:
                        if (!reducedDataset)
                        {
                            ParseComposites(fontMetrics);
                        }
                        break;
                    default:
                        if (reducedDataset && charMetricsRead)
                        {
                            break;
                        }
                        throw new IOException($"Unknown AFM key '{nextCommand}'");
                }
            }
            return fontMetrics;
        }

        private void ParseComposites(FontMetrics fontMetrics)
        {
            int countComposites = ReadInt();
            for (int i = 0; i < countComposites; i++)
            {
                Composite part = ParseComposite();
                fontMetrics.AddComposite(part);
            }
            ReadCommand(END_COMPOSITES);
        }

        private bool ParseCharMetrics(FontMetrics fontMetrics)
        {
            int countMetrics = ReadInt();
            bool charMetricsRead;
            List<CharMetric> charMetrics = new List<CharMetric>(countMetrics);
            for (int i = 0; i < countMetrics; i++)
            {
                CharMetric charMetric = ParseCharMetric();
                charMetrics.Add(charMetric);
            }
            ReadCommand(END_CHAR_METRICS);
            charMetricsRead = true;
            fontMetrics.CharMetrics = charMetrics;
            return charMetricsRead;
        }

        /**
         * This will parse the kern data.
         *
         * @param fontMetrics The metrics class to put the parsed data into.
         *
         * @throws IOException If there is an error parsing the data.
         */
        private void ParseKernData(FontMetrics fontMetrics)
        {
            string nextCommand;
            while (!(nextCommand = ReadString()).Equals(END_KERN_DATA, StringComparison.Ordinal))
            {
                switch (nextCommand)
                {
                    case START_TRACK_KERN:
                        int countTrackKern = ReadInt();
                        for (int i = 0; i < countTrackKern; i++)
                        {
                            fontMetrics.AddTrackKern(new TrackKern
                            {
                                Degree = ReadInt(),
                                MinPointSize = Readfloat(),
                                MinKern = Readfloat(),
                                MaxPointSize = Readfloat(),
                                MaxKern = Readfloat()
                            });
                        }
                        ReadCommand(END_TRACK_KERN);
                        break;
                    case START_KERN_PAIRS:
                        ParseKernPairs(fontMetrics);
                        break;
                    case START_KERN_PAIRS0:
                        ParseKernPairs0(fontMetrics);
                        break;
                    case START_KERN_PAIRS1:
                        ParseKernPairs1(fontMetrics);
                        break;
                    default:
                        throw new IOException($"Unknown kerning data type '{nextCommand}'");
                }
            }
        }

        private void ParseKernPairs1(FontMetrics fontMetrics)
        {
            int countKernPairs1 = ReadInt();
            for (int i = 0; i < countKernPairs1; i++)
            {
                fontMetrics.AddKernPair1(ParseKernPair());
            }
            ReadCommand(END_KERN_PAIRS);
        }

        private void ParseKernPairs0(FontMetrics fontMetrics)
        {
            int countKernPairs0 = ReadInt();
            for (int i = 0; i < countKernPairs0; i++)
            {
                fontMetrics.AddKernPair0(ParseKernPair());
            }
            ReadCommand(END_KERN_PAIRS);
        }

        private void ParseKernPairs(FontMetrics fontMetrics)
        {
            int countKernPairs = ReadInt();
            for (int i = 0; i < countKernPairs; i++)
            {
                fontMetrics.AddKernPair(ParseKernPair());
            }
            ReadCommand(END_KERN_PAIRS);
        }

        /**
         * This will parse a kern pair from the data stream.
         *
         * @return The kern pair that was parsed from the stream.
         *
         * @throws IOException If there is an error reading from the stream.
         */
        private KernPair ParseKernPair()
        {
            string cmd = ReadString();
            switch (cmd)
            {
                case KERN_PAIR_KP:
                    return new KernPair
                    {
                        FirstKernCharacter = ReadString(),
                        SecondKernCharacter = ReadString(),
                        X = Readfloat(),
                        Y = Readfloat(),
                    };
                case KERN_PAIR_KPH:
                    return new KernPair
                    {
                        FirstKernCharacter = HexTostring(ReadString()),
                        SecondKernCharacter = HexTostring(ReadString()),
                        X = Readfloat(),
                        Y = Readfloat()
                    };
                case KERN_PAIR_KPX:
                    return new KernPair
                    {
                        FirstKernCharacter = ReadString(),
                        SecondKernCharacter = ReadString(),
                        X = Readfloat(),
                        Y = 0
                    };
                case KERN_PAIR_KPY:
                    return new KernPair
                    {
                        FirstKernCharacter = ReadString(),
                        SecondKernCharacter = ReadString(),
                        X = 0,
                        Y = Readfloat()
                    };
                default:
                    throw new IOException($"Error expected kern pair command actual='{cmd}'");
            }
        }

        /**
         * This will convert and angle bracket hex string to a string.
         *
         * @param hexstring An angle bracket string.
         *
         * @return The bytes of the hex string.
         *
         * @throws IOException If the string is input an invalid format.
         */
        private string HexTostring(string hexstring)
        {
            if (hexstring.Length < 2)
            {
                throw new IOException($"Error: Expected hex string of length >= 2 not='{hexstring}");
            }
            if (hexstring[0] != '<' ||
                hexstring[hexstring.Length - 1] != '>')
            {
                throw new IOException($"string should be enclosed by angle brackets '{hexstring}'");
            }
            var slice = hexstring.AsSpan(1, hexstring.Length - 2);
            byte[] data = ConvertUtils.HexToByteArray(slice);
            //new byte[hexstring.Length / 2]
            //for (int i = 0; i < hexstring.Length; i += 2)
            //{
            //    string hex = new string(hexstring[i], hexstring[i + 1]);
            //    try
            //    {
            //        data[i / 2] = (byte)Convert.ToInt32(hex, BITS_IN_HEX);
            //    }
            //    catch (Exception e)
            //    {
            //        throw new IOException($"Error parsing AFM file:{e}");
            //    }
            //}
            return PdfClown.Tokens.Encoding.Pdf.Decode(data);
        }

        /**
         * This will parse a composite part from the stream.
         *
         * @return The composite.
         *
         * @throws IOException If there is an error parsing the composite.
         */
        private Composite ParseComposite()
        {
            string partData = ReadLine();
            var tokenizer = partData.Split(new[] { " ;" }, StringSplitOptions.None);

            int t = 0;
            string cc = tokenizer[t++];
            if (!cc.Equals(CC, StringComparison.Ordinal))
            {
                throw new IOException($"Expected '{CC}' actual='{cc}'");
            }
            string name = tokenizer[t++];
            var composite = new Composite
            {
                Name = name
            };

            int partCount = ParseInt(tokenizer[t++]);
            for (int i = 0; i < partCount; i++)
            {
                string pcc = tokenizer[t++];
                if (!pcc.Equals(PCC, StringComparison.Ordinal))
                {
                    throw new IOException("Expected '" + PCC + "' actual='" + pcc + "'");
                }
                string partName = tokenizer[t++];
                int x = ParseInt(tokenizer[t++]);
                int y = ParseInt(tokenizer[t++]);
                composite.AddPart(new CompositePart
                {
                    Name = partName,
                    XDisplacement = x,
                    YDisplacement = y
                });
            }
            return composite;
        }

        /**
         * This will parse a single CharMetric object from the stream.
         *
         * @return The next char metric input the stream.
         *
         * @throws IOException If there is an error reading from the stream.
         */
        private CharMetric ParseCharMetric()
        {
            var charMetric = new CharMetric();
            string metrics = ReadLine();
            var metricsTokenizer = metrics.Split(new char[] { ' ' });
            for (int i = 0; i < metricsTokenizer.Length;)
            {
                string nextCommand = metricsTokenizer[i++];
                switch (nextCommand)
                {
                    case CHARMETRICS_C:
                        string charCodeC = metricsTokenizer[i++];
                        charMetric.CharacterCode = ParseInt(charCodeC);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_CH:
                        //Is the hex string <FF> or FF, the spec is a little
                        //unclear, wait and see if it breaks anything.
                        string charCodeCH = metricsTokenizer[i++];
                        charMetric.CharacterCode = ParseInt(charCodeCH, BITS_IN_HEX);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_WX:
                        charMetric.Wx = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W0X:
                        charMetric.W0x = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W1X:
                        charMetric.W1x = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_WY:
                        charMetric.Wy = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W0Y:
                        charMetric.W0y = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W1Y:
                        charMetric.W1y = ParseFloat(metricsTokenizer[i++]);
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W:
                        float[] w = new float[2];
                        w[0] = ParseFloat(metricsTokenizer[i++]);
                        w[1] = ParseFloat(metricsTokenizer[i++]);
                        charMetric.W = w;
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W0:
                        float[] w0 = new float[2];
                        w0[0] = ParseFloat(metricsTokenizer[i++]);
                        w0[1] = ParseFloat(metricsTokenizer[i++]);
                        charMetric.W0 = w0;
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_W1:
                        float[] w1 = new float[2];
                        w1[0] = ParseFloat(metricsTokenizer[i++]);
                        w1[1] = ParseFloat(metricsTokenizer[i++]);
                        charMetric.W1 = w1;
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_VV:
                        float[] vv = new float[2];
                        vv[0] = ParseFloat(metricsTokenizer[i++]);
                        vv[1] = ParseFloat(metricsTokenizer[i++]);
                        charMetric.Vv = vv;
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_N:
                        charMetric.Name = metricsTokenizer[i++];
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_B:
                        var box = new SKRect
                        {
                            Left = ParseFloat(metricsTokenizer[i++]),
                            Bottom = ParseFloat(metricsTokenizer[i++]),
                            Right = ParseFloat(metricsTokenizer[i++]),
                            Top = ParseFloat(metricsTokenizer[i++])
                        };
                        charMetric.BoundingBox = box;
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    case CHARMETRICS_L:
                        charMetric.AddLigature(new Ligature
                        {
                            Successor = metricsTokenizer[i++],
                            LigatureValue = metricsTokenizer[i++]
                        });
                        VerifySemicolon(metricsTokenizer, ref i);
                        break;
                    default:
                        throw new IOException("Unknown CharMetrics command '" + nextCommand + "'");
                }
            }

            return charMetric;
        }



        /**
         * This is used to verify that a semicolon is the next token input the stream.
         *
         * @param tokenizer The tokenizer to read from.
         *
         * @throws IOException If the semicolon is missing.
         */
        private void VerifySemicolon(string[] tokenizer, ref int i)
        {
            if (i < tokenizer.Length)
            {
                string semicolon = tokenizer[i++];
                if (!";".Equals(semicolon, StringComparison.Ordinal))
                {
                    throw new IOException("Error: Expected semicolon input stream actual='" + semicolon + "'");
                }
            }
            else
            {
                throw new IOException("CharMetrics is missing a semicolon after a command");
            }
        }

        /**
         * This will read a bool from the stream.
         *
         * @return The bool input the stream.
         */
        private bool ReadBoolean()
        {
            return bool.Parse(ReadString());
        }

        /**
         * This will read an integer from the stream.
         *
         * @return The integer input the stream.
         */
        private int ReadInt()
        {
            return ParseInt(ReadString());
        }

        private static int ParseInt(string charCodeC)
        {
            return ParseInt(charCodeC, 10);
        }

        private static int ParseInt(string intValue, int radix)
        {
            try
            {
                return Convert.ToInt32(intValue, radix);
            }
            catch (Exception e)
            {
                throw new IOException("Error parsing AFM document:" + e, e);
            }
        }

        /**
         * This will read a float from the stream.
         *
         * @return The float input the stream.
         */
        private float Readfloat()
        {
            return ParseFloat(ReadString());
        }

        private float ParseFloat(String floatValue)
        {
            try
            {
                return float.Parse(floatValue, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                throw new IOException("Error parsing AFM document:" + e, e);
            }
        }

        /**
         * This will read until the end of a line.
         *
         * @return The string that is read.
         */
        private string ReadLine()
        {
            //First skip the whitespace
            var buf = new StringBuilder(60);
            int nextByte = input.ReadByte();
            while (IsWhitespace(nextByte))
            {
                nextByte = input.ReadByte();
                //do nothing just skip the whitespace.
            }
            buf.Append((char)nextByte);

            //now read the data
            nextByte = input.ReadByte();
            while (nextByte != -1 && !IsEOL(nextByte))
            {
                buf.Append((char)nextByte);
                nextByte = input.ReadByte();
            }
            return buf.ToString();
        }

        /**
         * This will read a string from the input stream and stop at any whitespace.
         *
         * @return The string read from the stream.
         *
         * @throws IOException If an IO error occurs when reading from the stream.
         */
        private string ReadString()
        {
            //First skip the whitespace
            var buf = new StringBuilder(24);
            int nextByte = input.ReadByte();
            while (IsWhitespace(nextByte))
            {
                nextByte = input.ReadByte();
                //do nothing just skip the whitespace.
            }
            buf.Append((char)nextByte);

            //now read the data
            nextByte = input.ReadByte();
            while (nextByte != -1 && !IsWhitespace(nextByte))
            {
                buf.Append((char)nextByte);
                nextByte = input.ReadByte();
            }
            return buf.ToString();
        }

        private void ReadCommand(string expectedCommand)
        {
            string endTrackKern = ReadString();
            if (!endTrackKern.Equals(expectedCommand, StringComparison.Ordinal))
            {
                throw new IOException($"Error: Expected '{expectedCommand}' actual '{endTrackKern}'");
            }
        }



        /**
         * This will determine if the byte is a whitespace character or not.
         *
         * @param character The character to test for whitespace.
         *
         * @return true If the character is whitespace as defined by the AFM spec.
         */
        private bool IsEOL(int character)
        {
            return character == 0x0D ||
                   character == 0x0A;
        }

        /**
         * This will determine if the byte is a whitespace character or not.
         *
         * @param character The character to test for whitespace.
         *
         * @return true If the character is whitespace as defined by the AFM spec.
         */
        private bool IsWhitespace(int character)
        {
            return character == ' ' ||
                   character == '\t' ||
                   character == 0x0D ||
                   character == 0x0A;
        }
    }
}
