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
        #region types
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
        #endregion

        #region dynamic
        #region constructors
        //TODO:implement function creation!

        internal Type0Function(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public override float[] Calculate(float[] inputs)
        {
            var domains = Domains;
            var ranges = Ranges;
            var decode = Decodes;
            var encode = Encodes;
            var sampleCount = SampleCounts;
            var samples = GetSamples();
            var sampleMax = (float)Math.Pow(2, BitsPerSample) - 1;
            for (int i = 0; i < domains.Count; i++)
            {
                var domain = domains[i];
                inputs[i] = Math.Min(Math.Max(inputs[i], domain.Low), domain.High);
            }
            var result = new float[ranges.Count * domains.Count];

            for (int d = 0; d < domains.Count; d++)
            {
                var x = inputs[d];

                for (int r = 0; r < ranges.Count; r++)
                {
                    var e = Linear(x, domains[d].Low, domains[d].High, encode[d].Low, encode[d].High);
                    e = Math.Min(Math.Max(e, 0), sampleCount[d]);
                    e = samples[r][d][(int)e];
                    e = Linear(e, 0, sampleMax, decode[r].Low, decode[r].High);
                    result[d * ranges.Count + r] = Math.Min(Math.Max(e, ranges[r].Low), ranges[r].High);
                }
            }
            return result;
        }

        /**
          <summary>Gets the linear mapping of input values into the domain of the function's sample table.</summary>
        */
        public IList<Interval<int>> Encodes => GetIntervals<int>(
                  PdfName.Encode,
                  delegate (IList<Interval<int>> intervals)
                  {
                      foreach (int sampleCount in SampleCounts)
                      { intervals.Add(new Interval<int>(0, sampleCount - 1)); }
                      return intervals;
                  }
                  );

        /**
          <summary>Gets the order of interpolation between samples.</summary>
        */
        public InterpolationOrderEnum Order
        {
            get
            {
                PdfInteger interpolationOrderObject = (PdfInteger)Dictionary[PdfName.Order];
                return (interpolationOrderObject == null
                  ? InterpolationOrderEnum.Linear
                  : (InterpolationOrderEnum)interpolationOrderObject.RawValue);
            }
        }

        /**
          <summary>Gets the linear mapping of sample values into the ranges of the function's output values.</summary>
        */
        public IList<Interval<float>> Decodes => GetIntervals<float>(PdfName.Decode, null);

        /**
          <summary>Gets the number of bits used to represent each sample.</summary>
        */
        public int BitsPerSample => ((PdfInteger)Dictionary[PdfName.BitsPerSample]).RawValue;

        /**
          <summary>Gets the number of samples in each input dimension of the sample table.</summary>
        */
        public IList<int> SampleCounts
        {
            get
            {
                List<int> sampleCounts = new List<int>();
                {
                    PdfArray sampleCountsObject = (PdfArray)Dictionary[PdfName.Size];
                    foreach (PdfDirectObject sampleCountObject in sampleCountsObject)
                    { sampleCounts.Add(((PdfInteger)sampleCountObject).RawValue); }
                }
                return sampleCounts;
            }
        }
        public List<List<List<int>>> GetSamples()
        {
            var ranges = Ranges;
            var domains = Domains;
            var sampleCounts = SampleCounts;
            var samples = new List<List<List<int>>>();
            var bytes = BitsPerSample / 8;
            var stream = BaseDataObject as PdfStream;
            var buffer = stream.GetBody(true) as Bytes.Buffer;
            buffer.Seek(0);
            for (var r = 0; r < ranges.Count; r++)
            {
                var rangeList = new List<List<int>>();
                samples.Add(rangeList);
                for (var d = 0; d < domains.Count; d++)
                {
                    var domainList = new List<int>();
                    rangeList.Add(domainList);
                    for (var s = 0; s < sampleCounts[d]; s++)
                    {
                        domainList.Add(buffer.ReadInt(bytes));
                        //if (buffer.Position == buffer.Length - 1)
                        //    break;
                    }
                }

            }

            return samples;
        }


        #endregion
        #endregion
        #endregion
    }
}