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

using PdfClown.Bytes;
using PdfClown.Files;

using System;
using System.Reflection;

namespace PdfClown.Objects
{
    /**
      <summary>Abstract PDF simple object.</summary>
    */
    public abstract class PdfSimpleObject<TValue> : PdfDirectObject, IPdfSimpleObject<TValue>
    {
        /**
          <summary>Gets the object equivalent to the given value.</summary>
        */
        public static PdfDirectObject Get(object value)
        {
            if (value == null)
                return null;

            if (value is int intValue)
                return PdfInteger.Get(intValue);
            else if (value is double doubleValue)
                return PdfReal.Get(doubleValue);
            else if (value is float singleValue)
                return PdfReal.Get(singleValue);
            else if (value is string stringValue)
                return PdfTextString.Get(stringValue);
            else if (value is DateTime dateTimeValue)
                return PdfDate.Get(dateTimeValue);
            else if (value is bool booleanValue)
                return PdfBoolean.Get(booleanValue);
            else if (value is long longValue)
                return PdfInteger.Get((int)longValue);
            else
                throw new NotImplementedException();
        }

        public static double? GetDoubleValue(PdfObject obj)
        {
            object value = GetValue(obj);
            return value switch
            {
                null => null,
                double dValue => dValue,
                _ => Convert.ToDouble(value)
            };
        }

        /**
          <summary>Gets the value corresponding to the given object.</summary>
          <param name="obj">Object to extract the value from.</param>
        */
        public static object GetValue(PdfObject obj) => GetValue(obj, null);

        /**
          <summary>Gets the value corresponding to the given object.</summary>
          <param name="obj">Object to extract the value from.</param>
          <param name="defaultValue">Value returned in case the object's one is undefined.</param>
        */
        public static object GetValue(PdfObject obj, object defaultValue)
        {
            object value = null;
            obj = Resolve(obj);
            if (obj is IPdfValued valued)
            {
                value = valued.Value;
            }
            return value ?? defaultValue;
        }

        protected TValue value;

        public PdfSimpleObject()
        { }

        public sealed override PdfObject Parent
        {
            get => null;  // NOTE: As simple objects are immutable, no parent can be associated.
            internal set {/* NOOP: As simple objects are immutable, no parent can be associated. */}
        }

        /**
          <summary>Gets/Sets the low-level representation of the value.</summary>
        */
        public virtual TValue RawValue
        {
            get => value;
            protected set => this.value = value;
        }

        public override bool Updateable
        {
            get => false;  // NOTE: Simple objects are immutable.
            set {/* NOOP: As simple objects are immutable, no update can be done. */}
        }

        public sealed override bool Updated
        {
            get => false;  // NOTE: Simple objects are immutable.
            protected internal set
            {/* NOOP: As simple objects are immutable, no update can be done. */}
        }

        /**
          <summary>Gets/Sets the high-level representation of the value.</summary>
        */
        public virtual object Value
        {
            get => value;
            protected set => this.value = (TValue)value;
        }

        protected internal override bool Virtual
        {
            get => false;
            set {/* NOOP */}
        }

        public override PdfObject Clone(File context) => this;  // NOTE: Simple objects are immutable.

        public override bool Equals(object @object)
        {
            return base.Equals(@object)
              || (@object != null
                && @object.GetType().Equals(GetType())
                && ((PdfSimpleObject<TValue>)@object).RawValue.Equals(RawValue));
        }

        public override int GetHashCode() => RawValue.GetHashCode();

        public override PdfObject Swap(PdfObject other) => throw new NotSupportedException("Immutable object");

        public override string ToString() => Value.ToString();

    }
}