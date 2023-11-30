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

using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Files;
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents.Patterns
{
    /**
      <summary>Paint that consists of a repeating graphical figure or a smoothly varying color gradient
      instead of a simple color [PDF:1.6:4.6].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public abstract class Pattern : Color
    {
        //TODO:verify!
        public static readonly Pattern Default = new TilingPattern(null);
        private const int PatternType1 = 1;
        private const int PatternType2 = 2;
        private float[] matrix;

        /**
          <summary>Wraps the specified base object into a pattern object.</summary>
          <param name="baseObject">Base object of a pattern object.</param>
          <returns>Pattern object corresponding to the base object.</returns>
        */
        public static Pattern Wrap(PdfDirectObject baseObject)
        {
            if (baseObject == null)
                return null;
            if (baseObject.Wrapper is Pattern pattern)
                return pattern;
            if (baseObject is PdfReference reference && reference.DataObject?.Wrapper is Pattern referencePattern)
            {
                baseObject.Wrapper = referencePattern;
                return referencePattern;
            }
            var dataObject = baseObject.Resolve();
            var dictionary = TryGetDictionary(dataObject);
            int patternType = dictionary.GetInt(PdfName.PatternType);
            switch (patternType)
            {
                case PatternType1:
                    return new TilingPattern(baseObject);
                case PatternType2:
                    return new ShadingPattern(baseObject);
                default:
                    throw new NotSupportedException("Pattern type " + patternType + " unknown.");
            }
        }

        //TODO:verify (colorspace is available or may be implicit?)
        protected Pattern(PdfDirectObject baseObject) : base(baseObject)
        { }

        //TODO:verify (colorspace is available or may be implicit?)
        protected Pattern(PatternColorSpace colorSpace, PdfDirectObject baseObject) : base(colorSpace, baseObject)
        { }

        public override object Clone(Document context)
        { throw new NotImplementedException(); }

        public override IList<PdfDirectObject> Components => new List<PdfDirectObject>();

        /**
          <summary>Gets the pattern matrix, a transformation matrix that maps the pattern's
          internal coordinate system to the default coordinate system of the pattern's
          parent content stream (the content stream in which the pattern is defined as a resource).</summary>
          <remarks>The concatenation of the pattern matrix with that of the parent content stream establishes
          the pattern coordinate space, within which all graphics objects in the pattern are interpreted.</remarks>
        */
        public float[] Matrix
        {
            get
            {
                /*
                  NOTE: Pattern-space-to-user-space matrix is identity [1 0 0 1 0 0] by default.
                */
                return matrix ??= Dictionary.Resolve(PdfName.Matrix) is PdfArray array
                   ? new float[]
                     {
                          array.GetFloat(0), // a.
                          array.GetFloat(1), // b.
                          array.GetFloat(2), // c.
                          array.GetFloat(3), // d.
                          array.GetFloat(4), // e.
                          array.GetFloat(5) // f.
                     }
                   : new float[]
                     {
                          1, // a.
                          0, // b.
                          0, // c.
                          1, // d.
                          0, // e.
                          0 // f.
                     };
            }
        }

        public SKMatrix SKMatrix
        {
            get
            {
                /*
                  NOTE: Form-space-to-user-space matrix is identity [1 0 0 1 0 0] by default,
                  but may be adjusted by setting the matrix entry in the form dictionary [PDF:1.6:4.9].
                */
                return new SKMatrix
                {
                    ScaleX = Matrix[0],
                    SkewY = Matrix[1],
                    SkewX = Matrix[2],
                    ScaleY = Matrix[3],
                    TransX = Matrix[4],
                    TransY = Matrix[5],
                    Persp2 = 1
                };
            }
            set => Dictionary[PdfName.Matrix] =
                 new PdfArray(6)
                 {
                    PdfReal.Get(value.ScaleX),
                    PdfReal.Get(value.SkewY),
                    PdfReal.Get(value.SkewX),
                    PdfReal.Get(value.ScaleY),
                    PdfReal.Get(value.TransX),
                    PdfReal.Get(value.TransY)
                };
        }

        /**
          <summary>Gets this pattern's dictionary.</summary>
        */

    }
}