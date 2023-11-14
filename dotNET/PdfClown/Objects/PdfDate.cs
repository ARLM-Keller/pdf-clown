/*
  Copyright 2006-2012 Stefano Chizzolini. http://www.pdfclown.org

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

using tokens = PdfClown.Tokens;
using PdfClown.Util.Parsers;

using System;
using System.Globalization;
using System.Text;
using System.Xml.Schema;
using PdfClown.Util;

namespace PdfClown.Objects
{
    /**
      <summary>PDF date object [PDF:1.6:3.8.3].</summary>
    */
    public sealed class PdfDate : PdfString
    {
        private const string FormatString = "yyyyMMddHHmmsszzz";
        private DateTime? date;

        /**
          <summary>Gets the object equivalent to the given value.</summary>
        */
        public static PdfDate Get(DateTime? value)
        {
            return value.HasValue ? new PdfDate(value.Value) : null;
        }

        /**
          <summary>Converts a PDF date literal into its corresponding date.</summary>
          <exception cref="PdfClown.Util.Parsers.ParseException">Thrown when date literal parsing fails.</exception>
        */
        public static DateTime? ToDate(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
                return null;
            value = value.Trim();
            if (value.Equals("D:", StringComparison.Ordinal)
                || value.Length < 6)
                return null;
            // 1. Normalization.
            var dateBuilder = new StringStream();
            try
            {
                int length = value.Length;
                // Year (YYYY). 
                dateBuilder.Append(value.Slice(2, 4)); // NOTE: Skips the "D:" prefix; Year is mandatory.
                // Month (MM).
                dateBuilder.Append(length < 8 ? "01" : value.Slice(6, 2));
                // Day (DD).
                dateBuilder.Append(length < 10 ? "01" : value.Slice(8, 2));
                // Hour (HH).
                dateBuilder.Append(length < 12 ? "00" : value.Slice(10, 2));
                // Minute (mm).
                dateBuilder.Append(length < 14 ? "00" : value.Slice(12, 2));
                // Second (SS).
                dateBuilder.Append(length < 16 ? "00" : value.Slice(14, 2));
                // Local time / Universal Time relationship (O).
                dateBuilder.Append(length < 17 || value.Slice(16, 1).Equals("Z", StringComparison.Ordinal) ? "+" : value.Slice(16, 1));
                // UT Hour offset (HH').
                dateBuilder.Append(length < 19 ? "00" : value.Slice(17, 2));
                // UT Minute offset (mm').
                dateBuilder.Append(':').Append(length < 22 ? "00" : value.Slice(20, 2));
            }
            catch (Exception exception)
            { throw new ParseException("Failed to normalize the date string.", exception); }

            // 2. Parsing.
            return DateTime.TryParseExact(
                dateBuilder.AsSpan(),
                FormatString,
                CultureInfo.InvariantCulture,//("en-US")
                DateTimeStyles.None,
                out var date) ? date : DateTime.MinValue;
            //{ throw new ParseException("Failed to parse the date string.", exception); }
        }

        private static string Format(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                value = value.ToLocalTime();
            return ("D:" + value.ToString(FormatString).Replace(':', '\'') + "'");
        }

        public PdfDate(DateTime value)
        {
            Value = value;
        }

        public override PdfObject Accept(IVisitor visitor, object data)
        {
            return visitor.Visit(this, data);
        }

        public override SerializationModeEnum SerializationMode
        {
            get => base.SerializationMode;
            set
            {/* NOOP: Serialization MUST be kept literal. */}
        }

        public override object Value
        {
            get => DateValue;
            protected set
            {
                if (value is DateTime dateTimeValue)
                {
                    RawValue = tokens::Encoding.Pdf.Encode(Format(dateTimeValue));
                    date = dateTimeValue;
                }
            }
        }

        public DateTime? DateValue
        {
            get => date ??= ToDate((string)base.Value);
            set => Value = value;
        }
    }
}
