/*
  Copyright 2006-2011 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Alexandr Vassilyev (alexandr_vslv@mail.ru)

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

namespace PdfClown.Documents.Contents.ColorSpaces
{
    public class ICCCurveType : ICCTag
    {
        public ICCCurveType(ICCTagTable table) : base(table)
        {
        }

        public const uint curv = 0x63757276;
        public uint Reserved = 0x00000000;
        public uint Count;
        public ushort[] Values;
        public float Gamma;

        public override void Load(PdfClown.Bytes.Buffer buffer)
        {
            buffer.Seek(Table.Offset);
            buffer.ReadUnsignedInt();
            buffer.ReadUnsignedInt();
            Count = buffer.ReadUnsignedInt();
            Values = new ushort[Count];
            if (Count > 1)
            {
                for (int i = 0; i < Count; i++)
                {
                    Values[i] = buffer.ReadUnsignedShort();
                }
            }
            else if (Count == 1)
            {
                Gamma = buffer.ReadFixed16(); 
            }

        }
    }
}