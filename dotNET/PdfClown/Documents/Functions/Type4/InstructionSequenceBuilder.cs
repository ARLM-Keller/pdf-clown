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
using PdfClown.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace PdfClown.Documents.Functions.Type4
{
    /**
	 * Basic parser for Type 4 functions which is used to build up instruction sequences.
	 *
	 */
    public sealed class InstructionSequenceBuilder : Parser.AbstractSyntaxHandler
    {
        private readonly InstructionSequence mainSequence = new();
        private readonly Stack<InstructionSequence> seqStack = new();

        private InstructionSequenceBuilder()
        {
            this.seqStack.Push(this.mainSequence);
        }

        /**
		 * Returns the instruction sequence that has been build from the syntactic elements.
		 * @return the instruction sequence
		 */
        public InstructionSequence InstructionSequence
        {
            get => this.mainSequence;
        }

        /**
		 * Parses the given text into an instruction sequence representing a Type 4 function
		 * that can be executed.
		 * @param text the Type 4 function text
		 * @return the instruction sequence
		 */
        public static InstructionSequence Parse(StreamReader text)
        {
            var builder = new InstructionSequenceBuilder();
            Parser.Parse(text, builder);
            return builder.InstructionSequence;
        }

        private InstructionSequence CurrentSequence
        {
            get => this.seqStack.Peek();
        }

        /** {@inheritDoc} */
        public override void Token(StringStream text)
        {
            var token = (ReadOnlySpan<Char>)text.AsSpan();
            if (token.Equals("{", StringComparison.Ordinal))
            {
                var child = new InstructionSequence();
                CurrentSequence.AddProc(child);
                this.seqStack.Push(child);
            }
            else if (token.Equals("}", StringComparison.Ordinal))
            {
                this.seqStack.Pop();
            }
            else
            {

                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    CurrentSequence.AddInteger(intValue);
                    return;
                }

                if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    CurrentSequence.AddReal(floatValue);
                    return;
                }

                //TODO Maybe implement radix numbers, such as 8#1777 or 16#FFFE

                CurrentSequence.AddName(token.ToString());
            }
        }

        /**
		 * Parses a value of type "int".
		 * @param token the token to be parsed
		 * @return the parsed value
		 */
        public static int ParseInt(ReadOnlySpan<char> token)
        {
            return int.Parse(token);
        }

        /**
		 * Parses a value of type "real".
		 * @param token the token to be parsed
		 * @return the parsed value
		 */
        public static float ParseReal(ReadOnlySpan<char> token)
        {
            return float.Parse(token);
        }

    }
}
