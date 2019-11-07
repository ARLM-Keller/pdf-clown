/*
  Copyright 2010-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Util;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>Indexed color space [PDF:1.6:4.5.5].</summary>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class IndexedColorSpace : SpecialColorSpace
    {
        #region dynamic
        #region fields
        private IDictionary<int, Color> baseColors = new Dictionary<int, Color>();
        private byte[] baseComponentValues;
        private ColorSpace baseSpace;
        #endregion

        #region constructors
        //TODO:IMPL new element constructor!

        internal IndexedColorSpace(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the base color space in which the values in the color table
          are to be interpreted.</summary>
        */
        public ColorSpace BaseSpace
        {
            get
            {
                if (baseSpace == null)
                { baseSpace = ColorSpace.Wrap(((PdfArray)BaseDataObject)[1]); }
                return baseSpace;
            }
        }

        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override int ComponentCount => 1;

        public override Color DefaultColor => IndexedColor.Default;

        /**
          <summary>Gets the color corresponding to the specified table index resolved according to
          the <see cref="BaseSpace">base space</see>.<summary>
        */
        public Color GetBaseColor(IndexedColor color)
        {
            int colorIndex = color.Index;
            if (!baseColors.TryGetValue(colorIndex, out var baseColor))
            {
                ColorSpace baseSpace = BaseSpace;
                IList<PdfDirectObject> components = new List<PdfDirectObject>();
                {
                    int componentCount = baseSpace.ComponentCount;
                    int componentValueIndex = colorIndex * componentCount;
                    byte[] baseComponentValues = BaseComponentValues;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        var byteValue = componentValueIndex < baseComponentValues.Length
                            ? baseComponentValues[componentValueIndex]
                            : 0;
                        var value = ((int)byteValue & 0xff) / 255d;
                        components.Add(PdfReal.Get(value));
                        componentValueIndex++;
                    }
                }
                baseColor = baseColors[colorIndex] = baseSpace.GetColor(components, null);
            }
            return baseColor;
        }

        public override Color GetColor(IList<PdfDirectObject> components, IContentContext context)
        { return new IndexedColor(components); }

        public override SKColor GetColor(Color color)
        {
            return BaseSpace.GetColor(GetBaseColor((IndexedColor)color));
        }

        public override SKPaint GetPaint(Color color, double? alpha)
        {
            return BaseSpace.GetPaint(GetBaseColor((IndexedColor)color), alpha);
        }
        #endregion

        #region private
        /**
          <summary>Gets the color table.</summary>
        */
        private byte[] BaseComponentValues
        {
            get
            {
                if (baseComponentValues == null)
                {
                    var value = ((PdfArray)BaseDataObject).Resolve(3);
                    if (value is IDataWrapper wrapper)
                    {
                        baseComponentValues = wrapper.ToByteArray();
                    }
                    else if (value is PdfStream stream)
                    {
                        baseComponentValues = stream.GetBody(false).ToByteArray();
                    }
                }
                return baseComponentValues;
            }
        }
        #endregion
        #endregion
        #endregion
    }
}