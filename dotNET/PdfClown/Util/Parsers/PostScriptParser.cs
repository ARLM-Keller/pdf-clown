/*
  Copyright 2011-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)

  This file should be part of the source code distribution of "PDF Clown library" (the
  Program): see the accompanying README files for more info.

  This Program is free software; you can redistribute it and/or modify it under the terms
  of the GNU Lesser General Public License as published by the Free Software Foundation;
  either version 3 of the License, or (at your option) any later version.

  This Program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY,
  either expressed or implied; without even the implied warranty of MERCHANTABILITY or
  FITNESS FOR A PARTICULAR PURPOSE. See the License for more details.

  You should have received a copy of the GNU Lesser General Public License along with this
  Program (see README files); if not, go to the GNU website (http://www.gnu.org/licenses/).

  Redistribution and use, with or without modification, are permitted provided that such
  redistributions retain the above copyright notice, license and disclaimer, along with
  this list of conditions.
*/

using PdfClown.Bytes;
using PdfClown.Tokens;
using PdfClown.Util.IO;
using System;
using System.Globalization;
using System.IO;

namespace PdfClown.Util.Parsers
{
    /**
      <summary>PostScript (non-procedural subset) parser [PS].</summary>
    */
    public class PostScriptParser : IDisposable
    {
        public enum TokenTypeEnum // [PS:3.3].
        {
            Keyword,
            Boolean,
            Integer,
            Real,
            Literal,
            Hex,
            Name,
            Comment,
            ArrayBegin,
            ArrayEnd,
            DictionaryBegin,
            DictionaryEnd,
            Null,
            Reference,
            InderectObject
        }

        protected static int GetHex(int c)
        {
            if (c >= '0' && c <= '9')
                return (c - '0');
            else if (c >= 'A' && c <= 'F')
                return (c - 'A' + 10);
            else if (c >= 'a' && c <= 'f')
                return (c - 'a' + 10);
            else
                return -1;
        }

        /**
          <summary>Evaluate whether a character is a delimiter.</summary>
        */
        protected static bool IsDelimiter(int c)
        {
            return c == Symbol.OpenRoundBracket
              || c == Symbol.CloseRoundBracket
              || c == Symbol.OpenAngleBracket
              || c == Symbol.CloseAngleBracket
              || c == Symbol.OpenSquareBracket
              || c == Symbol.CloseSquareBracket
              || c == Symbol.Slash
              || c == Symbol.Percent;
        }

        /**
          <summary>Evaluate whether a character is an EOL marker.</summary>
        */
        protected static bool IsEOL(int c)
        { return (c == 10 || c == 13); }

        /**
          <summary>Evaluate whether a character is a white-space.</summary>
        */
        protected static bool IsWhitespace(int c)
        { return c == 32 || IsEOL(c) || c == 0 || c == 9 || c == 12; }

        private IInputStream stream;
        private object token;
        private TokenTypeEnum tokenType;
        private StringStream sBuffer = new StringStream();
        private MemoryStream mBuffer = new MemoryStream();

        public PostScriptParser(IInputStream stream)
        {
            this.stream = stream;
        }

        public PostScriptParser(Memory<byte> data) : this(new ByteStream(data))
        { }

        ~PostScriptParser()
        { Dispose(false); }

        public override int GetHashCode() => stream.GetHashCode();

        /**
          <summary>Gets a token after moving to the given offset.</summary>
          <param name="offset">Number of tokens to skip before reaching the intended one.</param>
          <seealso cref="Token"/>
        */
        public object GetToken(int offset = 1)
        {
            MoveNext(offset);
            return Token;
        }

        public long Length => stream.Length;

        /**
          <summary>Moves the pointer to the next token.</summary>
          <param name="offset">Number of tokens to skip before reaching the intended one.</param>
        */
        public bool MoveNext(int offset)
        {
            for (int index = 0; index < offset; index++)
            {
                if (!MoveNext())
                    return false;
            }
            return true;
        }

