/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Bytes.Filters;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Objects;
using PdfClown.Tokens;
using PdfClown.Util;
using PdfClown.Util.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using static Org.BouncyCastle.Bcpg.Attr.ImageAttrib;
using text = System.Text;

namespace PdfClown.Bytes
{
    public static class StreamExtensions
    {
        private const int BufferSize = 4 * 1024;
        private const int MaxStackAllockSize = 256;

        public static StreamSegment ReadSegment(this IInputStream target, int length)
        {
            target.Skip(length);
            return new StreamSegment(target, length);
        }

        public static string ReadString(this IInputStream target, int length) => target.ReadString(length, Charset.ISO88591);

        public static string ReadString(this IInputStream target, int length, System.Text.Encoding encoding)
        {
            var span = target.ReadSpan(length);
            return encoding.GetString(span);
        }

        public static float ReadFixed32(this IInputStream target)
        {
            return target.ReadInt16() // Signed Fixed-point mantissa (16 bits).
               + target.ReadUInt16() / 16384f; // Fixed-point fraction (16 bits).
        }

        public static float ReadUnsignedFixed32(this IInputStream target)
        {
            return target.ReadUInt16() // Fixed-point mantissa (16 bits).
               + target.ReadUInt16() / 16384f; // Fixed-point fraction (16 bits).
        }

        public static float ReadFixed16(this IInputStream target)
        {
            return target.ReadSByte() // Fixed-point mantissa (8 bits).
               + target.ReadByte() / 64f; // Fixed-point fraction (8 bits).
        }

        public static float ReadUnsignedFixed16(this IInputStream target)
        {
            return (byte)target.ReadByte() // Fixed-point mantissa (8 bits).
               + target.ReadByte() / 64f; // Fixed-point fraction (8 bits).
        }

        public static float Read32Fixed(this IInputStream target)
        {
            float retval = 0;
            retval = target.ReadInt16();
            retval += (target.ReadUInt16() / 65536.0F);
            return retval;
        }

        public static string ReadTag(this IInputStream target)
        {
            Span<byte> buffer = stackalloc byte[4];
            target.Read(buffer);
            return Charset.ASCII.GetString(buffer);
        }

        public static DateTime ReadInternationalDate(this IInputStream target)
        {
            try
            {
                var secondsSince1904 = target.ReadInt64();
                var cal = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return cal + TimeSpan.FromSeconds(secondsSince1904);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"error: ReadInternationalDate {ex} ");
                return DateTime.UtcNow;
            }
        }

        public static int Read(this IInputStream target, sbyte[] data) => target.Read(data, 0, data.Length);

        public static int Read(this IInputStream target, sbyte[] data, int offset, int length)
        {
            return target.Read((byte[])(Array)data, offset, length);
        }

        public static ushort[] ReadUShortArray(this IInputStream input, int length)
        {
            ushort[] array = new ushort[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = input.ReadUInt16();
            }
            return array;
        }

        public static short[] ReadSShortArray(this IInputStream input, int length)
        {
            short[] array = new short[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = input.ReadInt16();
            }
            return array;
        }

        /**
		 * Read the offset from the buffer.
		 * @param offSize the given offsize
		 * @return the offset
		 * @throws IOException if an error occurs during reading
		 */
        public static int ReadOffset(this IInputStream input, int offSize)
        {
            Span<byte> bytes = stackalloc byte[offSize];
            input.Read(bytes);
            int value = 0;
            for (int i = 0; i < offSize; i++)
            {
                value = value << 8 | bytes[i];
            }
            return value;
        }

        public static void Write(this IOutputStream target, IInputStream data)
        {
            byte[] baseData = ArrayPool<byte>.Shared.Rent(BufferSize);
            data.Seek(0);
            int count;
            while ((count = data.Read(baseData, 0, baseData.Length)) > 0)
            {
                target.Write(baseData, 0, count);
            }
            ArrayPool<byte>.Shared.Return(baseData);
        }

        public static void Write(this IOutputStream target, Stream data)
        {
            byte[] baseData = ArrayPool<byte>.Shared.Rent(BufferSize);
            data.Position = 0;
            int count;
            while ((count = data.Read(baseData, 0, baseData.Length)) > 0)
            {
                target.Write(baseData, 0, count);
            }
            ArrayPool<byte>.Shared.Return(baseData);
        }

