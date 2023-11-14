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
using PdfClown.Util.Math;

using System;
using System.Linq;
using System.Collections.Generic;

namespace PdfClown.Documents.Functions
{
    /**
      <summary>Sampled function using a sequence of sample values to provide an approximation for
      functions whose domains and ranges are bounded [PDF:1.6:3.9.1].</summary>
      <remarks>The samples are organized as an m-dimensional table in which each entry has n components.
      </remarks>
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class Type0Function : Function
    {
        private IList<Interval<float>> decodes;
        private IList<Interval<int>> encodes;
        private List<int> sampleCounts;
        private float[] samples;
        public enum InterpolationOrderEnum
        {
            /**
              Linear spline interpolation.
            */
            Linear = 1,
            /**
              Cubic spline interpolation.
            */
            Cubic = 3
        }

        //TODO:implement function creation!

        internal Type0Function(PdfDirectObject baseObject) : base(baseObject)
        { }

        public override float[] Calculate(Span<float> inputs)
        {
            //Mozilla Pdf.js
            var domains = Domains;
            var ranges = Ranges;
            var decodes = Decodes ?? ranges;
            var encodes = Encodes;
            var sampleCount = SampleCounts;
            var samples = GetSamples();
            var sampleMax = (int)Math.Pow(2, BitsPerSample) - 1;
            var n = ranges.Count;
            var m = domains.Count;
            var k = n;
            var pos = 1;
            var cubeVertices = 1 << m;
            var cubeN = new float[cubeVertices];
            var cubeVertex = new int[cubeVertices];

            // Building the cube vertices: its part and sample index
            // http://rjwagner49.com/Mathematics/Interpolation.pdf
            for (var j = 0; j < cubeVertices; j++)
            {
                cubeN[j] = 1;
            }

            for (int i = 0; i < m; i++)
            {
                var domain = domains[i];
                var encode = encodes[i];
                var size = sampleCount[i];
                var x = inputs[i];
                x = Math.Min(Math.Max(x, domain.Low), domain.High);
                var e = Linear(x, domain.Low, domain.High, encode.Low, encode.High);
                e = Math.Min(Math.Max(e, 0), size - 1);
                var eiMax = (int)Math.Floor(e);
                var eiMin = (int)Math.Ceiling(e);


                // Adjusting the cube: N and vertex sample index
                var e0 = e < size - 1 ? eiMax : e - 1; // e1 = e0 + 1;
                var n0 = e0 + 1 - e; // (e1 - e) / (e1 - e0);
                var n1 = e - e0; // (e - e0) / (e1 - e0);
                var offset0 = e0 * k;
                var offset1 = offset0 + k; // e1 * k
                for (var j = 0; j < cubeVertices; j++)
                {
                    if ((j & pos) != 0)
                    {
                        cubeN[j] *= n1;
                        cubeVertex[j] += (int)offset1;
                    }
                    else
                    {
                        cubeN[j] *= n0;
                        cubeVertex[j] += (int)offset0;
                    }
                }
                k *= size;
                pos <<= 1;
            }

            var result = new float[n];
            for (int j = 0; j < n; j++)
            {
                var range = ranges[j];
                var decode = decodes[j];

                var r = 0F;

                for (int i = 0; i < cubeVertices; i++)
                {
                    r += samples[cubeVertex[i] + j] * cubeN[i];
                }

                r = Linear(r, 0, 1, decode.Low, decode.High);
                result[j] = Math.Min(Math.Max(r, range.Low), range.High);
            }

            return result;
        }

        /**
          <summary>Gets the linear mapping of input values into the domain of the function's sample table.</summary>
        */
        public IList<Interval<int>> Encodes => encodes ??= GetIntervals<int>(
                  PdfName.Encode,
                  delegate (IList<Interval<int>> intervals)
                  {
                      foreach (int sampleCount in SampleCounts)
                      { intervals.Add(new Interval<int>(0, sampleCount - 1)); }
                      return intervals;
                  });

        /**
          <summary>Gets the order of interpolation between samples.</summary>
        */
        public InterpolationOrderEnum Order => (InterpolationOrderEnum)Dictionary.GetInt(PdfName.Order, 1);

        /**
          <summary>Gets the linear mapping of sample values into the ranges of the function's output values.</summary>
        */
        public IList<Interval<float>> Decodes => decodes ?? (decodes = GetIntervals<float>(PdfName.Decode, null));

        /**
          <summary>Gets the number of bits used to represent each sample.</summary>
        */
        public int BitsPerSample => Dictionary.GetInt(PdfName.BitsPerSample);

        /**
          <summary>Gets the number of samples in each input dimension of the sample table.</summary>
        */
        public IList<int> SampleCounts
        {
            get
            {
                if (sampleCounts == null)
                {
                    sampleCounts = new List<int>();
                    PdfArray sampleCountsObject = (PdfArray)Dictionary[PdfName.Size];
                    foreach (PdfDirectObject sampleCountObject in sampleCountsObject)
                    { sampleCounts.Add(((PdfInteger)sampleCountObject).RawValue); }
                }
                return sampleCounts;
            }
        }
        public float[] GetSamples()
        {
            if (samples == null)
            {
                var ranges = Ranges;
                var domains = Domains;
                var size = SampleCounts;
                var outputSize = ranges.Count;
                var bps = BitsPerSample;
                var bytes = bps / 8;
                var stream = BaseDataObject as PdfStream;
                using (var buffer = stream.ExtractBody(true) as Bytes.ByteStream)
                {
                    var length = 1;
                    for (int i = 0, ii = size.Count; i < ii; i++)
                    {
                        length *= size[i];
                    }
                    length *= outputSize;

                    var array = new float[length];
                    var codeSize = 0;
                    var codeBuf = 0;
                    // 32 is a valid bps so shifting won't work
                    var sampleMul = 1.0 / (Math.Pow(2.0, bps) - 1);

                    var strBytes = buffer.ReadSpan((length * bps + 7) / 8);
                    var strIdx = 0;
                    for (int i = 0; i < length; i++)
                    {
                        while (codeSize < bps)
                        {
                            codeBuf <<= 8;
                            codeBuf |= strBytes[strIdx++];
                            codeSize += 8;
                        }
                        codeSize -= bps;
                        array[i] = (float)((codeBuf >> codeSize) * sampleMul);
                        codeBuf &= (1 << codeSize) - 1;
                    }
                    samples = array;
                }
            }
            return samples;
        }
    }
}