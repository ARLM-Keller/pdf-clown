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

using PdfClown.Objects;

using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Functions
{
    /**
      <summary>Exponential interpolation of one input value and <code>n</code> output values
      [PDF:1.6:3.9.2].</summary>
      <remarks>Each input value <code>x</code> will return <code>n</code> values, given by <code>
      y[j] = C0[j] + x^N × (C1[j] − C0[j])</code>, for <code>0 ≤ j < n</code>, where <code>C0</code>
      and <code>C1</code> are the {@link #getBoundOutputValues() function results} when, respectively,
      <code>x = 0</code> and <code>x = 1</code>, and <code>N</code> is the {@link #getExponent()
      interpolation exponent}.</remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class Type2Function : Function
    {
        private float[] _c0;
        private float[] _c1;
        private List<float[]> outputBounds;
        #region dynamic
        #region constructors
        //TODO:implement function creation!

        internal Type2Function(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public override float[] Calculate(float[] inputs)
        {
            var n = Exponent;
            var c0 = C0;
            var c1 = C1;
            var domains = Domains;
            var ranges = Ranges;
            for (int i = 0; i < domains.Count; i++)
            {
                var domain = domains[i];
                inputs[i] = Math.Min(Math.Max(inputs[i], domain.Low), domain.High);
            }
            var outCount = ranges?.Count ?? c0.Length;
            var result = new float[outCount];
            var x = inputs[0];
            var inputN = (float)Math.Pow(x, Exponent);
            for (int i = 0; i < outCount; i++)
            {
                var range = ranges?[i] ?? null;
                var exponenta = n == 1
                    ? Linear(x, domains[0].Low, domains[0].High, c0[i], c1[i])
                    : Exponential(x, c0[i], c1[i], inputN);
                result[i] = Math.Min(Math.Max(exponenta, range?.Low ?? 0F), range?.High ?? 1F);
            }
            return result;// new float[] { inputs[0], inputs[0], inputs[0], inputs[0] };
        }

        /**
          <summary>Gets the output value pairs <code>(C0,C1)</code> for lower (<code>0.0</code>)
          and higher (<code>1.0</code>) input values.</summary>
        */
        public IList<float[]> BoundOutputValues
        {
            get
            {
                if (outputBounds == null)
                {
                    PdfArray lowOutputBoundsObject = (PdfArray)Dictionary[PdfName.C0];
                    PdfArray highOutputBoundsObject = (PdfArray)Dictionary[PdfName.C1];
                    if (lowOutputBoundsObject == null)
                    {
                        outputBounds = new List<float[]>();
                        outputBounds.Add(new float[] { 0, 1 });
                    }
                    else
                    {
                        outputBounds = new List<float[]>();
                        IEnumerator<PdfDirectObject> lowOutputBoundsObjectIterator = lowOutputBoundsObject.GetEnumerator();
                        IEnumerator<PdfDirectObject> highOutputBoundsObjectIterator = highOutputBoundsObject.GetEnumerator();
                        while (lowOutputBoundsObjectIterator.MoveNext()
                          && highOutputBoundsObjectIterator.MoveNext())
                        {
                            outputBounds.Add(
                              new float[]
                              {
                                  ((IPdfNumber)lowOutputBoundsObjectIterator.Current).FloatValue,
                                  ((IPdfNumber)highOutputBoundsObjectIterator.Current).FloatValue
                              }
                              );
                        }
                    }
                }
                return outputBounds;
            }
        }

        /**
          <summary>Gets the interpolation exponent.</summary>
        */
        public float Exponent => ((IPdfNumber)Dictionary[PdfName.N]).FloatValue;

        public float[] C0
        {
            get
            {
                if (_c0 == null)
                {
                    var c0 = (PdfArray)Dictionary[PdfName.C0];

                    if (c0 == null)
                    {
                        _c0 = new float[] { 0, 0 };
                    }
                    else
                    {
                        _c0 = new float[c0.Count];
                        for (int index = 0, length = c0.Count; index < length; index++)
                        { _c0[index] = ((IPdfNumber)c0[index]).FloatValue; }
                    }
                }
                return _c0;
            }
        }

        public float[] C1
        {
            get
            {
                if (_c1 == null)
                {
                    var c1 = (PdfArray)Dictionary[PdfName.C1];

                    if (c1 == null)
                    {
                        _c1 = new float[] { 1, 0 };
                    }
                    else
                    {
                        _c1 = new float[c1.Count];
                        for (int index = 0, length = c1.Count; index < length; index++)
                        { _c1[index] = ((IPdfNumber)c1[index]).FloatValue; }
                    }
                }
                return _c1;
            }
        }


        #endregion
        #endregion
        #endregion
    }
}