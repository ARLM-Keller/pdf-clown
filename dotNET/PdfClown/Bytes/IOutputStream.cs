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

using System;

namespace PdfClown.Bytes
{
    /**
      <summary>Output stream interface.</summary>
    */
    public interface IOutputStream : IStream
    {
        /**
          <summary>Clears the buffer of any data.</summary>
        */
        void Clear();

        void WriteByte(byte value);

        void Write(int number, int size);

        /**
          <summary>Writes a byte array into the stream.</summary>
          <param name="data">Byte array to write into the stream.</param>
        */
        void Write(byte[] data);

        /**
          <summary>Writes a byte span into the stream.</summary>
          <param name="data">Byte span to write into the stream.</param>
        */
        void Write(ReadOnlySpan<byte> data);

        /**
          <summary>Writes a byte range into the stream.</summary>
          <param name="data">Byte array to write into the stream.</param>
          <param name="offset">Location in the byte array at which writing begins.</param>
          <param name="length">Number of bytes to write.</param>
        */
        void Write(byte[] data, int offset, int length);               

    }
}