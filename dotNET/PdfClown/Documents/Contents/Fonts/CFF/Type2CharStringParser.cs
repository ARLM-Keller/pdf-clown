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
using PdfClown.Documents.Contents.Fonts.Type1;
using PdfClown.Util.Collections;
using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents.Fonts.CCF
{

    /**
     * This class represents a converter for a mapping into a Type2-sequence.
     * @author Villu Ruusmann
     */
    public class Type2CharStringParser
    {
        // 1-byte commands
        private static readonly int CALLSUBR = 10;
        private static readonly int CALLGSUBR = 29;

        private int hstemCount;
        private int vstemCount;
        private readonly List<Object> sequence = new List<object>();
        private readonly string fontName;
        private string currentGlyph;

        /**
         * Constructs a new Type1CharStringParser object for a Type 1-equivalent font.
         *
         * @param fontName font name
         */
        public Type2CharStringParser(string fontName)
        {
            this.fontName = fontName;
        }

        /**
         * The given byte array will be parsed and converted to a Type2 sequence.
         * 
         * @param bytes the given mapping as byte array
         * @param globalSubrIndex array containing all global subroutines
         * @param localSubrIndex array containing all local subroutines
         * 
         * @return the Type2 sequence
         * @throws IOException if an error occurs during reading
         */
        public List<Object> Parse(Memory<byte> bytes, Memory<byte>[] globalSubrIndex, Memory<byte>[] localSubrIndex, string glyphName)
        {
            // reset values if the parser is used multiple times
            hstemCount = 0;
            vstemCount = 0;
            // create a new list as it is used as return value
            sequence.Clear();
            currentGlyph = glyphName;
            return ParseSequence(bytes, globalSubrIndex, localSubrIndex);
        }

        private List<Object> ParseSequence(Memory<byte> bytes, Memory<byte>[] globalSubrIndex, Memory<byte>[] localSubrIndex)
        {
            var input = new ByteStream(bytes);
            bool localSubroutineIndexProvided = localSubrIndex != null && localSubrIndex.Length > 0;
            bool globalSubroutineIndexProvided = globalSubrIndex != null && globalSubrIndex.Length > 0;

            while (input.HasRemaining())
            {
                var b0 = input.ReadUByte();
                if (b0 == CALLSUBR && localSubroutineIndexProvided)
                {
                    ProcessCallSubr(globalSubrIndex, localSubrIndex);
                }
                else if (b0 == CALLGSUBR && globalSubroutineIndexProvided)
                {
                    ProcessCallGSubr(globalSubrIndex, localSubrIndex);
                }
                else if ((b0 >= 0 && b0 <= 27) || (b0 >= 29 && b0 <= 31))
                {
                    sequence.Add(ReadCommand(b0, input));
                }
                else if (b0 == 28 || (b0 >= 32 && b0 <= 255))
                {
                    sequence.Add(ReadNumber(b0, input));
                }
                else
                {
                    throw new ArgumentException();
                }
            }
            return sequence;
        }

        private void ProcessCallSubr(Memory<byte>[] globalSubrIndex, Memory<byte>[] localSubrIndex)
        {
            int subrNumber = CalculateSubrNumber((int)(float)sequence.RemoveAtValue(sequence.Count - 1),
                    localSubrIndex.Length);
            if (subrNumber < localSubrIndex.Length)
            {
                var subrBytes = localSubrIndex[subrNumber];
                ParseSequence(subrBytes, globalSubrIndex, localSubrIndex);
                var lastItem = sequence[sequence.Count - 1];
                if (lastItem is CharStringCommand
                            && Type2KeyWord.RET == ((CharStringCommand)lastItem).Type2KeyWord)
                {
                    // RemoveAt "return" command
                    sequence.RemoveAt(sequence.Count - 1);
                }
            }
        }

        private void ProcessCallGSubr(Memory<byte>[] globalSubrIndex, Memory<byte>[] localSubrIndex)
        {
            int subrNumber = CalculateSubrNumber((int)(float)sequence.RemoveAtValue(sequence.Count - 1),
                    globalSubrIndex.Length);
            if (subrNumber < globalSubrIndex.Length)
            {
                var subrBytes = globalSubrIndex[subrNumber];
                ParseSequence(subrBytes, globalSubrIndex, localSubrIndex);
                var lastItem = sequence[sequence.Count - 1];
                if (lastItem is CharStringCommand
                            && Type2KeyWord.RET == ((CharStringCommand)lastItem).Type2KeyWord)
                {
                    // RemoveAt "return" command
                    sequence.RemoveAt(sequence.Count - 1);
                }
            }
        }

        private int CalculateSubrNumber(int operand, int subrIndexlength)
        {
            if (subrIndexlength < 1240)
            {
                return 107 + operand;
            }
            if (subrIndexlength < 33900)
            {
                return 1131 + operand;
            }
            return 32768 + operand;
        }

        private CharStringCommand ReadCommand(byte b0, IInputStream input)
        {

            if (b0 == 1 || b0 == 18)
            {
                hstemCount += CountNumbers() / 2;
            }
            else if (b0 == 3 || b0 == 19 || b0 == 20 || b0 == 23)
            {
                vstemCount += CountNumbers() / 2;
            } // End if

            if (b0 == 12)
            {
                var b1 = input.ReadUByte();

                return CharStringCommand.GetInstance(b0, b1);
            }
            else if (b0 == 19 || b0 == 20)
            {
                byte[] value = new byte[1 + GetMaskLength()];
                value[0] = b0;

                for (int i = 1; i < value.Length; i++)
                {
                    value[i] = input.ReadUByte();
                }

                return CharStringCommand.GetInstance(value);
            }

            return CharStringCommand.GetInstance(b0);
        }

        private float ReadNumber(int b0, IInputStream input)
        {
            if (b0 == 28)
            {
                return (int)input.ReadInt16();
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
            else if (b0 == 255)
            {
                short value = input.ReadInt16();
                // The lower bytes are representing the digits after the decimal point
                float fraction = input.ReadUInt16() / 65535f;
                return value + fraction;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private int GetMaskLength()
        {
            int hintCount = hstemCount + vstemCount;
            int Length = hintCount / 8;
            if (hintCount % 8 > 0)
            {
                Length++;
            }
            return Length;
        }

        private int CountNumbers()
        {
            int count = 0;
            for (int i = sequence.Count - 1; i > -1; i--)
            {
                if (!(sequence[i] is float))
                {
                    return count;
                }
                count++;
            }
            return count;
        }

        public override string ToString()
        {
            return fontName + ", current glpyh " + currentGlyph;
        }
    }
}
