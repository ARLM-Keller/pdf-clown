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
using System.IO;

namespace PdfClown.Documents.Functions.Type4
{


    /**
     * Parser for PDF Type 4 functions. This implements a small subset of the PostScript
     * language but is no full PostScript interpreter.
     *
     */
    public sealed class Parser
    {

        /** Used to indicate the parsers current state. */
        private enum State
        {
            NEWLINE, WHITESPACE, COMMENT, TOKEN
        }

        private Parser()
        {
            //nop
        }

        /**
         * Parses a Type 4 function and sends the syntactic elements to the given
         * syntax handler.
         * @param input the text source
         * @param handler the syntax handler
         */
        public static void Parse(StreamReader input, SyntaxHandler handler)
        {
            var tokenizer = new Tokenizer(input, handler);
            tokenizer.Tokenize();
        }

        /**
         * This interface defines all possible syntactic elements of a Type 4 function.
         * It is called by the parser as the function is interpreted.
         */
        public interface SyntaxHandler
        {

            /**
             * Indicates that a new line starts.
             * @param text the new line character (CR, LF, CR/LF or FF)
             */
            void NewLine(StringStream text);

            /**
             * Called when whitespace characters are encountered.
             * @param text the whitespace text
             */
            void Whitespace(StringStream text);

            /**
             * Called when a token is encountered. No distinction between operators and values
             * is done here.
             * @param text the token text
             */
            void Token(StringStream text);

            /**
             * Called for a comment.
             * @param text the comment
             */
            void Comment(StringStream text);
        }

        /**
         * Abstract base class for a {@link SyntaxHandler}.
         */
        public abstract class AbstractSyntaxHandler : SyntaxHandler
        {
            public abstract void Token(StringStream text);

            /** {@inheritDoc} */
            public virtual void Comment(StringStream text)
            {
                //nop
            }

            /** {@inheritDoc} */
            public virtual void NewLine(StringStream text)
            {
                //nop
            }

            /** {@inheritDoc} */
            public virtual void Whitespace(StringStream text)
            {
                //nop
            }

        }

        /**
         * Tokenizer for Type 4 functions.
         */
        internal sealed class Tokenizer
        {

            private const char NUL = '\u0000'; //NUL
            private const char EOT = '\u0004'; //END OF TRANSMISSION
            private const char TAB = '\u0009'; //TAB CHARACTER
            private const char FF = '\u000C'; //FORM FEED
            private const char CR = '\r'; //CARRIAGE RETURN
            private const char LF = '\n'; //LINE FEED
            private const char SPACE = '\u0020'; //SPACE

            private readonly StreamReader input;
            private readonly SyntaxHandler handler;
            private State state = State.WHITESPACE;
            private char current;
            private readonly StringStream buffer = new();

            public Tokenizer(StreamReader text, SyntaxHandler syntaxHandler)
            {
                this.input = text;
                this.handler = syntaxHandler;
            }

            private bool HasMore()
            {
                return !input.EndOfStream;
            }

            private char CurrentChar()
            {
                return current;
            }

            private char NextChar()
            {
                current = (char)input.Read();
                if (input.EndOfStream)
                {
                    return EOT;
                }
                else
                {
                    return CurrentChar();
                }
            }

            private char Peek()
            {
                var peek = input.Peek();
                if (peek >= 0)
                {
                    return (char)peek;
                }
                else
                {
                    return EOT;
                }
            }

            private State NextState()
            {
                char ch = CurrentChar();
                switch (ch)
                {
                    case CR:
                    case LF:
                    case FF: //FF
                        state = State.NEWLINE;
                        break;
                    case NUL:
                    case TAB:
                    case SPACE:
                        state = State.WHITESPACE;
                        break;
                    case '%':
                        state = State.COMMENT;
                        break;
                    default:
                        state = State.TOKEN;
                        break;
                }
                return state;
            }

            public void Tokenize()
            {
                while (HasMore())
                {
                    buffer.SetLength(0);
                    NextState();
                    switch (state)
                    {
                        case State.NEWLINE:
                            ScanNewLine();
                            break;
                        case State.WHITESPACE:
                            ScanWhitespace();
                            break;
                        case State.COMMENT:
                            ScanComment();
                            break;
                        default:
                            ScanToken();
                            break;
                    }
                }
            }

            private void ScanNewLine()
            {
                char ch = CurrentChar();
                buffer.Append(ch);
                if (ch == CR && Peek() == LF)
                {
                    //CRLF is treated as one newline
                    buffer.Append(NextChar());
                }
                handler.NewLine(buffer);
                NextChar();
            }

            private void ScanWhitespace()
            {
                buffer.Append(CurrentChar());
                while (HasMore())
                {
                    char ch = NextChar();
                    switch (ch)
                    {
                        case NUL:
                        case TAB:
                        case SPACE:
                            buffer.Append(ch);
                            break;
                        default:
                            goto breakLoop;
                    }
                }
            breakLoop:
                handler.Whitespace(buffer);
            }

            private void ScanComment()
            {
                buffer.Append(CurrentChar());
                while (HasMore())
                {
                    char ch = NextChar();
                    switch (ch)
                    {
                        case CR:
                        case LF:
                        case FF:
                            goto loop;
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }
            loop:
                //EOF reached
                handler.Comment(buffer);
            }

            private void ScanToken()
            {
                char ch = CurrentChar();
                buffer.Append(ch);
                switch (ch)
                {
                    case '{':
                    case '}':
                        handler.Token(buffer);
                        NextChar();
                        return;
                    default:
                        break;
                        //continue
                }
                while (HasMore())
                {
                    ch = NextChar();
                    switch (ch)
                    {
                        case NUL:
                        case TAB:
                        case SPACE:
                        case CR:
                        case LF:
                        case FF:
                        case EOT:
                        case '{':
                        case '}':
                            goto loop;
                        default:
                            buffer.Append(ch);
                            break;
                    }
                }
            loop:
                //EOF reached
                handler.Token(buffer);
            }

        }

    }
}