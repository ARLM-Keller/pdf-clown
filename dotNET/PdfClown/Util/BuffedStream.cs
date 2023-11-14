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

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfClown.Util
{
    public class BuffedStream<T>
    {
        private const int DefaultCapacity = 1 << 3;

        private Memory<T> data;
        private int length;
        private int position = 0;
        private int mark;

        public BuffedStream() : this(0)
        { }

        public BuffedStream(int capacity)
        {
            if (capacity < 1)
            { capacity = DefaultCapacity; }

            this.data = new T[capacity];
            this.length = 0;
        }

        public BuffedStream(Memory<T> data)
        {
            this.data = data;
            this.length = data.Length;
        }

        public BuffedStream(T[] data, int start, int end)
        {
            //unsafe mode;
            this.data = data;
            this.position = start;
            this.length = end - start;
        }

        public long Available { get => length - position; }

        public long Length
        {
            get => length;
        }

        public int Capacity => data.Length;

        public int Position
        {
            get => position;
            set => position = value;
        }

        public BuffedStream<T> Append(T data)
        {
            EnsureCapacity(1);
            this.data.Span[this.length++] = data;
            return this;
        }

        public BuffedStream<T> Append(T[] data) => Append(data.AsSpan(0, data.Length));

        public BuffedStream<T> Append(T[] data, int offset, int length) => Append(data.AsSpan(offset, length));

        public BuffedStream<T> Append(ReadOnlySpan<T> data)
        {
            EnsureCapacity(data.Length);
            data.CopyTo(AsSpan(this.length, data.Length));
            this.length += data.Length;
            return this;
        }

        public BuffedStream<T> Clone()
        {
            var clone = new BuffedStream<T>(Capacity);
            clone.Append(data.Span.Slice(0, length));
            return clone;
        }

        public void Delete(int index, int length)
        {
            var leftLength = this.length - (index + length);
            // Shift left the trailing data block to override the deleted data!
            //Array.Copy(data, index + length, this.data, index, leftLength);

            AsSpan(index + length, leftLength).CopyTo(AsSpan(index, leftLength));
            this.length -= length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(int index) => data.Span[index];

        public T[] GetArray(int index, int length)
        {
            if ((index + length) > this.length)
            {
                length = this.length - index;
            }
            var data = new T[length];
            AsSpan(index, length).CopyTo(data.AsSpan());
            return data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int index, int length)
        {
            return data.Span.Slice(index, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int index) => AsSpan(index, length - index);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan() => AsSpan(0, length);

        public void Insert(int index, T[] data) => Insert(index, data, 0, data.Length);

        public void Insert(int index, T[] data, int offset, int length) => Insert(index, data.AsSpan(offset, length));

        public void Insert(int index, ReadOnlySpan<T> data)
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
        }

        public void Replace(int index, T[] data) => Replace(index, data, 0, data.Length);

        public void Replace(int index, T[] data, int offset, int length) => Replace(index, data.AsSpan(offset, length));

        public void Replace(int index, ReadOnlySpan<T> data)
        {
            //Array.Copy(data, offset, this.data, index, data.Length);
            data.CopyTo(AsSpan(index, data.Length));
        }

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
            }
        }

        public int Read(T[] data) => Read(data, 0, data.Length);

        public int Read(T[] data, int offset, int length) => Read(data.AsSpan(offset, length));

        public int Read(Span<T> data)
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

        public Span<T> ReadSpan(int length)
        {
            if (position + length > Length)
            {
                length = (int)(Length - position);
            }
            var start = position;
            position += length;
            return AsSpan(start, length);
        }

        public T[] ReadBytes(int length)
        {
            if (position + length > Length)
            {
                length = (int)(Length - position);
            }
            var start = position;
            position += length;
            return GetArray(start, length);
        }

        public T Read()
        {
            if (position >= data.Length)
                return default(T);

            return Get(position++);
        }

        public T Peek()
        {
            if (position >= data.Length)
                return default(T);
            return Get(position);
        }

        public long Seek(long position)
        {
            if (position < 0)
            { position = 0; }
            else if (position > data.Length)
            { position = data.Length; }

            return this.position = (int)position;
        }

        public long Skip(long offset)
        {
            return Seek(position + offset);
        }

        public long Seek(int offset, SeekOrigin origin)
        {
            return origin switch
            {
                SeekOrigin.Current => Skip(offset),
                SeekOrigin.End => Seek(Length - offset),
                _ => Position = offset,
            };
        }

        public int Mark()
        {
            return mark = position;
        }

        public void ResetMark()
        {
            Seek(mark);
        }

        public void Reset()
        {
            SetLength(0);
        }

        public T[] ToArray()
        {
            return data.Slice(0, length).ToArray();
        }

        public Memory<T> GetMemoryBuffer()
        {
            return data.Slice(0, length);
        }

        public T[] GetArrayBuffer()
        {
            return Unsafe.As<Memory<T>, ArraySegment<T>>(ref data).Array;
        }

        public void SetBuffer(T[] data)
        {
            this.data = data;
            length = data.Length;
            position = 0;
        }

        public void SetBuffer(Memory<T> data)
        {
            this.data = data;
            length = data.Length;
            position = 0;
        }

        public void Clear() => SetLength(0);

        ///<summary>Check whether the buffer has sufficient room for
        ///  adding data.</summary>
        private void EnsureCapacity(int additionalLength)
        {
            int minCapacity = this.length + additionalLength;
            // Is additional data within the buffer capacity?
            if (minCapacity <= this.data.Length)
                return;

            // Additional data exceed buffer capacity.
            // Reallocate the buffer!
            var data = new T[System.Math.Max(this.data.Length << 1, minCapacity)];
            //Array.Copy(this.data, 0, data, 0, this.length);
            this.data.Span.CopyTo(data);
            this.data = data;
        }


    }
}