/*
  Copyright 2015 Stefano Chizzolini. http://www.pdfclown.org

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
using System.IO;
using System.Security.Cryptography;

namespace PdfClown.Util.IO
{
    /**
      <summary>IO utilities.</summary>
    */
    public static class IOUtils
    {
        public static bool Exists(string path)
        { return Directory.Exists(path) || File.Exists(path); }

        public static void Write(this Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void Reset(this MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.SetLength(0);
        }

        public static void Update(this HashAlgorithm hash, byte oneByte)
        {
            Update(hash, new byte[] { oneByte });
        }

        public static void Update(this HashAlgorithm hash, byte[] bytes)
        {
            Update(hash, bytes, 0, bytes.Length);
        }

        public static void Update(this HashAlgorithm hash, byte[] bytes, int offcet, int count)
        {
            hash.TransformBlock(bytes, offcet, count, null, 0);
        }

        public static byte[] Digest(this HashAlgorithm hash)
        {
            return Digest(hash, Array.Empty<byte>());
        }

        public static byte[] Digest(this HashAlgorithm hash, byte[] bytes)
        {
            hash.TransformFinalBlock(bytes, 0, bytes.Length);
            return hash.Hash;
        }

        public static byte[] DoFinal(this ICryptoTransform transform, byte[] bytes)
        {
            return transform.TransformFinalBlock(bytes, 0, bytes.Length);
        }
    }
}

