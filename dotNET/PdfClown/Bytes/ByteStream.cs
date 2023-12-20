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
using PdfClown.Documents.Contents;
using PdfClown.Objects;
using PdfClown.Tokens;
using PdfClown.Util;
using PdfClown.Util.IO;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using text = System.Text;

namespace PdfClown.Bytes
{
    //TODO:IMPL Substitute System.Array static class invocations with System.Buffer static class invocations (better performance)!!!
    ///<summary>Byte buffer.</summary>
    public class ByteStream : System.IO.Stream, IByteStream
    {
        ///<summary>Default buffer capacity.</summary>
        private const int DefaultCapacity = 1 << 8;

        public event EventHandler OnChange;
        ///<summary>Inner buffer where data are stored.</summary>
        private ArraySegment<byte> data;
        ///<summary>Number of bytes actually used in the buffer.</summary>
        private int length;
        ///<summary>Pointer position within the buffer.</summary>
        private int position = 0;
        private ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian;
        private bool dirty;
        private long mark;
        private int bitShift = -1;
        private byte currentByte;

        public ByteStream() : this(0)
        { }

        public ByteStream(int capacity)
        {
            if (capacity < 1)
            { capacity = DefaultCapacity; }

            data = new byte[capacity];
            length = 0;
        }

        public ByteStream(Memory<byte> data)
        {
            SetBuffer(data);
        }

        public ByteStream(byte[] data, int start, int end) : this(data.AsMemory(start, end - start))
        { }

        public ByteStream(Stream data) : this((int)data.Length)
        {
            this.Write(data);
        }

        public ByteStream(IInputStream data) : this((int)data.Length)
        {
            this.Write(data);
        }

        public ByteStream(IDataWrapper data) : this(data.AsMemory())
        { }

        public ByteStream(string data) : this(data.Length)
        {
            this.Write(data);
        }

        public ByteStream(IInputStream data, int begin, int len)
        {
            data.Seek(begin);
            SetBuffer(data.ReadMemory(len));
        }

        public long Available { get => length - position; }

        public bool IsAvailable => length > position;

        public override long Length
        {
            get => length;
        }

        public int Capacity => data.Count;

        public bool Dirty
        {
            get => dirty;
            set => dirty = value;
        }

        public ByteOrderEnum ByteOrder
        {
            get => byteOrder;
            set => byteOrder = value;
        }

