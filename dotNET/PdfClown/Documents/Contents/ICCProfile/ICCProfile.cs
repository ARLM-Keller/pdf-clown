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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PdfClown.Bytes;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    public class ICCProfile
    {
        public static readonly Dictionary<uint, Type> Types = new Dictionary<uint, Type>()
        {
            { ICCChromaticityType.chrm, typeof(ICCChromaticityType) },
            { ICCCrdInfoType.crdi, typeof(ICCCrdInfoType) },
            { ICCCurveType.curv, typeof(ICCCurveType) },
            { ICCDataType.data, typeof(ICCDataType) },
            { ICCDateTimeType.dtim, typeof(ICCDateTimeType) },
            { ICCDeviceSettingsType.devs, typeof(ICCDeviceSettingsType) },
            { ICCLut16Type.mft2, typeof(ICCLut16Type) },
            { ICCLut8Type.mft1, typeof(ICCLut8Type) },
            { ICCMeasurementType.meas, typeof(ICCMeasurementType) },
            { ICCNamedColorType.ncol, typeof(ICCNamedColorType) },
            { ICCNamedColor2Type.ncl2, typeof(ICCNamedColor2Type)  },
            { ICCProfileSequenceDescType.pseq, typeof(ICCProfileSequenceDescType)  },
            { ICCResponseCurveSet16Type.rcs2, typeof(ICCResponseCurveSet16Type)  },
            { ICCS15Fixed16ArrayType.sf32, typeof(ICCS15Fixed16ArrayType)  },
            { ICCU16Fixed16ArrayType.uf32, typeof(ICCU16Fixed16ArrayType)  },
            { ICCUInt16ArrayType.ui16, typeof(ICCUInt16ArrayType)  },
            { ICCUInt32ArrayType.ui32, typeof(ICCUInt32ArrayType)  },
            { ICCUInt64ArrayType.ui64, typeof(ICCUInt64ArrayType)  },
            { ICCUInt8ArrayType.ui08, typeof(ICCUInt8ArrayType)  },
            { ICCScreeningType.scrn, typeof(ICCScreeningType)  },
            { ICCSignatureType.sig, typeof(ICCSignatureType)  },
            { ICCTextDescriptionType.desc, typeof(ICCTextDescriptionType)  },
            { ICCTextType.text, typeof(ICCTextType)  },
            { ICCUcrbgType.bfd, typeof(ICCUcrbgType)  },
            { ICCViewingConditionsTypee.view, typeof(ICCViewingConditionsTypee)  },
            { ICCXYZType.XYZ, typeof(ICCXYZType)  },

        };

        public static ICCProfile Load(Memory<byte> data)
        {
            var profile = new ICCProfile();
            var header = new ICCHeader();
            var buffer = new Bytes.ByteStream(data);
            header.ProfileSize = buffer.ReadUInt32();
            header.CMMTypeSignature = buffer.ReadUInt32();
            header.ProfileVersionNumber.Major = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Minor = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Reserv1 = (byte)buffer.ReadByte();
            header.ProfileVersionNumber.Reserv2 = (byte)buffer.ReadByte();
            header.ProfileDeviceClassSignature = (ICCProfileDeviceSignatures)buffer.ReadUInt32();
            header.ColorSpaceOfData = (ICCColorSpaceSignatures)buffer.ReadUInt32();
            header.ProfileConnectionSpace = (ICCColorSpaceSignatures)buffer.ReadUInt32();
            header.DateCreated.Load(buffer);
            header.acsp = buffer.ReadUInt32();
            header.PrimaryPlatformSignature = (ICCPrimaryPlatformSignatures)buffer.ReadUInt32();
            header.Flags = (ICCProfileFlags)buffer.ReadUInt32();
            header.DeviceManufacturer = buffer.ReadUInt32();
            header.DeviceModel = buffer.ReadUInt32();
            header.DeviceAttributes.Load(buffer);
            header.RenderingIntent.Intents = buffer.ReadUInt16();
            header.RenderingIntent.Reserved = buffer.ReadUInt16();
            header.XYZ.Load(buffer);
            header.ProfileCreatorSignature = buffer.ReadUInt32();
            header.FutureUse = new byte[44];
            buffer.Read(header.FutureUse);
            profile.Header = header;
            var tagCount = buffer.ReadUInt32();
            for (int i = 0; i < tagCount; i++)
            {
                var tag = new ICCTagTable();
                tag.Signature = (ICCTagTypes)buffer.ReadUInt32();
                tag.Offset = buffer.ReadUInt32();
                tag.ElementSize = buffer.ReadUInt32();
                profile.Tags[tag.Signature] = tag;
            }
            foreach (var tagTable in profile.Tags.Values)
            {
                buffer.Seek(tagTable.Offset);
                var key = buffer.ReadUInt32();
                if (Types.TryGetValue(key, out var type))
                {
                    tagTable.Tag = (ICCTag)Activator.CreateInstance(type, tagTable);
                    tagTable.Tag.Profile = profile;
                    tagTable.Tag.Load(buffer);
                }
            }
            return profile;
        }

        public float MapGrayDisplay(float g)
        {
            var trc = Tags[ICCTagTypes.grayTRCTag].Tag as ICCCurveType;
            if (trc.Values.Length > 1)
            {
                return LinearCurve(g, trc);
            }
            return g;
        }

        public float LinearCurve(float x, ICCCurveType curveType)
        {
            var gIndex = (int)Linear(x, 0, 1, 0, curveType.Values.Length);
            var value = curveType.Values[gIndex];
            return (float)Linear(value, curveType.Values[0], curveType.Values[curveType.Values.Length - 1], 0, 1);
        }

        static public double Linear(double x, double x0, double x1, double y0, double y1)
        {
            if ((x1 - x0) == 0)
            {
                return (y0 + y1) / 2;
            }
            return y0 + (x - x0) * ((y1 - y0) / (x1 - x0));
        }

        //https://stackoverflow.com/a/2887/4682355
        public static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

        }

        public ICCHeader Header;

        public Dictionary<ICCTagTypes, ICCTagTable> Tags { get; set; } = new Dictionary<ICCTagTypes, ICCTagTable>();
    }
}