/*
  Copyright 2008-2012 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Interaction.Actions;
using PdfClown.Files;
using PdfClown.Objects;

using system = System;
using System.Collections.Generic;

namespace PdfClown.Documents
{
    /**
      <summary>Page actions [PDF:1.6:8.5.2].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class PageActions : PdfObjectWrapper<PdfDictionary>
    {
        public PageActions(Document context) : base(context, new PdfDictionary())
        { }

        public PageActions(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets/Sets the action to be performed when the page is closed.</summary>
        */
        public Action OnClose
        {
            get => Action.Wrap(BaseDataObject[PdfName.C]);
            set => BaseDataObject[PdfName.C] = value.BaseObject;
        }

        /**
          <summary>Gets/Sets the action to be performed when the page is opened.</summary>
        */
        public Action OnOpen
        {
            get => Action.Wrap(BaseDataObject[PdfName.O]);
            set => BaseDataObject[PdfName.O] = value.BaseObject;
        }
    }
}