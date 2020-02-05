/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)
    * Alexandr

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

namespace PdfClown.Documents.Contents.XObjects
{
    public class GroupXObject : PdfObjectWrapper<PdfDictionary>
    {
        public static GroupXObject Wrap(PdfDirectObject baseObject)
        {
            if (baseObject == null)
                return null;
            if (baseObject.Wrapper is GroupXObject groupObject)
                return groupObject;
            if (baseObject is PdfReference pdfReference && pdfReference.DataObject?.Wrapper is GroupXObject referenceGroupObject)
            {
                baseObject.Wrapper = referenceGroupObject;
                return referenceGroupObject;
            }
            var subtype = (PdfName)((PdfDictionary)baseObject)[PdfName.S];
            if (subtype.Equals(PdfName.Transparency))
            {
                return new TransparencyXObject(baseObject);
            }
            else
            {
                return new GroupXObject(baseObject);
            }
        }

        public GroupXObject(Document context, PdfDictionary baseDataObject)
            : base(context, baseDataObject)
        {
            Type = PdfName.Group;
        }

        public GroupXObject(PdfDirectObject baseObject)
            : base(baseObject)
        { }

        public PdfName Type
        {
            get => (PdfName)BaseDataObject[PdfName.Type];
            set => BaseDataObject[PdfName.Type] = value;
        }

        public PdfName SubType
        {
            get => (PdfName)BaseDataObject[PdfName.S];
            set => BaseDataObject[PdfName.S] = value;
        }
    }
}