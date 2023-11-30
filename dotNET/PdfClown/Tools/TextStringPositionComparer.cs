/*
  Copyright 2009-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Contents;

using System;
using System.Collections.Generic;
using PdfClown.Util.Math.Geom;

namespace PdfClown.Tools
{
    /**
      <summary>Text string position comparer.</summary>
    */
    public class TextStringPositionComparer<T> : IComparer<T>
      where T : ITextString
    {
        /**
          <summary>Gets whether the specified boxes lay on the same text line.</summary>
        */
        public static bool IsOnTheSameLine(Quad box1, Quad box2)
        {
            /*
              NOTE: In order to consider the two boxes being on the same line,
              we apply a simple rule of thumb: at least 25% of a box's height MUST
              lay on the horizontal projection of the other one.
            */
            double minHeight = Math.Min(box1.Height, box2.Height);
            double yThreshold = minHeight * .75;
            return ((box1.Top > box2.Top - yThreshold
                && box1.Top < box2.Bottom + yThreshold - minHeight)
              || (box2.Top > box1.Top - yThreshold
                && box2.Top < box1.Bottom + yThreshold - minHeight));
        }

        public int Compare(T textString1, T textString2)
        {
            var quad1 = textString1.Quad;
            var quad2 = textString2.Quad;
            if (IsOnTheSameLine(quad1, quad2))
            {
                /*
                  [FIX:55:0.1.3] In order not to violate the transitive condition, equivalence on x-axis
                  MUST fall back on y-axis comparison.
                */
                int xCompare = quad1.Left.CompareTo(quad2.Left);
                if (xCompare != 0)
                    return xCompare;
            }
            return quad1.Top.CompareTo(quad2.Top);
        }
    }
}
