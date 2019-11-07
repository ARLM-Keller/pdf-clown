﻿/*
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
    public class ICCCrdInfoType : ICCTag
    {
        public ICCCrdInfoType(ICCTagTable table) : base(table)
        {
        }

        public const uint crdi = 0x63726469;
        public uint Reserved = 0x00000000;
        public uint ProductNameLength;
        public string ProductName;
        public uint RenderingIntent0Lenght;
        public string RenderingIntent0;
        public uint RenderingIntent1Lenght;
        public string RenderingIntent1;
        public uint RenderingIntent2Lenght;
        public string RenderingIntent2;
        public uint RenderingIntent3Lenght;
        public string RenderingIntent3;

        public override void Load(PdfClown.Bytes.Buffer buffer)
        {
            buffer.Seek(Table.Offset);
            buffer.ReadUnsignedInt();
            buffer.ReadUnsignedInt();
            ProductNameLength = buffer.ReadUnsignedInt();
            ProductName = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes((int)ProductNameLength));
            RenderingIntent0Lenght = buffer.ReadUnsignedInt();
            RenderingIntent0 = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes((int)RenderingIntent0Lenght));
            RenderingIntent1Lenght = buffer.ReadUnsignedInt();
            RenderingIntent1 = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes((int)RenderingIntent1Lenght));
            RenderingIntent2Lenght = buffer.ReadUnsignedInt();
            RenderingIntent2 = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes((int)RenderingIntent2Lenght));
            RenderingIntent3Lenght = buffer.ReadUnsignedInt();
            RenderingIntent3 = System.Text.Encoding.ASCII.GetString(buffer.ReadBytes((int)RenderingIntent3Lenght));

        }
    }
}