        /**
          <summary>Moves the pointer to the next token.</summary>
          <remarks>To properly parse the current token, the pointer MUST be just before its starting
          (leading whitespaces are ignored). When this method terminates, the pointer IS
          at the last byte of the current token.</remarks>
          <returns>Whether a new token was found.</returns>
        */
        public virtual bool MoveNext()
        {
            mBuffer.Reset();
            sBuffer.Reset();

            token = null;
            tokenType = (TokenTypeEnum)(-1);
            // Skip leading white-space characters.
            int c = ReadIgnoreWhitespace();
            if (c == -1)
                return false;

            // Which character is it?
            switch (c)
            {
                case Symbol.Slash: // Name.
                    {
                        tokenType = TokenTypeEnum.Name;

                        /*
                          NOTE: As name objects are simple symbols uniquely defined by sequences of characters,
                          the bytes making up the name are never treated as text, so here they are just
                          passed through without unescaping.
                        */
                        while (true)
                        {
                            c = stream.ReadByte();
                            if (c == -1)
                                break; // NOOP.
                            if (IsDelimiter(c) || IsWhitespace(c))
                                break;

                            sBuffer.Append((char)c);
                        }
                        if (c > -1)
                        { stream.Skip(-1); } // Restores the first byte after the current token.
                    }
                    break;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '.':
                case '-':
                case '+': // Number.
                    {
                        if (c == '.')
                        { tokenType = TokenTypeEnum.Real; }
                        else // Digit or signum.
                        { tokenType = TokenTypeEnum.Integer; } // By default (it may be real).

                        // Building the number...                        
                        while (true)
                        {
                            sBuffer.Append((char)c);
                            c = stream.ReadByte();
                            if (c == -1)
                                break; // NOOP.
                            else if (c == '.')
                                tokenType = TokenTypeEnum.Real;
                            else if (c < '0' || c > '9')
                                break;
                        }
                        if (c > -1)
                        { stream.Skip(-1); } // Restores the first byte after the current token.
                    }
                    break;
                case Symbol.OpenSquareBracket: // Array (begin).
                    tokenType = TokenTypeEnum.ArrayBegin;
                    break;
                case Symbol.CloseSquareBracket: // Array (end).
                    tokenType = TokenTypeEnum.ArrayEnd;
                    break;
                case Symbol.OpenAngleBracket: // Dictionary (begin) | Hexadecimal string.
                    {
                        c = stream.ReadByte();
                        if (c == -1)
                            throw new PostScriptParseException("Isolated opening angle-bracket character.");
                        // Is it a dictionary (2nd angle bracket)?
                        if (c == Symbol.OpenAngleBracket)
                        {
                            tokenType = TokenTypeEnum.DictionaryBegin;
                            break;
                        }

                        // Hexadecimal string (single angle bracket).
                        tokenType = TokenTypeEnum.Hex;

                        c = IsWhitespace(c) ? ReadIgnoreWhitespace() : c;
                        while (c != Symbol.CloseAngleBracket)  // NOT string end.
                        {
                            var c2 = ReadIgnoreWhitespace();
                            if (c2 == Symbol.CloseAngleBracket)
                            {
                                mBuffer.WriteByte(ConvertUtils.ReadHexByte((char)c, '0'));
                                break;
                            }
                            if (c == -1 || c2 == -1)
                                throw new PostScriptParseException("Malformed hex string.");
                            mBuffer.WriteByte(ConvertUtils.ReadHexByte((char)c, (char)c2));

                            c = ReadIgnoreWhitespace();
                        }
                    }
                    break;
                case Symbol.CloseAngleBracket: // Dictionary (end).
                    {
                        c = stream.ReadByte();
                        if (c == -1)
                            throw new PostScriptParseException("Malformed dictionary.");
                        else if (c != Symbol.CloseAngleBracket)
                            throw new PostScriptParseException("Malformed dictionary.", this);

                        tokenType = TokenTypeEnum.DictionaryEnd;
                    }
                    break;
                case Symbol.OpenRoundBracket: // Literal string.
                    {
                        tokenType = TokenTypeEnum.Literal;

                        int level = 0;
                        while (true)
                        {
                            c = stream.ReadByte();
                            if (c == -1)
                                break;
                            else if (c == Symbol.OpenRoundBracket)
                                level++;
                            else if (c == Symbol.CloseRoundBracket)
                                level--;
                            else if (c == '\\')
                            {
                                bool lineBreak = false;
                                c = stream.ReadByte();
                                switch (c)
                                {
                                    case 'n':
                                        c = Symbol.LineFeed;
                                        break;
                                    case 'r':
                                        c = Symbol.CarriageReturn;
                                        break;
                                    case 't':
                                        c = '\t';
                                        break;
                                    case 'b':
                                        c = '\b';
                                        break;
                                    case 'f':
                                        c = '\f';
                                        break;
                                    case Symbol.OpenRoundBracket:
                                    case Symbol.CloseRoundBracket:
                                    case '\\':
                                        break;
                                    case Symbol.CarriageReturn:
                                        lineBreak = true;
                                        c = stream.ReadByte();
                                        if (c != Symbol.LineFeed)
                                            stream.Skip(-1);
                                        break;
                                    case Symbol.LineFeed:
                                        lineBreak = true;
                                        break;
                                    default:
                                        {
                                            // Is it outside the octal encoding?
                                            if (c < '0' || c > '7')
                                                break;

                                            // Octal.
                                            int octal = c - '0';
                                            c = stream.ReadByte();
                                            // Octal end?
                                            if (c < '0' || c > '7')
                                            { c = octal; stream.Skip(-1); break; }
                                            octal = (octal << 3) + c - '0';
                                            c = stream.ReadByte();
                                            // Octal end?
                                            if (c < '0' || c > '7')
                                            { c = octal; stream.Skip(-1); break; }
                                            octal = (octal << 3) + c - '0';
                                            c = octal & 0xff;
                                            break;
                                        }
                                }
                                if (lineBreak)
                                    continue;
                                if (c == -1)
                                    break;
                            }
                            else if (c == Symbol.CarriageReturn)
                            {
                                c = stream.ReadByte();
                                if (c == -1)
                                    break;
                                else if (c != Symbol.LineFeed)
                                { c = Symbol.LineFeed; stream.Skip(-1); }
                            }
                            if (level == -1)
                                break;

                            mBuffer.WriteByte((byte)c);
                        }
                        if (c == -1)
                            throw new PostScriptParseException("Malformed literal string.");
                    }
                    break;
                case Symbol.Percent: // Comment.
                    {
                        tokenType = TokenTypeEnum.Comment;

                        while (true)
                        {
                            c = stream.ReadByte();
                            if (c == -1
                              || IsEOL(c))
                                break;

                            sBuffer.Append((char)c);
                        }
                    }
                    break;
                default: // Keyword.
                    {
                        tokenType = TokenTypeEnum.Keyword;

                        do
                        {
                            sBuffer.Append((char)c);
                            c = stream.ReadByte();
                            if (c == -1)
                                break;
                        } while (!IsDelimiter(c) && !IsWhitespace(c));
                        if (c > -1)
                        { stream.Skip(-1); } // Restores the first byte after the current token.
                    }
                    break;
            }

            switch (tokenType)
            {
                case TokenTypeEnum.Keyword:
                    var span = sBuffer.AsSpan();
                    if (MemoryExtensions.Equals(span, Keyword.False, StringComparison.Ordinal))
                    {
                        tokenType = TokenTypeEnum.Boolean;
                        token = false;
                    }
                    else if (MemoryExtensions.Equals(span, Keyword.True, StringComparison.Ordinal))
                    {
                        tokenType = TokenTypeEnum.Boolean;
                        token = true;
                    }
                    else if (MemoryExtensions.Equals(span, Keyword.Null, StringComparison.Ordinal))
                    {
                        tokenType = TokenTypeEnum.Null;
                        token = null;
                    }
                    else
                    {
                        token = sBuffer;
                    }
                    break;
                case TokenTypeEnum.Literal:
                case TokenTypeEnum.Hex:
                    token = mBuffer;
                    break;
                case TokenTypeEnum.Name:
                case TokenTypeEnum.Comment:
                    token = sBuffer;
                    break;
                case TokenTypeEnum.Integer:
                    if (long.TryParse(sBuffer.AsSpan().ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lResult))
                        token = unchecked((int)lResult);
                    break;
                case TokenTypeEnum.Real:
                    if (double.TryParse(sBuffer.AsSpan().ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dResult))
                        token = dResult;
                    break;
                default:
                    break;
            }
            if ((int)tokenType == -1)
            { }
            return true;
        }

