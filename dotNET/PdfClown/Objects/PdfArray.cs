/*
  Copyright 2006-2013 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown;
using PdfClown.Bytes;
using PdfClown.Files;
using PdfClown.Tokens;
using PdfClown.Util.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using text = System.Text;

namespace PdfClown.Objects
{
    /**
      <summary>PDF array object, that is a one-dimensional collection of (possibly-heterogeneous)
      objects arranged sequentially [PDF:1.7:3.2.5].</summary>
    */
    public sealed class PdfArray : PdfDirectObject, IList<PdfDirectObject>
    {
        private static readonly byte[] BeginArrayChunk = Encoding.Pdf.Encode(Keyword.BeginArray);
        private static readonly byte[] EndArrayChunk = Encoding.Pdf.Encode(Keyword.EndArray);

        internal List<PdfDirectObject> items;

        private PdfObject parent;
        private bool updateable = true;
        private bool updated;
        private bool virtual_;

        public PdfArray() : this(10)
        { }

        public PdfArray(int capacity)
        {
            items = new List<PdfDirectObject>(capacity);
        }

        public PdfArray(params PdfDirectObject[] items)
            : this(items.Length)
        {
            Updateable = false;
            Extension.AddRange(this, items);
            Updateable = true;
        }

        public PdfArray(IEnumerable<PdfDirectObject> items)
            : this(items.Count())
        {
            Updateable = false;
            Extension.AddRange(this, items);
            Updateable = true;
        }

        public override PdfObject Accept(IVisitor visitor, object data)
        {
            return visitor.Visit(this, data);
        }

        public override int CompareTo(PdfDirectObject obj)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object @object)
        {
            return base.Equals(@object)
              || (@object != null
                && @object.GetType().Equals(GetType())
                && ((PdfArray)@object).items.Equals(items));
        }

        public IPdfNumber GetNumber(int index) => (IPdfNumber)Resolve(index);

        public float GetFloat(int index, float def = 0) => ((IPdfNumber)Resolve(index))?.FloatValue ?? def;

        public float? GetNFloat(int index, float? def = null) => (Resolve(index) as IPdfNumber)?.FloatValue ?? def;

        public void SetFloat(int index, float? value) => this[index] = PdfReal.Get(value);

        public double GetDouble(int index, double def = 0) => ((IPdfNumber)Resolve(index))?.RawValue ?? def;

        public void SetDouble(int index, double? value) => this[index] = PdfReal.Get(value);

        public int GetInt(int index, int def = 0) => ((IPdfNumber)Resolve(index))?.IntValue ?? def;

        public int? GetNInt(int index, int? def = null) => (Resolve(index) as IPdfNumber)?.IntValue ?? def;

        public void SetInt(int index, int? value) => this[index] = PdfInteger.Get(value);

        public bool GetBool(int index, bool def = false) => ((PdfBoolean)Resolve(index))?.BooleanValue ?? def;

        public void SetBool(int index, bool? value) => this[index] = PdfBoolean.Get(value);

        public string GetString(int index, string def = null) => ((IPdfString)Resolve(index))?.StringValue ?? def;

        public void SetName(int index, string value) => this[index] = PdfName.Get(value);

        public void SetText(int index, string value) => this[index] = PdfTextString.Get(value);

        /**
          <summary>Gets the value corresponding to the given index, forcing its instantiation as a direct
          object in case of missing entry.</summary>
          <param name="index">Index of the item to return.</param>
          <param name="itemClass">Class to use for instantiating the item in case of missing entry.</param>
        */
        public PdfDirectObject Get<T>(int index) where T : PdfDataObject, new()
        { return Get<T>(index, true); }

        /**
          <summary>Gets the value corresponding to the given index, forcing its instantiation in case
          of missing entry.</summary>
          <param name="index">Index of the item to return.</param>
          <param name="direct">Whether the item has to be instantiated directly within its container
          instead of being referenced through an indirect object.</param>
        */
        public PdfDirectObject Get<T>(int index, bool direct) where T : PdfDataObject, new()
        {
            PdfDirectObject item;
            if (index == Count
              || (item = this[index]) == null
              || !item.Resolve().GetType().Equals(typeof(T)))
            {
                /*
                  NOTE: The null-object placeholder MUST NOT perturb the existing structure; therefore:
                    - it MUST be marked as virtual in order not to unnecessarily serialize it;
                    - it MUST be put into this array without affecting its update status.
                */
                try
                {
                    item = (PdfDirectObject)Include(direct
                      ? (PdfDataObject)new T()
                      : new PdfIndirectObject(File, new T(), new XRefEntry(0, 0)).Reference);
                    if (index == Count)
                    { items.Add(item); }
                    else if (item == null)
                    { items[index] = item; }
                    else
                    { items.Insert(index, item); }
                    item.Virtual = true;
                }
                catch (Exception e)
                { throw new Exception(typeof(T).Name + " failed to instantiate.", e); }
            }
            return item;
        }

        public override int GetHashCode()
        {
            return items.GetHashCode();
        }

        public override PdfObject Parent
        {
            get => parent;
            internal set => parent = value;
        }

        public override bool Updateable
        {
            get => updateable;
            set => updateable = value;
        }

        public override bool Updated
        {
            get => updated;
            protected internal set => updated = value;
        }

        /**
          <summary>Gets the dereferenced value corresponding to the given index.</summary>
          <remarks>This method takes care to resolve the value returned by
          <see cref="this[int]">this[int]</see>.</remarks>
          <param name="index">Index of the item to return.</param>
        */
        public PdfDataObject Resolve(int index) => Resolve(this[index]);

        /**
          <summary>Gets the dereferenced value corresponding to the given index, forcing its
          instantiation in case of missing entry.</summary>
          <remarks>This method takes care to resolve the value returned by
          <see cref="Get<T>">Get<T></see>.</remarks>
          <param name="index">Index of the item to return.</param>
        */
        public T Resolve<T>(int index) where T : PdfDataObject, new()
        {
            return (T)Resolve(Get<T>(index));
        }

        public override PdfObject Swap(PdfObject other)
        {
            PdfArray otherArray = (PdfArray)other;
            List<PdfDirectObject> otherItems = (List<PdfDirectObject>)otherArray.items;
            // Update the other!
            otherArray.items = this.items;
            otherArray.Update();
            // Update this one!
            this.items = otherItems;
            this.Update();
            return this;
        }

        public override string ToString()
        {
            var buffer = new text::StringBuilder();
            {
                // Begin.
                buffer.Append("[ ");
                // Elements.
                foreach (PdfDirectObject item in items)
                { buffer.Append(PdfDirectObject.ToString(item)).Append(" "); }
                // End.
                buffer.Append("]");
            }
            return buffer.ToString();
        }

        public override void WriteTo(IOutputStream stream, File context)
        {
            // Begin.
            stream.Write(BeginArrayChunk);
            // Elements.
            foreach (PdfDirectObject item in items)
            {
                if (item != null && item.Virtual)
                    continue;

                PdfDirectObject.WriteTo(stream, context, item); stream.Write(Chunk.Space);
            }
            // End.
            stream.Write(EndArrayChunk);
        }

        public int IndexOf(PdfDirectObject item)
        {
            return items.IndexOf(item);
        }

        public void Insert(int index, PdfDirectObject item)
        {
            items.Insert(index, (PdfDirectObject)Include(item));
            Update();
        }

        public void RemoveAt(int index)
        {
            PdfDirectObject oldItem = items[index];
            items.RemoveAt(index);
            Exclude(oldItem);
            Update();
        }

        public PdfDirectObject this[int index]
        {
            get => items[index];
            set
            {
                PdfDirectObject oldItem = items[index];
                items[index] = (PdfDirectObject)Include(value);
                Exclude(oldItem);
                Update();
            }
        }

        public void Add(PdfDirectObject item)
        {
            items.Add((PdfDirectObject)Include(item));
            Update();
        }

        public void AddRange(IEnumerable<PdfDirectObject> source)
        {
            items.AddRange(source.Select(x => (PdfDirectObject)Include(x)));
            Update();
        }

        public void Clear()
        {
            while (items.Count > 0)
            { RemoveAt(0); }
        }

        public bool Contains(PdfDirectObject item) => items.Contains(item);

        public void CopyTo(PdfDirectObject[] items, int index) => this.items.CopyTo(items, index);

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public bool Remove(PdfDirectObject item)
        {
            if (!items.Remove(item))
                return false;

            Exclude((PdfDirectObject)item);
            Update();
            return true;
        }

        public IEnumerator<PdfDirectObject> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<PdfDirectObject>)this).GetEnumerator();
        }

        public static PdfArray FromInts(ICollection<int> source)
        {
            var array = new PdfArray(source.Count);
            array.AddRange(source.Select(p => PdfInteger.Get(p)));
            return array;
        }

        public static PdfArray FromFloats(ICollection<float> source)
        {
            var array = new PdfArray(source.Count);
            array.AddRange(source.Select(p => PdfReal.Get(p)));
            return array;
        }

        protected internal override bool Virtual
        {
            get => virtual_;
            set => virtual_ = value;
        }

        public override PdfReference Reference => base.Reference;
    }
}