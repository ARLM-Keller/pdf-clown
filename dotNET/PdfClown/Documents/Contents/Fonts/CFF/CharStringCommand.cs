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
using PdfClown.Util;
using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents.Fonts.Type1
{
    /**
	 * This class represents a CharStringCommand.
	 * 
	 * @author Villu Ruusmann
	 */
    public class CharStringCommand
    {
        private static readonly Dictionary<KeyWord, CharStringCommand> CHAR_STRING_COMMANDS = new()
        {
        {KeyWord.HSTEM, new CharStringCommand(KeyWord.HSTEM) },
        {KeyWord.VSTEM, new CharStringCommand(KeyWord.VSTEM) },
        {KeyWord.VMOVETO, new CharStringCommand(KeyWord.VMOVETO) },
        {KeyWord.RLINETO, new CharStringCommand(KeyWord.RLINETO) },
        {KeyWord.HLINETO, new CharStringCommand(KeyWord.HLINETO) },
        {KeyWord.VLINETO, new CharStringCommand(KeyWord.VLINETO) },
        {KeyWord.RRCURVETO, new CharStringCommand(KeyWord.RRCURVETO) },
        {KeyWord.CLOSEPATH, new CharStringCommand(KeyWord.CLOSEPATH) },
        {KeyWord.CALLSUBR, new CharStringCommand(KeyWord.CALLSUBR) },
        {KeyWord.RET, new CharStringCommand(KeyWord.RET) },
        {KeyWord.ESCAPE, new CharStringCommand(KeyWord.ESCAPE) },

        {KeyWord.HSBW, new CharStringCommand(KeyWord.HSBW) },
        {KeyWord.ENDCHAR, new CharStringCommand(KeyWord.ENDCHAR) },
        {KeyWord.HSTEMHM, new CharStringCommand(KeyWord.HSTEMHM) },
        {KeyWord.HINTMASK, new CharStringCommand(KeyWord.HINTMASK) },
        {KeyWord.CNTRMASK, new CharStringCommand(KeyWord.CNTRMASK) },
        {KeyWord.RMOVETO, new CharStringCommand(KeyWord.RMOVETO) },
        {KeyWord.HMOVETO, new CharStringCommand(KeyWord.HMOVETO) },
        {KeyWord.VSTEMHM, new CharStringCommand(KeyWord.VSTEMHM) },
        {KeyWord.RCURVELINE, new CharStringCommand(KeyWord.RCURVELINE) },
        {KeyWord.RLINECURVE, new CharStringCommand(KeyWord.RLINECURVE) },
        {KeyWord.VVCURVETO, new CharStringCommand(KeyWord.VVCURVETO) },
        {KeyWord.HHCURVETO, new CharStringCommand(KeyWord.HHCURVETO) },
        {KeyWord.SHORTINT, new CharStringCommand(KeyWord.SHORTINT) },
        {KeyWord.CALLGSUBR, new CharStringCommand(KeyWord.CALLGSUBR) },
        {KeyWord.VHCURVETO, new CharStringCommand(KeyWord.VHCURVETO) },
        {KeyWord.HVCURVETO, new CharStringCommand(KeyWord.HVCURVETO) },

        // two byte commands
        {KeyWord.DOTSECTION, new CharStringCommand(12, 0) },
        {KeyWord.VSTEM3, new CharStringCommand(12, 1) },
        {KeyWord.HSTEM3, new CharStringCommand(12, 2) },
        {KeyWord.AND, new CharStringCommand(12, 3) },
        {KeyWord.OR, new CharStringCommand(12, 4) },
        {KeyWord.NOT, new CharStringCommand(12, 5) },
        {KeyWord.SEAC, new CharStringCommand(12, 6) },
        {KeyWord.SBW, new CharStringCommand(12, 7) },
        {KeyWord.ABS, new CharStringCommand(12, 9) },
        {KeyWord.ADD, new CharStringCommand(12, 10) },
        {KeyWord.SUB, new CharStringCommand(12, 11) },
        {KeyWord.DIV, new CharStringCommand(12, 12) },
        {KeyWord.NEG, new CharStringCommand(12, 14) },
        {KeyWord.EQ, new CharStringCommand(12, 15) },
        {KeyWord.CALLOTHERSUBR, new CharStringCommand(12, 16) },
        {KeyWord.POP, new CharStringCommand(12, 17) },
        {KeyWord.DROP, new CharStringCommand(12, 18) },
        {KeyWord.PUT, new CharStringCommand(12, 20) },
        {KeyWord.GET, new CharStringCommand(12, 21) },
        {KeyWord.IFELSE, new CharStringCommand(12, 22) },
        {KeyWord.RANDOM, new CharStringCommand(12, 23) },
        {KeyWord.MUL, new CharStringCommand(12, 24) },
        {KeyWord.SQRT, new CharStringCommand(12, 26) },
        {KeyWord.DUP, new CharStringCommand(12, 27) },
        {KeyWord.EXCH, new CharStringCommand(12, 28) },
        {KeyWord.INDEX, new CharStringCommand(12, 29) },
        {KeyWord.ROLL, new CharStringCommand(12, 30) },
        {KeyWord.SETCURRENTPOINT, new CharStringCommand(12, 33) },
        {KeyWord.HFLEX, new CharStringCommand(12, 34) },
        {KeyWord.FLEX, new CharStringCommand(12, 35) },
        {KeyWord.HFLEX1, new CharStringCommand(12, 36) },
        {KeyWord.FLEX1, new CharStringCommand(12, 37) },
        };

        public static readonly CharStringCommand COMMAND_CLOSEPATH = GetInstance(KeyWord.CLOSEPATH);
        public static readonly CharStringCommand COMMAND_RLINETO = GetInstance(KeyWord.RLINETO);
        public static readonly CharStringCommand COMMAND_HLINETO = GetInstance(KeyWord.HLINETO);
        public static readonly CharStringCommand COMMAND_VLINETO = GetInstance(KeyWord.VLINETO);
        public static readonly CharStringCommand COMMAND_RRCURVETO = GetInstance(KeyWord.RRCURVETO);
        public static readonly CharStringCommand COMMAND_HSBW = GetInstance(KeyWord.HSBW);
        public static readonly CharStringCommand COMMAND_CALLOTHERSUBR = GetInstance(KeyWord.CALLOTHERSUBR);

        private static readonly byte KEY_UNKNOWN = 99;
        public static readonly CharStringCommand COMMAND_UNKNOWN = new CharStringCommand(KEY_UNKNOWN, 0);

        /**
     * Get an instance of the CharStringCommand represented by the given value.
     * 
     * @param b0 value
     * @return CharStringCommand represented by the given value
     */
        public static CharStringCommand GetInstance(KeyWord keyword)
        {
            return CHAR_STRING_COMMANDS.TryGetValue(keyword, out CharStringCommand command) ? command : COMMAND_UNKNOWN;
        }

        public static CharStringCommand GetInstance(int b0)
        {
            return CHAR_STRING_COMMANDS.TryGetValue((KeyWord)b0, out CharStringCommand command) ? command : COMMAND_UNKNOWN;
        }

        public static CharStringCommand GetInstance(int b0, int b1)
        {
            return CHAR_STRING_COMMANDS.TryGetValue((KeyWord)((b0 << 8) | b1), out CharStringCommand command) ? command : COMMAND_UNKNOWN;
        }

        private readonly KeyWord keyWord;

        /**
     * Get an instance of the CharStringCommand represented by the given array.
     * 
     * @param values array of values
     * 
     * @return CharStringCommand represented by the given values
     */
        public static CharStringCommand GetInstance(byte[] values)
        {
            if (values.Length == 1)
            {
                return GetInstance(values[0]);
            }
            else if (values.Length == 2)
            {
                return GetInstance(values[0], values[1]);
            }
            return COMMAND_UNKNOWN;
        }

        /**
     * Constructor with the CharStringCommand key as value.
     * 
     * @param key the key of the char string command
     */
        private CharStringCommand(KeyWord key)
        {
            keyWord = (KeyWord)((int)key);
        }

        /**
		 * Constructor with two values.
		 * 
		 * @param b0 value1
		 * @param b1 value2
		 */
        private CharStringCommand(int b0, int b1)
        {
            keyWord = (KeyWord)((b0 << 8) | b1);
        }

        /**
		 * Constructor with an array as values.
		 * 
		 * @param values array of values
		 */
        private CharStringCommand(int values)
        {
            keyWord = (KeyWord)values;
        }

        public Type1KeyWord Type1KeyWord
        {
            get => (Type1KeyWord)keyWord;
        }

        public Type2KeyWord Type2KeyWord
        {
            get => (Type2KeyWord)keyWord;
        }

        /**
		 * {@inheritDoc}
		 */
        override public string ToString()
        {
            string str = Enum.GetName(typeof(KeyWord), keyWord);
            if (str == null)
            {
                return ((int)keyWord).ToString() + '|';
            }
            return str + '|';
        }

        /**
		 * {@inheritDoc}
		 */
        override public int GetHashCode()
        {
            return keyWord.GetHashCode();
        }

        /**
		 * {@inheritDoc}
		 */
        override public bool Equals(object obj)
        {
            if (obj is CharStringCommand that)
            {
                return keyWord == that.keyWord;
            }
            return false;
        }

        /**
		 * A map with the Type1 vocabulary.
		 */
        public static readonly Dictionary<ByteKey, Type1KeyWord> TYPE1_VOCABULARY = new(26)
        {
            { new ByteKey(1), Type1KeyWord.HSTEM },
            { new ByteKey(3), Type1KeyWord.VSTEM },
            { new ByteKey(4), Type1KeyWord.VMOVETO },
            { new ByteKey(5), Type1KeyWord.RLINETO },
            { new ByteKey(6), Type1KeyWord.HLINETO },
            { new ByteKey(7), Type1KeyWord.VLINETO },
            { new ByteKey(8), Type1KeyWord.RRCURVETO },
            { new ByteKey(9), Type1KeyWord.CLOSEPATH },
            { new ByteKey(10), Type1KeyWord.CALLSUBR },
            { new ByteKey(11), Type1KeyWord.RET },
            { new ByteKey(12), Type1KeyWord.ESCAPE },
            { new ByteKey(12, 0), Type1KeyWord.DOTSECTION },
            { new ByteKey(12, 1), Type1KeyWord.VSTEM3 },
            { new ByteKey(12, 2), Type1KeyWord.HSTEM3 },
            { new ByteKey(12, 6), Type1KeyWord.SEAC },
            { new ByteKey(12, 7), Type1KeyWord.SBW },
            { new ByteKey(12, 12), Type1KeyWord.DIV },
            { new ByteKey(12, 16), Type1KeyWord.CALLOTHERSUBR },
            { new ByteKey(12, 17), Type1KeyWord.POP },
            { new ByteKey(12, 33), Type1KeyWord.SETCURRENTPOINT },
            { new ByteKey(13), Type1KeyWord.HSBW },
            { new ByteKey(14), Type1KeyWord.ENDCHAR },
            { new ByteKey(21), Type1KeyWord.RMOVETO },
            { new ByteKey(22), Type1KeyWord.HMOVETO },
            { new ByteKey(30), Type1KeyWord.VHCURVETO },
            { new ByteKey(31), Type1KeyWord.HVCURVETO },
        };

        /**
		 * A map with the Type2 vocabulary.
		 */
        public static readonly Dictionary<ByteKey, Type2KeyWord> TYPE2_VOCABULARY = new(50)
        {
            { new ByteKey(1), Type2KeyWord.HSTEM},
            { new ByteKey(3), Type2KeyWord.VSTEM},
            { new ByteKey(4), Type2KeyWord.VMOVETO},
            { new ByteKey(5), Type2KeyWord.RLINETO},
            { new ByteKey(6), Type2KeyWord.HLINETO},
            { new ByteKey(7), Type2KeyWord.VLINETO},
            { new ByteKey(8), Type2KeyWord.RRCURVETO},
            { new ByteKey(10), Type2KeyWord.CALLSUBR},
            { new ByteKey(11), Type2KeyWord.RET},
            { new ByteKey(12), Type2KeyWord.ESCAPE},
            { new ByteKey(12, 3), Type2KeyWord.AND},
            { new ByteKey(12, 4), Type2KeyWord.OR},
            { new ByteKey(12, 5), Type2KeyWord.NOT},
            { new ByteKey(12, 9), Type2KeyWord.ABS},
            { new ByteKey(12, 10), Type2KeyWord.ADD},
            { new ByteKey(12, 11), Type2KeyWord.SUB},
            { new ByteKey(12, 12), Type2KeyWord.DIV},
            { new ByteKey(12, 14), Type2KeyWord.NEG},
            { new ByteKey(12, 15), Type2KeyWord.EQ},
            { new ByteKey(12, 18), Type2KeyWord.DROP},
            { new ByteKey(12, 20), Type2KeyWord.PUT},
            { new ByteKey(12, 21), Type2KeyWord.GET},
            { new ByteKey(12, 22), Type2KeyWord.IFELSE},
            { new ByteKey(12, 23), Type2KeyWord.RANDOM},
            { new ByteKey(12, 24), Type2KeyWord.MUL},
            { new ByteKey(12, 26), Type2KeyWord.SQRT},
            { new ByteKey(12, 27), Type2KeyWord.DUP},
            { new ByteKey(12, 28), Type2KeyWord.EXCH},
            { new ByteKey(12, 29), Type2KeyWord.INDEX},
            { new ByteKey(12, 30), Type2KeyWord.ROLL},
            { new ByteKey(12, 34), Type2KeyWord.HFLEX},
            { new ByteKey(12, 35), Type2KeyWord.FLEX},
            { new ByteKey(12, 36), Type2KeyWord.HFLEX1},
            { new ByteKey(12, 37), Type2KeyWord.FLEX1},
            { new ByteKey(14), Type2KeyWord.ENDCHAR},
            { new ByteKey(18), Type2KeyWord.HSTEMHM},
            { new ByteKey(19), Type2KeyWord.HINTMASK},
            { new ByteKey(20), Type2KeyWord.CNTRMASK},
            { new ByteKey(21), Type2KeyWord.RMOVETO},
            { new ByteKey(22), Type2KeyWord.HMOVETO},
            { new ByteKey(23), Type2KeyWord.VSTEMHM},
            { new ByteKey(24), Type2KeyWord.RCURVELINE},
            { new ByteKey(25), Type2KeyWord.RLINECURVE},
            { new ByteKey(26), Type2KeyWord.VVCURVETO},
            { new ByteKey(27), Type2KeyWord.HHCURVETO},
            { new ByteKey(28), Type2KeyWord.SHORTINT},
            { new ByteKey(29), Type2KeyWord.CALLGSUBR},
            { new ByteKey(30), Type2KeyWord.VHCURVETO},
            { new ByteKey(31), Type2KeyWord.HVCURVETO},
        };
    }


    /**
        * Enum of all valid type1 key words
        */
    public enum Type1KeyWord
    {
        HSTEM = KeyWord.HSTEM, VSTEM = KeyWord.VSTEM, VMOVETO = KeyWord.VMOVETO, RLINETO = KeyWord.RLINETO, //
        HLINETO = KeyWord.HLINETO, VLINETO = KeyWord.VLINETO, RRCURVETO = KeyWord.RRCURVETO, //
        CLOSEPATH = KeyWord.CLOSEPATH, CALLSUBR = KeyWord.CALLSUBR, RET = KeyWord.RET, //
        ESCAPE = KeyWord.ESCAPE, DOTSECTION = KeyWord.DOTSECTION, //
        VSTEM3 = KeyWord.VSTEM3, HSTEM3 = KeyWord.HSTEM3, SEAC = KeyWord.SEAC, SBW = KeyWord.SBW, //
        DIV = KeyWord.DIV, CALLOTHERSUBR = KeyWord.CALLOTHERSUBR, POP = KeyWord.POP, //
        SETCURRENTPOINT = KeyWord.SETCURRENTPOINT, HSBW = KeyWord.HSBW, ENDCHAR = KeyWord.ENDCHAR, //
        RMOVETO = KeyWord.RMOVETO, HMOVETO = KeyWord.HMOVETO, VHCURVETO = KeyWord.VHCURVETO, //
        HVCURVETO = KeyWord.HVCURVETO
    }


    /**
        * Enum of all valid type2 key words
        */
    public enum Type2KeyWord
    {
        HSTEM = KeyWord.HSTEM,
        VSTEM = KeyWord.VSTEM,
        VMOVETO = KeyWord.VMOVETO,
        RLINETO = KeyWord.RLINETO, //
        HLINETO = KeyWord.HLINETO,
        VLINETO = KeyWord.VLINETO,
        RRCURVETO = KeyWord.RRCURVETO,
        CALLSUBR = KeyWord.CALLSUBR, //
        RET = KeyWord.RET, ESCAPE = KeyWord.ESCAPE, AND = KeyWord.AND, OR = KeyWord.OR, //
        NOT = KeyWord.NOT, ABS = KeyWord.ABS, ADD = KeyWord.ADD, SUB = KeyWord.SUB, //
        DIV = KeyWord.DIV, NEG = KeyWord.NEG, EQ = KeyWord.EQ, DROP = KeyWord.DROP, //
        PUT = KeyWord.PUT, GET = KeyWord.GET, IFELSE = KeyWord.IFELSE, //
        RANDOM = KeyWord.RANDOM, MUL = KeyWord.MUL, SQRT = KeyWord.SQRT, DUP = KeyWord.DUP, //
        EXCH = KeyWord.EXCH, INDEX = KeyWord.INDEX, ROLL = KeyWord.ROLL, //
        HFLEX = KeyWord.HFLEX, FLEX = KeyWord.FLEX, HFLEX1 = KeyWord.HFLEX1, //
        FLEX1 = KeyWord.FLEX1, ENDCHAR = KeyWord.ENDCHAR, HSTEMHM = KeyWord.HSTEMHM, HINTMASK = KeyWord.HINTMASK, //
        CNTRMASK = KeyWord.CNTRMASK, RMOVETO = KeyWord.RMOVETO, HMOVETO = KeyWord.HMOVETO, VSTEMHM = KeyWord.VSTEMHM, //
        RCURVELINE = KeyWord.RCURVELINE, RLINECURVE = KeyWord.RLINECURVE, VVCURVETO = KeyWord.VVCURVETO, //
        HHCURVETO = KeyWord.HHCURVETO, SHORTINT = KeyWord.SHORTINT, CALLGSUBR = KeyWord.CALLGSUBR, //
        VHCURVETO = KeyWord.VHCURVETO, HVCURVETO = KeyWord.HVCURVETO
    }


    public enum KeyWord
    {
        HSTEM = 1,
        VSTEM = 3,
        VMOVETO = 4,
        RLINETO = 5, //
        HLINETO = 6,
        VLINETO = 7,
        RRCURVETO = 8,
        CLOSEPATH = 9,
        CALLSUBR = 10, //
        RET = 11,
        ESCAPE = 12,
        DOTSECTION = (12 << 8) | 0,
        VSTEM3 = (12 << 8) | 1,
        HSTEM3 = (12 << 8) | 2, //
        AND = (12 << 8) | 3,
        OR = (12 << 8) | 4,
        NOT = (12 << 8) | 5,
        SEAC = (12 << 8) | 6,
        SBW = (12 << 8) | 7, //
        ABS = (12 << 8) | 9,
        ADD = (12 << 8) | 10,
        SUB = (12 << 8) | 11,
        DIV = (12 << 8) | 12,
        NEG = (12 << 8) | 14,
        EQ = (12 << 8) | 15, //
        CALLOTHERSUBR = (12 << 8) | 16,
        POP = (12 << 8) | 17,
        DROP = (12 << 8) | 18, //
        PUT = (12 << 8) | 20,
        GET = (12 << 8) | 21,
        IFELSE = (12 << 8) | 22, //
        RANDOM = (12 << 8) | 23,
        MUL = (12 << 8) | 24,
        SQRT = (12 << 8) | 26,
        DUP = (12 << 8) | 27, //
        EXCH = (12 << 8) | 28,
        INDEX = (12 << 8) | 29,
        ROLL = (12 << 8) | 30,
        SETCURRENTPOINT = (12 << 8) | 33, //
        HFLEX = (12 << 8) | 34,
        FLEX = (12 << 8) | 35,
        HFLEX1 = (12 << 8) | 36,
        FLEX1 = (12 << 8) | 37, //
        HSBW = 13,
        ENDCHAR = 14,
        HSTEMHM = 18,
        HINTMASK = 19, //
        CNTRMASK = 20,
        RMOVETO = 21,
        HMOVETO = 22,
        VSTEMHM = 23, //
        RCURVELINE = 24,
        RLINECURVE = 25,
        VVCURVETO = 26, //
        HHCURVETO = 27,
        SHORTINT = 28,
        CALLGSUBR = 29, //
        VHCURVETO = 30,
        HVCURVETO = 31
    }

}
