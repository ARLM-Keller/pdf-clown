/*
  Copyright 2009-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfClown.Util
{
    /**
      <summary>Data convertion utility.</summary>
      <remarks>This class is a specialized adaptation from the original <a href="http://commons.apache.org/codec/">
      Apache Commons Codec</a> project, licensed under the <a href="http://www.apache.org/licenses/LICENSE-2.0">
      Apache License, Version 2.0</a>.</remarks>
    */
    public static class ConvertUtils
    {
        private static readonly char[] HexDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
        public static readonly string HexAlphabet = "0123456789ABCDEF";

        public static readonly int[] HexValue = new int[] {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };

        public static string ByteArrayToHex(ReadOnlySpan<byte> data)
        {
            int dataLength = data.Length;
            char[] result = new char[dataLength * 2];
            for (int dataIndex = 0, resultIndex = 0; dataIndex < dataLength; dataIndex++)
            {
                result[resultIndex++] = HexDigits[(0xF0 & data[dataIndex]) >> 4];
                result[resultIndex++] = HexDigits[0x0F & data[dataIndex]];
            }
            return new string(result);
        }

        public static byte[] HexToByteArray(ReadOnlySpan<char> data)
        {
            byte[] result;
            {
                int dataLength = data.Length;
                if ((dataLength % 2) != 0)
                    throw new Exception("Odd number of characters.");

                result = new byte[dataLength / 2];
                for (int resultIndex = 0, dataIndex = 0; dataIndex < dataLength; resultIndex++, dataIndex += 2)
                {
                    result[resultIndex] = byte.Parse(data.Slice(dataIndex, 2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo);
                }
            }
            return result;
        }

        //https://stackoverflow.com/a/5919521/4682355
        public static string ByteArrayToHexString(ReadOnlySpan<byte> bytes)
        {
            var Result = new StringBuilder(bytes.Length * 2);

            foreach (byte B in bytes)
            {
                Result.Append(HexAlphabet[(int)(B >> 4)]);
                Result.Append(HexAlphabet[(int)(B & 0xF)]);
            }

            return Result.ToString();
        }

        public static byte[] HexStringToByteArray(string Hex)
        {
            byte[] Bytes = new byte[Hex.Length / 2];


            for (int x = 0, i = 0; i < Hex.Length; i += 2, x += 1)
            {
                Bytes[x] = ReadHexByte(Hex[i + 0], Hex[i + 1]);
            }

            return Bytes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadHexByte(char c1, char c2)
        {
            return (byte)(HexValue[Char.ToUpper(c1) - '0'] << 4 |
                          HexValue[Char.ToUpper(c2) - '0']);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ReadHexByte(Span<byte> span)
        {
            return (byte)(HexValue[Char.ToUpper((char)span[0]) - '0'] << 4 |
                          HexValue[Char.ToUpper((char)span[1]) - '0']);
        }

        //public static int ReadInt(this byte[] data, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        //{
        //    return data.Length switch
        //    {
        //        1 => data[0],
        //        2 => ReadUInt16(data.AsSpan(), byteOrder),
        //        4 => ReadUInt32(data.AsSpan(), byteOrder),
        //        8 => (int)ReadUInt64(data.AsSpan(), byteOrder),
        //        _ => ReadIntByLength(data.AsSpan(), byteOrder),
        //    };
        //}

        //public static int ReadInt(Span<byte> data, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        //{
        //    return data.Length switch
        //    {
        //        1 => data[0],
        //        2 => ReadUInt16(data, byteOrder),
        //        4 => ReadUInt32(data, byteOrder),
        //        8 => (int)ReadUInt64(data, byteOrder),
        //        _ => ReadIntByLength(data, byteOrder),
        //    };
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(this byte[] data, int index = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(index, sizeof(int));
            return ReadInt32(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(this ReadOnlySpan<byte> buffer, int offset, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
           => ReadInt32(buffer.Slice(offset, sizeof(int)), byteOrder);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ReadInt32(ReadOnlySpan<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadInt32BigEndian(buffer)
                : BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt32(Span<byte> buffer, int value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(this byte[] data, int index = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(index, sizeof(uint));
            return ReadUInt32(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(this ReadOnlySpan<byte> buffer, int offset, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
           => ReadUInt32(buffer.Slice(offset, sizeof(uint)), byteOrder);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(ReadOnlySpan<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadUInt32BigEndian(buffer)
                : BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt32(Span<byte> buffer, uint value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(this byte[] data, int index = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(index, sizeof(short));
            return ReadInt16(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(this ReadOnlySpan<byte> buffer, int offset, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
            => ReadInt16(buffer.Slice(offset, sizeof(short)), byteOrder);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short ReadInt16(ReadOnlySpan<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadInt16BigEndian(buffer)
                : BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt16(Span<byte> buffer, short value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(this byte[] data, int index = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(index, sizeof(ushort));
            return ReadUInt16(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(this ReadOnlySpan<byte> buffer, int offset, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
            => ReadUInt16(buffer.Slice(offset, sizeof(ushort)), byteOrder);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(ReadOnlySpan<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
                : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt16(Span<byte> buffer, ushort value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(this byte[] data, int position = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(position, sizeof(long));
            return ReadInt64(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ReadInt64(ReadOnlySpan<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadInt64BigEndian(buffer)
                : BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteInt64(Span<byte> buffer, long value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUint64(this byte[] data, int position = 0, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var buffer = data.AsSpan(position, sizeof(ulong));
            return ReadUInt64(buffer, byteOrder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ReadUInt64(Span<byte> buffer, ByteOrderEnum byteOrder)
        {
            return byteOrder == ByteOrderEnum.BigEndian
                ? BinaryPrimitives.ReadUInt64BigEndian(buffer)
                : BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUInt64(Span<byte> buffer, ulong value, ByteOrderEnum byteOrder)
        {
            if (byteOrder == ByteOrderEnum.BigEndian)
                BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            else
                BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        }



        public static byte[] IntToByteArray(int data, bool compact = false)
        {
            if (compact)
            {
                if (data < 1 << 8)
                {
                    return new byte[] { (byte)data };
                }
                else if (data < 1 << 16)
                {
                    return new byte[] { (byte)(data >> 8), (byte)data };
                }
                else if (data < 1 << 24)
                {
                    return new byte[] { (byte)(data >> 16), (byte)(data >> 8), (byte)data };
                }
            }
            return new byte[] { (byte)(data >> 24), (byte)(data >> 16), (byte)(data >> 8), (byte)data };
        }

        public static int ReadIntOffset(byte[] data, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian) => ReadIntOffset(data, 0, data.Length, byteOrder);

        public static int ReadIntOffset(byte[] data, int index, int length, ByteOrderEnum byteOrder)
        {
            int value = 0;
            length = (int)System.Math.Min(length, data.Length - index);
            for (int i = index, endIndex = index + length; i < endIndex; i++)
            { value |= (data[i] & 0xff) << 8 * (byteOrder == ByteOrderEnum.LittleEndian ? i - index : endIndex - i - 1); }
            return value;
        }

        public static int ReadIntOffset(this ReadOnlySpan<byte> data, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            int value = 0;
            var length = data.Length;
            for (int i = 0, endIndex = length; i < endIndex; i++)
            { value |= (data[i] & 0xff) << 8 * (byteOrder == ByteOrderEnum.LittleEndian ? i : endIndex - i - 1); }
            return value;
        }

        //public static void WriteInt(Span<byte> result, int data, ByteOrderEnum byteOrder)
        //{
        //    switch (result.Length)
        //    {
        //        case 1: result[0] = (byte)data; break;
        //        case 2: WriteUInt16(result, (ushort)data, byteOrder); break;
        //        case 4: WriteInt32(result, data, byteOrder); break;
        //        case 8: WriteInt64(result, (long)data, byteOrder); break;
        //        default: WriteIntByLength(result, data, byteOrder); break;
        //    }
        //}

        public static byte[] WriteIntOffset(int data, int length, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            byte[] result = new byte[length];
            for (int index = 0; index < length; index++)
            { result[index] = (byte)(data >> 8 * (byteOrder == ByteOrderEnum.LittleEndian ? index : length - index - 1)); }
            return result;
        }

        public static void WriteIntOffset(Span<byte> result, int data, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            var length = result.Length;
            for (int index = 0; index < length; index++)
            { result[index] = (byte)(data >> 8 * (byteOrder == ByteOrderEnum.LittleEndian ? index : length - index - 1)); }
        }

        public static int ParseAsIntInvariant(string value) => (int)ParseFloatInvariant(value);

        public static int ParseAsIntInvariant(ReadOnlySpan<char> value) => (int)ParseFloatInvariant(value);

        public static double ParseDoubleInvariant(string value) => Double.Parse(value, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        public static double ParseDoubleInvariant(ReadOnlySpan<char> value) => Double.Parse(value, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        public static float ParseFloatInvariant(string value) => Single.Parse(value, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        public static float ParseFloatInvariant(ReadOnlySpan<char> value) => Single.Parse(value, NumberStyles.Float, NumberFormatInfo.InvariantInfo);

        public static int ParseIntInvariant(string value) => Int32.Parse(value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);

        public static int ParseIntInvariant(ReadOnlySpan<char> value) => Int32.Parse(value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);

        public static float[] ToFloatArray(double[] array)
        {
            float[] result = new float[array.Length];
            for (int index = 0, length = array.Length; index < length; index++)
            { result[index] = (float)array[index]; }
            return result;
        }
    }
}