        /* int GetHashCode() uses inherited implementation. */
        public override long Position
        {
            get => position;
            set => position = (int)value;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public IByteStream Append(byte data)
        {
            EnsureCapacity(1);
            this.data[length++] = data;
            NotifyChange();
            return this;
        }

        public IByteStream Append(byte[] data) => Append(data.AsSpan(0, data.Length));

        public IByteStream Append(byte[] data, int offset, int length) => Append(data.AsSpan(offset, length));

        public IByteStream Append(ReadOnlySpan<byte> data)
        {
            EnsureCapacity(data.Length);
            data.CopyTo(AsSpan(length, data.Length));
            length += data.Length;
            NotifyChange();
            return this;
        }

        public IByteStream Clone()
        {
            var clone = new ByteStream(length);
            clone.Append(AsSpan(0, length));
            return clone;
        }

        public void Decode(Filter filter, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            var data = filter.Decode(this, parameters, header);
            this.data = Unsafe.As<Memory<byte>, ArraySegment<byte>>(ref data);
            position = 0;
            length = data.Length;
        }

        public IByteStream Extract(Filter filter, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            var data = filter.Decode(this, parameters, header);
            return new ByteStream(data);
        }

        public void Delete(int index, int length)
        {
            var leftLength = this.length - (index + length);
            // Shift left the trailing data block to override the deleted data!
            //Array.Copy(data, index + length, this.data, index, leftLength);

            AsSpan(index + length, leftLength).CopyTo(AsSpan(index, leftLength));
            this.length -= length;
            NotifyChange();
        }

        public Memory<byte> Encode(Filter filter, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            return filter.Encode(this, parameters, header);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetByte(int index) => data[index];

        public byte[] GetByteArray(int index, int length)
        {
            if ((index + length) > this.length)
            {
                length = this.length - index;
            }
            byte[] data = new byte[length];
            AsSpan(index, length).CopyTo(data.AsSpan());
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan(int index, int length) => data.AsSpan(index, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan(int index) => AsSpan(index, length - index);

        public string GetString(int index, int length) => Charset.ISO88591.GetString(AsSpan(index, length));

        public string GetString() => Charset.ISO88591.GetString(AsSpan());

        public void Insert(int index, byte[] data) => Insert(index, data, 0, data.Length);

        public void Insert(int index, byte[] data, int offset, int length) => Insert(index, data.AsSpan(offset, length));

        public void Insert(int index, ReadOnlySpan<byte> data)
        {
            EnsureCapacity(length);
            var leftLength = this.length - index;
            // Shift right the existing data block to make room for new data!
            //Array.Copy(this.data, index, this.data, index + length, leftLength);
            AsSpan(index, leftLength).CopyTo(AsSpan(index + length, leftLength));

            // Insert additional data!
            //Array.Copy(data, offset, this.data, index, length);
            data.CopyTo(AsSpan(index, length));
            this.length += length;
            NotifyChange();
        }

        public void Insert(int index, string data) => Insert(index, Encoding.Pdf.Encode(data));

        public void Insert(int index, IInputStream data) => Insert(index, data.AsMemory().Span);

        public void Replace(int index, byte[] data) => Replace(index, data, 0, data.Length);

        public void Replace(int index, byte[] data, int offset, int length) => Replace(index, data.AsSpan(offset, length));

        public void Replace(int index, ReadOnlySpan<byte> data)
        {
            //Array.Copy(data, offset, this.data, index, data.Length);
            data.CopyTo(AsSpan(index, data.Length));
            NotifyChange();
        }

        public void Replace(int index, string data) => Replace(index, Encoding.Pdf.Encode(data));

        public void Replace(int index, IInputStream data) => Replace(index, data.AsMemory().Span);

        public override void SetLength(long value) => SetLength((int)value);

        public void SetLength(int value)
        {
            if (length != value)
            {
                if (Capacity < value)
                {
                    EnsureCapacity(value - Capacity);
                }
                length = value;
                if (position > length)
                    position = length;
                NotifyChange();
            }
        }

        public void WriteTo(IOutputStream stream) => stream.Write(AsSpan(0, length));

        public int Read(byte[] data) => Read(data, 0, data.Length);

        public override int Read(byte[] data, int offset, int length) => Read(data.AsSpan(offset, length));

        public override int Read(Span<byte> data)
        {
            var length = data.Length;
            if (position + length > Length)
            {
                length = (int)(Length - position);
            }
            AsSpan(position, length).CopyTo(data);
            position += length;
            return length;
        }

        public byte[] ReadNullTermitaded()
        {
            var start = position;
            var length = 0;
            while (ReadByte() > 0) { length++; }
            return GetByteArray(start, length);
        }

        public Span<byte> ReadNullTermitadedSpan()
        {
            var start = position;
            var length = 0;
            while (ReadByte() > 0) { length++; }
            return AsSpan(start, length);
        }

        public Span<byte> ReadSpan(int length)
        {
            if (position + length > this.length)
            {
                length = (int)(this.length - position);
            }
            var start = position;
            position += length;
            return AsSpan(start, length);
        }

        public Memory<byte> ReadMemory(int length)
        {
            if (position + length > this.length)
            {
                length = (int)(this.length - position);
            }
            var start = position;
            position += length;
            return data.Slice(start, length);
        }

        public override int ReadByte()
        {
            if (position >= length)
                return -1;

            return GetByte(position++);
        }

        public byte ReadUByte()
        {
            if (position >= length)
                throw new EndOfStreamException();

            return GetByte(position++);
        }

        public sbyte ReadSByte()
        {
            if (position >= length)
                throw new EndOfStreamException();

            return unchecked((sbyte)GetByte(position++));
        }

        public int PeekByte()
        {
            if (position >= length)
                return -1;
            return GetByte(position);
        }

        public byte PeekUByte(int offset)
        {
            if (position + offset >= length)
                throw new EndOfStreamException();
            return GetByte(position + offset);
        }

        public short ReadInt16()
        {
            short value = ConvertUtils.ReadInt16(AsSpan(position, sizeof(short)), byteOrder);
            position += sizeof(short);
            return value;
        }

        public ushort ReadUInt16()
        {
            ushort value = ConvertUtils.ReadUInt16(AsSpan(position, sizeof(ushort)), byteOrder);
            position += sizeof(ushort);
            return value;
        }

        public int ReadInt32()
        {
            int value = ConvertUtils.ReadInt32(AsSpan(position, sizeof(int)), byteOrder);
            position += sizeof(int);
            return value;
        }

        public uint ReadUInt32()
        {
            var value = ConvertUtils.ReadUInt32(AsSpan(position, sizeof(uint)), byteOrder);
            position += sizeof(uint);
            return value;
        }

        public int ReadInt(int length)
        {
            int value = ConvertUtils.ReadIntOffset(AsSpan(position, length), byteOrder);
            position += length;
            return value;
        }

        public long ReadInt64()
        {
            var value = ConvertUtils.ReadInt64(AsSpan(position, sizeof(long)), byteOrder);
            position += sizeof(long);
            return value;
        }

        public ulong ReadUInt64()
        {
            var value = ConvertUtils.ReadUInt64(AsSpan(position, sizeof(ulong)), byteOrder);
            position += sizeof(ulong);
            return value;
        }

        public string ReadLine()
        {
            if (position >= length)
                throw new EndOfStreamException();

            var buffer = new text::StringBuilder();
            while (position < length)
            {
                int c = GetByte(position++);
                if (c == '\r'
                  || c == '\n')
                    break;

                buffer.Append((char)c);
            }
            return buffer.ToString();
        }

        public void ByteAlign()
        {
            this.bitShift = -1;
        }

        public int ReadBit()
        {
            if (bitShift < 0)
            {
                currentByte = ReadUByte();
                bitShift = 7;
            }
            var bit = (currentByte >> bitShift) & 1;
            bitShift--;
            return bit;
        }

        public uint ReadBits(int count)
        {
            var result = (uint)0;
            for (int i = count - 1; i >= 0; i--)
            {
                result |= (uint)(ReadBit() << i);
            }
            return result;
        }

        /**
         * Read a fixed length string.
         * 
         * @param length The length of the string to read in bytes.
         * @param charset The expected character set of the string.
         * @return A string of the desired length.
         * @ If there is an error reading the data.
         */
        public string ReadString(int length) => StreamExtensions.ReadString(this, length);

        public long Seek(long position)
        {
            if (position < 0)
            { position = 0; }
            else if (position > length)
            { position = length; }

            return this.position = (int)position;
        }

        public long Skip(long offset) => Seek(position + offset);

        public int Mark() => (int)(mark = position);

        public int Mark(long position) => (int)(mark = this.position + position);

        public void ResetMark() => Seek(mark);

        public byte[] ToArray() => data.Slice(0, length).ToArray();

        public Memory<byte> AsMemory() => data.Slice(0, length);

        public Span<byte> AsSpan() => data.AsSpan(0, length);

        public byte[] GetArrayBuffer() => data.Array;

        public void SetBuffer(Memory<byte> data)
        {
            this.data = Unsafe.As<Memory<byte>, ArraySegment<byte>>(ref data);
            length = data.Length;
            position = 0;
        }

        public void Clear() => SetLength(0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte data) => Append(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int data, int length)
        {
            Span<byte> result = stackalloc byte[length];
            ConvertUtils.WriteIntOffset(result, data, byteOrder);
            Write(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] data) => Append(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(ReadOnlySpan<byte> data) => Append(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] data, int offset, int length) => Append(data, offset, length);

        public int WriteBits(long data, int count)
        {
            throw new NotImplementedException();
        }

        /**
          <summary>Check whether the buffer has sufficient room for
          adding data.</summary>
        */
        private void EnsureCapacity(int additionalLength)
        {
            int minCapacity = length + additionalLength;
            // Is additional data within the buffer capacity?
            if (minCapacity <= data.Count)
                return;

            // Additional data exceed buffer capacity.
            // Reallocate the buffer!
            var newBuffer = new byte[Math.Max(data.Count << 1, minCapacity)];
            AsSpan().CopyTo(newBuffer);
            data = newBuffer;
        }

        private void NotifyChange()
        {
            if (dirty || OnChange == null)
                return;

            dirty = true;
            OnChange(this, null);
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return origin switch
            {
                SeekOrigin.Current => Skip(offset),
                SeekOrigin.End => Seek(Length - offset),
                _ => Seek(offset),
            };
        }

    }


}