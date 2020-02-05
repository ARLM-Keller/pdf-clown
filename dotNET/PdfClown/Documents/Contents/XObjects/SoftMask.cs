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

using PdfClown.Documents.Functions;
using PdfClown.Objects;
using System;

namespace PdfClown.Documents.Contents.XObjects
{
    [PDF(VersionEnum.PDF14)]
    public class SoftMask : GroupXObject
    {
        public static SoftMask WrapSoftMask(PdfDirectObject baseObject)
        {
            if (baseObject is PdfName name && name.Equals(PdfName.None))
                return null;
            return Wrap<SoftMask>(baseObject);
        }

        public SoftMask(Document context, PdfDictionary baseDataObject) : base(context, baseDataObject)
        {
            Type = PdfName.Mask;
        }

        public SoftMask(PdfDirectObject baseObject) : base(baseObject)
        {

        }

        public FormXObject Group
        {
            get => FormXObject.Wrap(BaseDataObject[PdfName.G]);
            set => BaseDataObject[PdfName.G] = value?.BaseObject;
        }

        public PdfArray BackColor
        {
            get => (PdfArray)BaseDataObject.Resolve(PdfName.BC);
            set => BaseDataObject[PdfName.BC] = value;
        }

        public Function Function
        {
            get => Function.Wrap(BaseDataObject[PdfName.TR]);
            set => BaseDataObject[PdfName.TR] = value.BaseObject;
        }


    }
}