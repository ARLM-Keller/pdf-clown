/*
  Copyright 2006-2011 Stefano Chizzolini. http://www.pdfclown.org

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

using org.pdfclown.documents;
using org.pdfclown.files;
using org.pdfclown.objects;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace org.pdfclown.documents.contents.colorSpaces
{
    /**
      <summary>ICC-based color space [PDF:1.6:4.5.4].</summary>
    */
    // TODO:IMPL improve profile support (see ICC.1:2003-09 spec)!!!
    [PDF(VersionEnum.PDF13)]
    public sealed class ICCBasedColorSpace
      : ColorSpace
    {
        private SKColorSpace skColorSpace;
        private SKMatrix44 xyzD50 = SKMatrix44.CreateIdentity();
        private ICCProfile iccProfile;
        #region dynamic
        #region constructors
        //TODO:IMPL new element constructor!

        internal ICCBasedColorSpace(PdfDirectObject baseObject) : base(baseObject) { }
        #endregion

        #region interface
        #region public
        public override object Clone(Document context)
        {
            throw new NotImplementedException();
        }

        public override int ComponentCount
        {
            get { return N; }
        }

        public override Color DefaultColor
        {
            get { return DeviceGrayColor.Default; } // FIXME:temporary hack...
        }

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        {
            if (components.Count == 1)
                return new DeviceGrayColor(components);
            else if (components.Count == 3)
                return new DeviceRGBColor(components); // FIXME:temporary hack...
            else if (components.Count == 4)
                return new DeviceCMYKColor(components);
            return null;
        }

        public override SKColor GetColor(Color color)
        {
            GetIccProfile();
            GetSKColorSpace();

            // FIXME: temporary hack
            if (color is DeviceRGBColor devRGB)
            {
                var point = Power(xyzD50, (float)devRGB.R, (float)devRGB.G, (float)devRGB.B);
                return new SKColor(
                   (byte)(point.X * 255),
                   (byte)(point.Y * 255),
                   (byte)(point.Z * 255));
            }
            else if (color is DeviceGrayColor devGray)
            {
                var point = Power(xyzD50, (float)devGray.G, (float)devGray.G, (float)devGray.G);
                return new SKColor(
                    (byte)(point.X * 255),
                    (byte)(point.Y * 255),
                    (byte)(point.Z * 255));
            }

            return SKColors.Black;
        }

        

        public PdfStream Profile
        {
            get { return (PdfStream)((PdfArray)BaseDataObject).Resolve(1); }
        }
        
        public PdfName Alternate
        {
            get => Profile?.Header.Resolve(PdfName.Alternate) as PdfName;
        }

        public int N
        {
            get => ((PdfInteger)Profile?.Header.Resolve(PdfName.N))?.RawValue ?? 0;
        }


        public SKColorSpace GetSKColorSpace()
        {
            if (skColorSpace == null)
            {
                skColorSpace = SKColorSpace.CreateIcc(Profile.GetBody(true).ToByteArray());
                xyzD50 = skColorSpace.ToXyzD50();
            }
            return skColorSpace;
        }

        private void GetIccProfile()
        {
            if (iccProfile == null)
            {
                iccProfile = ICCProfile.Load(Profile.GetBody(true).ToByteArray());
            }
        }

        private SKPoint3 Power(SKMatrix44 matrix, float x, float y, float z)
        {
            var array = new[] { x, y, z };//matrix.MapScalars(x, y, z, 1);//
            return new SKPoint3
            {
                X = array[0],
                Y = array[1],
                Z = array[2]
            };
        }

        #endregion
        #endregion
        #endregion
    }



}