/*
  Copyright 2007-2011 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Bytes;
using colorSpaces = PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Objects;

using System.Collections.Generic;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>Shading object [PDF:1.6:4.6.3].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class Shading : GraphicsObject, IResourceReference<colorSpaces::Shading>
    {
        #region static
        #region fields
        public static readonly string BeginOperatorKeyword = PaintShading.OperatorKeyword;
        public static readonly string EndOperatorKeyword = BeginOperatorKeyword;
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public Shading(PaintShading operation) : base(operation)
        { }
        #endregion

        #region interface
        #region public
        #region IResourceReference
        public colorSpaces::Shading GetResource(IContentContext context)
        { return Operation.GetResource(context); }

        public PdfName Name
        {
            get => Operation.Name;
            set => Operation.Name = value;
        }

        public override void Scan(ContentScanner.GraphicsState state)
        {
            var shading = GetResource(state.Scanner.ContentContext);
            base.Scan(state);
        }
        #endregion
        #endregion

        #region private
        private PaintShading Operation => (PaintShading)Objects[0];
        #endregion
        #endregion
        #endregion
    }
}