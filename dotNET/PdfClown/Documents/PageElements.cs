/*
  Copyright 2012-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util.Collections.Generic;

using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfClown.Documents
{
    /**
      <summary>Page elements.</summary>
    */
    public abstract class PageElements<TItem> : Array<TItem>
        where TItem : PdfObjectWrapper<PdfDictionary>
    {
        #region dynamic
        #region fields
        private Page page;
        #endregion

        #region constructors
        internal PageElements(PdfDirectObject baseObject, Page page)
            : base(baseObject)
        { this.page = page; }

        internal PageElements(IWrapper<TItem> itemWrapper, PdfDirectObject baseObject, Page page)
            : base(itemWrapper, baseObject)
        { this.page = page; }
        #endregion

        #region interface
        #region public
        public override void Add(TItem item)
        {
            DoAdd(item);
            base.Add(item);
        }

        public override object Clone(Document context)
        { throw new NotSupportedException(); }

        public override void Insert(int index, TItem @object)
        {
            DoAdd(@object);
            base.Insert(index, @object);
        }

        /**
          <summary>Gets the page associated to these elements.</summary>
        */
        public Page Page => page;

        public override void RemoveAt(int index)
        {
            TItem @object = this[index];
            base.RemoveAt(index);
            DoRemove(@object);
        }

        public override bool Remove(TItem item)
        {
            if (!base.Remove(item))
                return false;

            DoRemove((TItem)item);
            return true;
        }
        #endregion

        #region private
        private void DoAdd(TItem item)
        {
            // Link the element to its page!
            item.BaseDataObject[PdfName.P] = page.BaseObject;
        }

        private void DoRemove(TItem item)
        {
            // Unlink the element from its page!
            item.BaseDataObject.Remove(PdfName.P);
        }
        #endregion
        #endregion
        #endregion
    }
}