        public static void Write(this IOutputStream target, short value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(short)];
            ConvertUtils.WriteInt16(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void Write(this IOutputStream target, ushort value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            ConvertUtils.WriteUInt16(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void Write(this IOutputStream target, int value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ConvertUtils.WriteInt32(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void Write(this IOutputStream target, uint value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            ConvertUtils.WriteUInt32(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void Write(this IOutputStream target, long value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ConvertUtils.WriteInt64(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void Write(this IOutputStream target, ulong value, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];
            ConvertUtils.WriteUInt64(buffer, value, byteOrder);
            target.Write(buffer);
        }

        public static void WriteFixed(this IOutputStream target, double f, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            double ip = Math.Floor(f);
            double fp = (f - ip) * 65536.0;
            target.Write((short)ip, byteOrder);
            target.Write((short)fp, byteOrder);
        }

        public static void WriteLongDateTime(this IOutputStream target, DateTime calendar, ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian)
        {
            // inverse operation of IInputStream.readInternationalDate()
            DateTime cal = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long secondsSince1904 = (long)(calendar - cal).TotalSeconds;
            target.Write(secondsSince1904, byteOrder);
        }

        public static void WriteAsString(this IOutputStream target, int value) => target.WriteAsString(value, Charset.ISO88591);

        public static void WriteAsString(this IOutputStream target, int value, System.Text.Encoding encoding)
        {
            Span<char> chars = stackalloc char[12];
            value.TryFormat(chars, out var lenth, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture);
            target.Write(chars[..lenth], encoding);
        }

        public static void WriteAsString(this IOutputStream target, double value, string format, IFormatProvider provider) => target.WriteAsString(value, format, provider, Charset.ISO88591);

        public static void WriteAsString(this IOutputStream target, double value, string format, IFormatProvider provider, System.Text.Encoding encoding)
        {
            Span<char> chars = stackalloc char[22];
            value.TryFormat(chars, out var lenth, format, provider);
            target.Write(chars[..lenth], encoding);
        }

        public static void Write(this IOutputStream target, ReadOnlySpan<char> data) => target.Write(data, Charset.ISO88591);

        public static void Write(this IOutputStream target, ReadOnlySpan<char> data, text::Encoding encoding)
        {
            var length = encoding.GetByteCount(data);
            Span<byte> buffer = length <= MaxStackAllockSize
                ? stackalloc byte[length]
                : new byte[length];
            encoding.GetBytes(data, buffer);
            target.Write(buffer);
        }

        public static Memory<byte> AsMemory(this Stream stream) => stream is MemoryStream memoryStream
            ? memoryStream.AsMemory()
            : stream is IDataWrapper dataWrapper
                ? dataWrapper.AsMemory()
                : throw new Exception("new BuffedStream(stream).GetMemoryBuffer()");

        public static Memory<byte> AsMemory(this IDataWrapper stream) => stream.AsMemory();

        public static Memory<byte> AsMemory(this MemoryStream stream) => new Memory<byte>(stream.GetBuffer(), 0, (int)stream.Length);

        public static Span<byte> AsSpan(this MemoryStream stream) => new Span<byte>(stream.GetBuffer(), 0, (int)stream.Length);

        /**
        * Determines if there are any bytes left to read or not. 
        * @return true if there are any bytes left to read
        */
        public static bool HasRemaining(this IInputStream input)
        {
            return input.Length > input.Position;
        }

        /**
		 * Peeks one single signed byte from the buffer.
		 * @return the signed byte as int
		 * @throws IOException if an error occurs during reading
		 */
        public static sbyte PeekSignedByte(this IInputStream input, int offset)
        {
            try
            {
                return unchecked((sbyte)input.PeekUByte(offset));
            }
            catch (Exception re)
            {
                Debug.WriteLine("debug: An error occurred peeking at offset " + offset + " - returning -1", re);
                throw new EndOfStreamException();
            }
        }

        public static PdfDataObject Resolve(PdfObject @object)
        {
            return @object == null ? null : @object.Resolve();
        }

        public static void Decode(this IByteStream buffer, PdfDataObject filter, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            if (filter is PdfName name) // Single filter.
            {
                buffer.Decode(Filter.Get(name), (PdfDictionary)parameters, header);
            }
            else // Multiple filters.
            {
                IEnumerator<PdfDirectObject> filterIterator = ((PdfArray)filter).GetEnumerator();
                IEnumerator<PdfDirectObject> parametersIterator = (parameters != null ? ((PdfArray)parameters).GetEnumerator() : null);
                while (filterIterator.MoveNext())
                {
                    PdfDictionary filterParameters;
                    if (parametersIterator == null)
                    { filterParameters = null; }
                    else
                    {
                        parametersIterator.MoveNext();
                        filterParameters = (PdfDictionary)Resolve(parametersIterator.Current);
                    }
                    buffer.Decode(Filter.Get((PdfName)Resolve(filterIterator.Current)), filterParameters, header);
                }
            }
        }

        public static IByteStream Extract(this IByteStream buffer, PdfDataObject filter, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            if (filter == null)
            {
                return buffer;
            }
            if (filter is PdfName) // Single filter.
            {
                buffer = buffer.Extract(Filter.Get((PdfName)filter), parameters, header);
            }
            else // Multiple filters.
            {
                var filterIterator = ((PdfArray)filter).GetEnumerator();
                var parametersIterator = (parameters != null ? ((PdfArray)parameters).GetEnumerator() : null);
                while (filterIterator.MoveNext())
                {
                    PdfDictionary filterParameters;
                    if (parametersIterator == null)
                    { filterParameters = null; }
                    else
                    {
                        parametersIterator.MoveNext();
                        filterParameters = (PdfDictionary)Resolve(parametersIterator.Current);
                    }
                    buffer = buffer.Extract(Filter.Get((PdfName)Resolve(filterIterator.Current)), filterParameters, header);
                }
            }
            return buffer;
        }
    }
}