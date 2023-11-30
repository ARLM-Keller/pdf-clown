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

using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Multimedia;
using PdfClown.Util;
using PdfClown.Util.IO;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using text = System.Text;

namespace PdfClown.Bytes
{
    /**
      <summary>Generic stream.</summary>
    */
    public class StreamContainer : Stream, IInputStream, IOutputStream
    {
        private Stream stream;

        private ByteOrderEnum byteOrder = ByteOrderEnum.BigEndian;
        private byte currentByte;
        private int bitShift;
        private byte[] temp;
        private long mark;

        public StreamContainer(Stream stream) => this.stream = stream;

        ~StreamContainer()
        { Dispose(false); }

        public ByteOrderEnum ByteOrder
        {
            get => byteOrder;
            set => byteOrder = value;
        }

        public bool IsAvailable => stream.Length > stream.Position;

        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => stream.Position;
            set => stream.Position = value;
        }

        public override long Length => stream.Length;

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override int GetHashCode() => stream.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(Span<byte> data) => stream.Read(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Read(byte[] data) => stream.Read(data, 0, data.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int Read(byte[] data, int offset, int count) => stream.Read(data, offset, count);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int ReadByte() => stream.ReadByte();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public sbyte ReadSByte() => unchecked((sbyte)stream.ReadByte());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadUByte() => unchecked((byte)stream.ReadByte());

        public int PeekByte()
        {
            var temp = Position;
            var peek = ReadByte();
            if (temp != Position)
            {
                stream.Seek(-1, SeekOrigin.Current);
            }
            return peek;
        }

        public byte PeekUByte(int offset)
        {
            var temp = Position;
            Skip(offset);
            var peek = ReadUByte();
            if (temp != Position)
            {
                stream.Seek(temp, SeekOrigin.Begin);
            }
            return peek;
        }

        public int ReadInt32()
        {
            Span<byte> data = stackalloc byte[sizeof(int)];
            Read(data);
            return ConvertUtils.ReadInt32(data, byteOrder);
        }

        public uint ReadUInt32()
        {
            Span<byte> data = stackalloc byte[sizeof(uint)];
            Read(data);
            return ConvertUtils.ReadUInt32(data, byteOrder);
        }

        public int ReadInt(int length)
        {
            Span<byte> data = stackalloc byte[length];
            Read(data);
            return ConvertUtils.ReadIntOffset(data, byteOrder);
        }

        public string ReadLine()
        {
            var buffer = new text::StringBuilder();
            while (true)
            {
                int c = stream.ReadByte();
                if (c == -1)
                    if (buffer.Length == 0)
                        return null;
                    else
                        break;
                else if (c == '\r'
                  || c == '\n')
                    break;

                buffer.Append((char)c);
            }
            return buffer.ToString();
        }

        public short ReadInt16()
        {
            Span<byte> data = stackalloc byte[sizeof(short)];
            Read(data);
            return ConvertUtils.ReadInt16(data, byteOrder);
        }

        public ushort ReadUInt16()
        {
            Span<byte> data = stackalloc byte[sizeof(ushort)];
            Read(data);
            return ConvertUtils.ReadUInt16(data, byteOrder);
        }

        public long ReadInt64()
        {
            Span<byte> data = stackalloc byte[sizeof(long)];
            Read(data);
            return ConvertUtils.ReadInt64(data, byteOrder);
        }

        public ulong ReadUInt64()
        {
            Span<byte> data = stackalloc byte[sizeof(ulong)];
            Read(data);
            return ConvertUtils.ReadUInt64(data, byteOrder);
        }

        public Memory<byte> ReadMemory(int length) => stream is IInputStream inputStream ? inputStream.ReadMemory(length) : ReadBytesAlloc(length);

        public Memory<byte> ReadBytesAlloc(int length)
        {
            if (Position + length > Length)
            {
                length = (int)(Length - Position);
            }
            var buffer = new byte[length];
            Read(buffer);
            return buffer;
        }

        public Span<byte> ReadSpan(int length) => stream is IInputStream inputStream ? inputStream.ReadSpan(length) : ReadBytesAlloc(length).Span;

        public string ReadString(int length) => StreamExtensions.ReadString(this, length);

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

        public long Seek(long offset) => stream.Seek(offset, SeekOrigin.Begin);

        public long Skip(long offset) => stream.Seek(offset, SeekOrigin.Current);

        public void Clear()
        { stream.SetLength(0); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte data) => stream.WriteByte(data);

        public void Write(int data, int length)
        {
            Span<byte> result = stackalloc byte[length];
            ConvertUtils.WriteIntOffset(result, data, byteOrder);
            Write(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte[] data) => stream.Write(data, 0, data.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(byte[] data, int offset, int length) => stream.Write(data, offset, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Write(ReadOnlySpan<byte> data) => stream.Write(data);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
                GC.SuppressFinalize(this);
            }
        }

        public override void Flush() => stream.Flush();

        public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

        public override void SetLength(long value) => stream.SetLength(value);

        public byte[] ToArray()
        {
            var position = Position;
            byte[] data = new byte[stream.Length];
            {
                stream.Position = 0;
                stream.Read(data, 0, data.Length);
            }
            stream.Position = position;
            return data;
        }

        public byte[] GetArrayBuffer()
        {
            return temp ??= ToArray();
        }

        public Memory<byte> AsMemory()
        {
            return GetArrayBuffer().AsMemory();
        }

        public Span<byte> AsSpan()
        {
            return GetArrayBuffer().AsSpan();
        }

        public void SetBuffer(Memory<byte> data)
        {
            SetLength(0);
            Write(data.Span);
        }

        public int Mark() => (int)(mark = Position);

        public int Mark(long position) => (int)(mark = Position + position);

        public void ResetMark() => Seek(mark);
    }
}