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

using PdfClown.Documents;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>Device Red-Green-Blue color space [PDF:1.6:4.5.3].</summary>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class DeviceRGBColorSpace : DeviceColorSpace
    {
        /*
          NOTE: It may be specified directly (i.e. without being defined in the ColorSpace subdictionary
          of the contextual resource dictionary) [PDF:1.6:4.5.7].
        */
        public static readonly DeviceRGBColorSpace Default = new DeviceRGBColorSpace(PdfName.DeviceRGB);

        public DeviceRGBColorSpace(Document context) : base(context, PdfName.DeviceRGB)
        { }

        internal DeviceRGBColorSpace(PdfDirectObject baseObject) : base(baseObject)
        { }

        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 3;

        public override Color DefaultColor => DeviceRGBColor.Default;

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        { return new DeviceRGBColor(components); }

        public override bool IsSpaceColor(Color color)
        { return color is DeviceRGBColor; }

        public override SKColor GetSKColor(Color color, float? alpha = null)
        {
            DeviceRGBColor spaceColor = (DeviceRGBColor)color;
            var skColor = new SKColor(
               (byte)Math.Round(spaceColor.R * 255),
               (byte)Math.Round(spaceColor.G * 255),
               (byte)Math.Round(spaceColor.B * 255));
            if (alpha != null)
            {
                skColor = skColor.WithAlpha((byte)(alpha.Value * 255));
            }
            return skColor;
        }

        public override SKColor GetSKColor(Span<float> components, float? alpha = null)
        {
            var skColor = new SKColor(
                 (byte)Math.Round(components[0] * 255),
                 (byte)Math.Round(components[1] * 255),
                 (byte)Math.Round(components[2] * 255));
            if (alpha != null)
            {
                skColor = skColor.WithAlpha((byte)(alpha.Value * 255));
            }
            return skColor;
        }
    }
}