        public long Position => stream.Position;

        /**
          <summary>Moves the pointer to the given absolute byte position.</summary>
        */
        public void Seek(long offset) => stream.Seek(offset);

        /**
          <summary>Moves the pointer to the given relative byte position.</summary>
        */
        public void Skip(long offset) => stream.Skip(offset);

        /**
          <summary>Moves the pointer after the next end-of-line character sequence (that is just before
          the non-EOL character following the EOL sequence).</summary>
          <returns>Whether the stream can be further read.</returns>
        */
        public bool SkipEOL()
        {
            int c;
            bool found = false;
            while (true)
            {
                c = stream.ReadByte();
                if (c == -1)
                    return false;
                else if (IsEOL(c))
                {
                    if (found && c == 10)
                        return true;
                    found = true;
                }
                else if (found) // After EOL.
                    break;
            }
            stream.Skip(-1); // Moves back to the first non-EOL character position (ready to read the next token).
            return true;
        }

        /**
          <summary>Moves the pointer to first entires of specified key char sequence.</summary>
          <returns>Whether the stream can be further read.</returns>
        */
        public bool SkipKey(string key)
        {
            int c;
            int index = 0;
            while (true)
            {
                c = stream.ReadByte();
                if (c == -1)
                    return false;
                else if (c < char.MaxValue && (char)c == key[index])
                {
                    index++;
                    if (index == key.Length)
                    {
                        break;
                    }
                }
                else
                {
                    index = 0;
                }
            }
            stream.Skip(-(key.Length + 1)); // Moves back to the first non-EOL character position (ready to read the next token).
            return true;
        }

