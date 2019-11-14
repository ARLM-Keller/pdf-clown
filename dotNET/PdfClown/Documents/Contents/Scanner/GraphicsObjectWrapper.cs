/*
  Copyright 2007-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Contents.Objects;
using SkiaSharp;

namespace PdfClown.Documents.Contents.Scanner
{

    public abstract class GraphicsObjectWrapper
    {
        #region static
        internal static GraphicsObjectWrapper Get(ContentScanner scanner)
        {
            var obj = scanner.Current;
            if (obj is ShowText)
                return new TextStringWrapper(scanner);
            else if (obj is Text)
                return new TextWrapper(scanner);
            else if (obj is XObject)
                return new XObjectWrapper(scanner);
            else if (obj is InlineImage)
                return new InlineImageWrapper(scanner);
            else
                return null;
        }
        #endregion

        #region dynamic
        #region fields
        protected SKRect? box;
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the object's bounding box.</summary>
        */
        public virtual SKRect? Box => box;
        #endregion
        #endregion
        #endregion
    }
}