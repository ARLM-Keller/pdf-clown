/*
  Copyright 2008-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Files;
using tokens = PdfClown.Tokens;
using PdfClown.Util;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace PdfClown.Objects
{
    /**
      <summary>PDF string object [PDF:1.6:3.2.3].</summary>
      <remarks>
        <para>A string object consists of a series of bytes.</para>
        <para>String objects can be serialized in two ways:</para>
        <list type="bullet">
          <item>as a sequence of literal characters (plain form)</item>
          <item>as a sequence of hexadecimal digits (hexadecimal form)</item>
        </list>
      </remarks>
    */
    public class PdfString : PdfSimpleObject<Memory<byte>>, IDataWrapper, IPdfString
    {
        /*
          NOTE: String objects are internally represented as unescaped sequences of bytes.
          Escaping is applied on serialization only.
        */
        /**
          <summary>String serialization mode.</summary>
        */
        public enum SerializationModeEnum
        {
            /**
              Plain form.
            */
            Literal,
            /**
              Hexadecimal form.
            */
            Hex
        };

        public static PdfString Get(string value)
        {
            return value != null ? new PdfString(value) : null;
        }

        public static readonly PdfString Default = new PdfString("");

        private const byte BackspaceCode = 8;
        private const byte CarriageReturnCode = 13;
        private const byte FormFeedCode = 12;
        private const byte HorizontalTabCode = 9;
        private const byte LineFeedCode = 10;

        private const byte HexLeftDelimiterCode = 60;
        private const byte HexRightDelimiterCode = 62;
        private const byte LiteralEscapeCode = 92;
        private const byte LiteralLeftDelimiterCode = 40;
        private const byte LiteralRightDelimiterCode = 41;

        private SerializationModeEnum serializationMode = SerializationModeEnum.Literal;
        protected string stringValue;

        public PdfString(Memory<byte> rawValue, SerializationModeEnum serializationMode = SerializationModeEnum.Literal)
        {
            SerializationMode = serializationMode;
            RawValue = rawValue;
        }

        public PdfString(string value, SerializationModeEnum serializationMode = SerializationModeEnum.Literal)
        {
            SerializationMode = serializationMode;
            Value = value;
        }

        protected PdfString()
        { }

        public override PdfObject Accept(IVisitor visitor, object data) => visitor.Visit(this, data);

        public override int CompareTo(PdfDirectObject obj)
        {
            if (!(obj is PdfString objString))
                throw new ArgumentException("Object MUST be a PdfString");

            return string.CompareOrdinal(StringValue, objString.StringValue);
        }

        public override bool Equals(object @object)
        {
            if (@object is PdfString objString)
                return RawValue.Span.SequenceEqual(objString.RawValue.Span);
            return base.Equals(@object);
        }

        public override int GetHashCode() => RawValue.GetHashCode();

        /**
          <summary>Gets/Sets the serialization mode.</summary>
        */
        public virtual SerializationModeEnum SerializationMode
        {
            get => serializationMode;
            set => serializationMode = value;
        }

        public string StringValue => (string)Value;

        public byte[] ToArray() => RawValue.ToArray();

        public Memory<byte> AsMemory() => RawValue;

        public Span<byte> AsSpan() => RawValue.Span;

        public byte[] GetArrayBuffer()
        {
            return Unsafe.As<Memory<byte>, ArraySegment<byte>>(ref value).Array;
        }

        public void SetBuffer(byte[] data)
        {
            RawValue = data;
            stringValue = null;
        }

        public void SetBuffer(Memory<byte> data)
        {
            RawValue = data;
            stringValue = null;
        }

        public override string ToString()
        {
            switch (SerializationMode)
            {
                case SerializationModeEnum.Hex:
                    return "<" + base.ToString() + ">";
                case SerializationModeEnum.Literal:
                    return "(" + base.ToString() + ")";
                default:
                    throw new NotImplementedException();
            }
        }

        public override object Value
        {
            get
            {
                if (stringValue != null)
                    return stringValue;
                switch (SerializationMode)
                {
                    case SerializationModeEnum.Literal:
                        return stringValue = tokens::Encoding.Pdf.Decode(RawValue.Span);
                    case SerializationModeEnum.Hex:
                        return stringValue = tokens::Encoding.Pdf.Decode(RawValue.Span);
                    default:
                        throw new NotImplementedException(SerializationMode + " serialization mode is not implemented.");
                }
            }
            protected set
            {

                switch (SerializationMode)
                {
                    case SerializationModeEnum.Literal:
                        stringValue = (string)value;
                        RawValue = tokens::Encoding.Pdf.Encode(stringValue);
                        break;
                    case SerializationModeEnum.Hex:
                        stringValue = null;
                        RawValue = ConvertUtils.HexStringToByteArray((string)value);
                        break;
                    default:
                        throw new NotImplementedException(SerializationMode + " serialization mode is not implemented.");
                }
            }
        }

        public override void WriteTo(IOutputStream stream, Files.File context)
        {
            var buffer = stream;
            {
                var rawValue = RawValue.Span;
                switch (SerializationMode)
                {
                    case SerializationModeEnum.Literal:
                        buffer.WriteByte(LiteralLeftDelimiterCode);
                        /*
                          NOTE: Literal lexical conventions prescribe that the following reserved characters
                          are to be escaped when placed inside string character sequences:
                            - \n Line feed (LF)
                            - \r Carriage return (CR)
                            - \t Horizontal tab (HT)
                            - \b Backspace (BS)
                            - \f Form feed (FF)
                            - \( Left parenthesis
                            - \) Right parenthesis
                            - \\ Backslash
                        */
                        for (int index = 0; index < rawValue.Length; index++)
                        {
                            byte valueByte = rawValue[index];
                            switch (valueByte)
                            {
                                case LineFeedCode:
                                    buffer.WriteByte(LiteralEscapeCode); valueByte = 110; break;
                                case CarriageReturnCode:
                                    buffer.WriteByte(LiteralEscapeCode); valueByte = 114; break;
                                case HorizontalTabCode:
                                    buffer.WriteByte(LiteralEscapeCode); valueByte = 116; break;
                                case BackspaceCode:
                                    buffer.WriteByte(LiteralEscapeCode); valueByte = 98; break;
                                case FormFeedCode:
                                    buffer.WriteByte(LiteralEscapeCode); valueByte = 102; break;
                                case LiteralLeftDelimiterCode:
                                case LiteralRightDelimiterCode:
                                case LiteralEscapeCode:
                                    buffer.WriteByte(LiteralEscapeCode); break;
                            }
                            buffer.WriteByte(valueByte);
                        }
                        buffer.WriteByte(LiteralRightDelimiterCode);
                        break;
                    case SerializationModeEnum.Hex:
                        buffer.WriteByte(HexLeftDelimiterCode);
                        buffer.Write(ConvertUtils.ByteArrayToHex(rawValue));
                        buffer.WriteByte(HexRightDelimiterCode);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}