        public bool SkipKeyRevers(string key, long limitPosition = 0)
        {
            int c;
            int index = key.Length - 1;
            while (true)
            {
                stream.Skip(-1);
                c = stream.PeekByte();

                if (c < char.MaxValue && (char)c == key[index])
                {
                    index--;
                    if (index == 0)
                    {
                        break;
                    }
                }
                else
                {
                    index = key.Length - 1;
                }
                if (stream.Position == limitPosition)
                    return false;
            }
            return true;
        }

        /**
          <summary>Moves the pointer after the current whitespace sequence (that is just before the
          non-whitespace character following the whitespace sequence).</summary>
          <returns>Whether the stream can be further read.</returns>
        */
        public bool SkipWhitespace()
        {
            int c;
            do
            {
                c = stream.ReadByte();
                if (c == -1)
                    return false;
            } while (IsWhitespace(c)); // Keeps going till there's a whitespace character.
            stream.Skip(-1); // Moves back to the first non-whitespace character position (ready to read the next token).
            return true;
        }

        public int ReadIgnoreWhitespace()
        {
            int c;
            do
            {
                c = stream.ReadByte();
                if (c == -1)
                    return -1;
            }
            while (IsWhitespace(c));// Keep goin' till there's a white-space character...
            return c;
        }

        public IInputStream Stream => stream;

        /**
          <summary>Gets the currently-parsed token.</summary>
        */
        public object Token
        {
            get => token;
            protected set => token = value;
        }

        public ReadOnlySpan<char> CharsToken
        {
            get => sBuffer.AsSpan();
        }

        public ReadOnlySpan<byte> BytesToken
        {
            get => mBuffer.AsSpan();
        }

        /**
          <summary>Gets the currently-parsed token type.</summary>
        */
        public TokenTypeEnum TokenType
        {
            get => tokenType;
            protected set => tokenType = value;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
            }
        }
    }
}