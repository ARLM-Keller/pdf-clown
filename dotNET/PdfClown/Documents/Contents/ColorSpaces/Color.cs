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

using PdfClown.Objects;

using System;
using System.Collections.Generic;
using System.Drawing;

namespace PdfClown.Documents.Contents.ColorSpaces
{
    /**
      <summary>Color value [PDF:1.6:4.5.1].</summary>
    */
    public abstract class Color : PdfObjectWrapper<PdfDataObject>
    {
        /**
          <summary>Gets the normalized value of a color component [PDF:1.6:4.5.1].</summary>
          <param name="value">Color component value to normalize.</param>
          <returns>Normalized color component value.</returns>
        */
        /*
          NOTE: Further developments may result in a color-space family-specific
          implementation of this method; currently this implementation focuses on
          device colors only.
        */
        protected static double NormalizeComponent(double value)
        {
            if (value < 0)
                return 0;
            else if (value > 1)
                return 1;
            else
                return value;
        }

        private ColorSpace colorSpace;

        //TODO:verify whether to remove the colorSpace argument (should be agnostic?)!
        protected Color(ColorSpace colorSpace, PdfDirectObject baseObject) : base(baseObject)
        {
            this.colorSpace = colorSpace;
        }

        public Color(PdfDirectObject baseObject) : base(baseObject)
        { }

        //TODO:remove? - one dependency
        public ColorSpace ColorSpace => colorSpace;

        /**
          <summary>Gets the components defining this color value.</summary>
        */
        public abstract IList<PdfDirectObject> Components
        {
            get;
        }

        public void CopyTo(Span<float> to)
        {
            var components = Components;
            for (int i = 0; i < components.Count; i++)
            {
                to[i] = ((IPdfNumber)components[i]).FloatValue;
            }
        }

        public Span<float> AsSpan()
        {
            Span<float> components = new float[Components.Count];
            CopyTo(components);
            return components;
        }

    }
}