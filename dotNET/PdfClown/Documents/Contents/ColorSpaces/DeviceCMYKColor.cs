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

using PdfClown.Objects;

using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>Device Cyan-Magenta-Yellow-Key color value [PDF:1.6:4.5.3].</summary>
      <remarks>The 'Key' component is renamed 'Black' to avoid semantic
      ambiguities.</remarks>
    */
    [PDF(VersionEnum.PDF11)]
    public sealed class DeviceCMYKColor : DeviceColor, IEquatable<DeviceCMYKColor>
    {
        #region static
        #region fields
        public static readonly DeviceCMYKColor Black = new DeviceCMYKColor(0, 0, 0, 1);
        public static readonly DeviceCMYKColor White = new DeviceCMYKColor(0, 0, 0, 0);

        public static readonly DeviceCMYKColor Default = Black;
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the color corresponding to the specified components.</summary>
          <param name="components">Color components to convert.</param>
        */
        public static new DeviceCMYKColor Get(PdfArray components)
        {
            return components != null
                ? components.Wrapper is DeviceCMYKColor color ? color : new DeviceCMYKColor(components)
                : Default;
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public DeviceCMYKColor(double c, double m, double y, double k) : this(
            new PdfArray(
                  PdfReal.Get(NormalizeComponent(c)),
                  PdfReal.Get(NormalizeComponent(m)),
                  PdfReal.Get(NormalizeComponent(y)),
                  PdfReal.Get(NormalizeComponent(k))))
        { }

        internal DeviceCMYKColor(IList<PdfDirectObject> components)
            : base(DeviceCMYKColorSpace.Default, components is PdfArray pdfArray ? pdfArray : new PdfArray(components))
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the cyan component.</summary>
        */
        public float C
        {
            get => GetComponentValue(0);
            set => SetComponentValue(0, value);
        }

        /**
          <summary>Gets/Sets the magenta component.</summary>
        */
        public float M
        {
            get => GetComponentValue(1);
            set => SetComponentValue(1, value);
        }

        /**
          <summary>Gets/Sets the yellow component.</summary>
        */
        public float Y
        {
            get => GetComponentValue(2);
            set => SetComponentValue(2, value);
        }

        /**
          <summary>Gets/Sets the black (key) component.</summary>
        */
        public float K
        {
            get => GetComponentValue(3);
            set => SetComponentValue(3, value);
        }

        public override bool IsZero => Equals(White);

        public override object Clone(Document context)
        {
            throw new NotImplementedException();
        }

        public bool Equals(DeviceCMYKColor other)
        {
            if (other == null)
                return false;
            return C.Equals(other.C)
                && M.Equals(other.M)
                && Y.Equals(other.Y)
                && K.Equals(other.K);
        }

        #endregion
        #endregion
        #endregion
    }
}