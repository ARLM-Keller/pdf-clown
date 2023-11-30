/*
  Copyright 2010-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Objects;
using PdfClown.Util;
using PdfClown.Util.IO;
using PdfClown.Util.Parsers;

using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfClown.Tokens
{
    /**
      <summary>Cross-reference stream containing cross-reference information [PDF:1.6:3.4.7].</summary>
      <remarks>It is alternative to the classic cross-reference table.</remarks>
    */
    public sealed class XRefStream : PdfStream, IDictionary<int, XRefEntry>
    {
        private const int FreeEntryType = 0;
        private const int InUseEntryType = 1;
        private const int InUseCompressedEntryType = 2;

        private static readonly double ByteBaseLog = Math.Log(256);

        private static readonly int EntryField0Size = 1;
        private static readonly int EntryField2Size = GetFieldSize(XRefEntry.GenerationUnreusable);

        /**
          <summary>Gets the number of bytes needed to store the specified value.</summary>
          <param name="maxValue">Maximum storable value.</param>
        */
        private static int GetFieldSize(int maxValue)
        { return (int)Math.Ceiling(Math.Log(maxValue) / ByteBaseLog); }


        private SortedDictionary<int, XRefEntry> entries;

        public XRefStream(File file)
            : this(new PdfDictionary() { { PdfName.Type, PdfName.XRef } }, new ByteStream())
        {
            PdfDictionary header = Header;
            foreach (var entry in file.Trailer)
            {
                PdfName key = entry.Key;
                if (key.Equals(PdfName.Root)
                  || key.Equals(PdfName.Info)
                  || key.Equals(PdfName.ID))
                { header[key] = entry.Value; }
            }
        }

        public XRefStream(PdfDictionary header, IByteStream body)
            : base(header, body)
        { }

        public override PdfObject Accept(IVisitor visitor, object data)
        { return visitor.Visit(this, data); }

        /**
          <summary>Gets the byte offset from the beginning of the file
          to the beginning of the previous cross-reference stream.</summary>
          <returns>-1 in case no linked stream exists.</returns>
        */
        public int LinkedStreamOffset
        {
            get => Header.GetInt(PdfName.Prev, -1);
        }

        public override void WriteTo(IOutputStream stream, File context)
        {
            if (entries != null)
            { Flush(stream); }

            base.WriteTo(stream, context);
        }

        public void Add(int key, XRefEntry value)
        {
            Entries.Add(key, value);
        }

        public bool ContainsKey(int key)
        {
            return Entries.ContainsKey(key);
        }

        public ICollection<int> Keys => Entries.Keys;

        public bool Remove(int key)
        {
            return Entries.Remove(key);
        }

        public XRefEntry this[int key]
        {
            get => Entries[key];
            set => Entries[key] = value;
        }

        public bool TryGetValue(int key, out XRefEntry value)
        {
            if (ContainsKey(key))
            {
                value = this[key];
                return true;
            }
            else
            {
                value = default(XRefEntry);
                return false;
            }
        }

        public ICollection<XRefEntry> Values => Entries.Values;

        void ICollection<KeyValuePair<int, XRefEntry>>.Add(KeyValuePair<int, XRefEntry> entry)
        {
            Add(entry.Key, entry.Value);
        }

        public void Clear()
        {
            if (entries == null)
            { entries = new SortedDictionary<int, XRefEntry>(); }
            else
            { entries.Clear(); }
        }

        bool ICollection<KeyValuePair<int, XRefEntry>>.Contains(KeyValuePair<int, XRefEntry> entry)
        {
            return ((ICollection<KeyValuePair<int, XRefEntry>>)Entries).Contains(entry);
        }

        public void CopyTo(KeyValuePair<int, XRefEntry>[] entries, int index)
        {
            Entries.CopyTo(entries, index);
        }

        public int Count => Entries.Count;

        public bool IsReadOnly => false;

        public bool Remove(KeyValuePair<int, XRefEntry> entry)
        {
            XRefEntry value;
            if (TryGetValue(entry.Key, out value)
              && value.Equals(entry.Value))
                return Entries.Remove(entry.Key);
            else
                return false;
        }

        IEnumerator<KeyValuePair<int, XRefEntry>> IEnumerable<KeyValuePair<int, XRefEntry>>.GetEnumerator()
        {
            return Entries.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<int, XRefEntry>>)this).GetEnumerator();
        }

        private SortedDictionary<int, XRefEntry> Entries
        {
            get
            {
                if (entries == null)
                {
                    entries = new SortedDictionary<int, XRefEntry>();

                    IByteStream body = Body;
                    if (body.Length > 0)
                    {
                        PdfDictionary header = Header;
                        int size = header.GetInt(PdfName.Size);
                        int[] entryFieldSizes;
                        {
                            PdfArray entryFieldSizesObject = (PdfArray)header[PdfName.W];
                            entryFieldSizes = new int[entryFieldSizesObject.Count];
                            for (int index = 0, length = entryFieldSizes.Length; index < length; index++)
                            { entryFieldSizes[index] = entryFieldSizesObject.GetInt(index); }
                        }

                        PdfArray subsectionBounds;
                        if (header.ContainsKey(PdfName.Index))
                        {
                            subsectionBounds = (PdfArray)header[PdfName.Index];
                        }
                        else
                        {
                            subsectionBounds = new PdfArray();
                            subsectionBounds.Add(PdfInteger.Get(0));
                            subsectionBounds.Add(PdfInteger.Get(size));
                        }

                        body.ByteOrder = ByteOrderEnum.BigEndian;
                        body.Seek(0);

                        IEnumerator<PdfDirectObject> subsectionBoundIterator = subsectionBounds.GetEnumerator();
                        while (subsectionBoundIterator.MoveNext())
                        {
                            try
                            {
                                int start = ((PdfInteger)subsectionBoundIterator.Current).IntValue;
                                subsectionBoundIterator.MoveNext();
                                int count = ((PdfInteger)subsectionBoundIterator.Current).IntValue;
                                for (int entryIndex = start, length = start + count; entryIndex < length; entryIndex++)
                                {
                                    int entryFieldType = (entryFieldSizes[0] == 0 ? 1 : body.ReadInt(entryFieldSizes[0]));
                                    switch (entryFieldType)
                                    {
                                        case FreeEntryType:
                                            {
                                                int nextFreeObjectNumber = body.ReadInt(entryFieldSizes[1]);
                                                int generation = body.ReadInt(entryFieldSizes[2]);
                                                entries[entryIndex] = new XRefEntry(entryIndex, generation, nextFreeObjectNumber, XRefEntry.UsageEnum.Free);
                                                break;
                                            }
                                        case InUseEntryType:
                                            {
                                                int offset = body.ReadInt(entryFieldSizes[1]);
                                                int generation = body.ReadInt(entryFieldSizes[2]);
                                                entries[entryIndex] = new XRefEntry(entryIndex, generation, offset, XRefEntry.UsageEnum.InUse);
                                                break;
                                            }
                                        case InUseCompressedEntryType:
                                            {
                                                int streamNumber = body.ReadInt(entryFieldSizes[1]);
                                                int innerNumber = body.ReadInt(entryFieldSizes[2]);
                                                entries[entryIndex] = new XRefEntry(entryIndex, innerNumber, streamNumber);
                                                break;
                                            }
                                        default:
                                            throw new NotSupportedException("Unknown xref entry type '" + entryFieldType + "'.");
                                    }
                                }
                            }
                            catch (Exception e)
                            { throw new ParseException("Malformed cross-reference stream object.", e); }
                        }
                    }
                }
                return entries;
            }
        }

        /**
          <summary>Serializes the xref stream entries into the stream body.</summary>
        */
        private void Flush(IOutputStream stream)
        {
            // 1. Body.
            PdfArray indexArray = new PdfArray();
            int[] entryFieldSizes = new int[]
              {
                  EntryField0Size,
                  GetFieldSize((int)stream.Length), // NOTE: We assume this xref stream is the last indirect object.
                  EntryField2Size
              };
            {
                // Get the stream buffer!
                IByteStream body = Body;

                // Delete the old entries!
                body.SetLength(0);

                // Serializing the entries into the stream buffer...
                int prevObjectNumber = -2; // Previous-entry object number.
                foreach (XRefEntry entry in entries.Values)
                {
                    int entryNumber = entry.Number;
                    if (entryNumber - prevObjectNumber != 1) // Current subsection terminated.
                    {
                        if (indexArray.Count > 0)
                        { indexArray.Add(PdfInteger.Get(prevObjectNumber - indexArray.GetInt(indexArray.Count - 1) + 1)); } // Number of entries in the previous subsection.
                        indexArray.Add(PdfInteger.Get(entryNumber)); // First object number in the next subsection.
                    }
                    prevObjectNumber = entryNumber;

                    switch (entry.Usage)
                    {
                        case XRefEntry.UsageEnum.Free:
                            body.WriteByte(FreeEntryType);
                            body.Write(entry.Offset, entryFieldSizes[1]);
                            body.Write(entry.Generation, entryFieldSizes[2]);
                            break;
                        case XRefEntry.UsageEnum.InUse:
                            body.WriteByte(InUseEntryType);
                            body.Write(entry.Offset, entryFieldSizes[1]);
                            body.Write(entry.Generation, entryFieldSizes[2]);
                            break;
                        case XRefEntry.UsageEnum.InUseCompressed:
                            body.WriteByte(InUseCompressedEntryType);
                            body.Write(entry.StreamNumber, entryFieldSizes[1]);
                            body.Write(entry.Offset, entryFieldSizes[2]);
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
                indexArray.Add(PdfInteger.Get(prevObjectNumber - indexArray.GetInt(indexArray.Count - 1) + 1)); // Number of entries in the previous subsection.
            }

            // 2. Header.
            {
                PdfDictionary header = Header;
                header[PdfName.Index] = indexArray;
                header[PdfName.Size] = PdfInteger.Get(File.IndirectObjects.Count + 1);
                header[PdfName.W] = new PdfArray(3)
                {
                  PdfInteger.Get(entryFieldSizes[0]),
                  PdfInteger.Get(entryFieldSizes[1]),
                  PdfInteger.Get(entryFieldSizes[2])
                };
            }
        }
    }
}