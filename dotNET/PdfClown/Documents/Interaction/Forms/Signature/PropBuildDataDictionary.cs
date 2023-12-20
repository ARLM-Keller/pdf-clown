/*
  Copyright 2008-2015 Stefano Chizzolini. http://www.pdfclown.org

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

namespace PdfClown.Documents.Interaction.Forms.Signature
{
    public class PropBuildDataDictionary : PdfObjectWrapper<PdfDictionary>
    {
        public PropBuildDataDictionary(Document doc)
            : base(doc, new PdfDictionary())
        { }

        public PropBuildDataDictionary(PdfDirectObject obj)
            : base(obj)
        { }

        public string Name
        {
            get => BaseDataObject.GetString(PdfName.Name);
            set => BaseDataObject.SetName(PdfName.Name, value);
        }

        public DateTime? Date
        {
            get => BaseDataObject.GetDate(PdfName.Date);
            set => BaseDataObject.SetDate(PdfName.Date, value);
        }

        public string Version
        {
            get => BaseDataObject.GetString(PdfName.REx);
            set => BaseDataObject.SetName(PdfName.REx, value);
        }

        public int Revision
        {
            get => BaseDataObject.GetInt(PdfName.R);
            set => BaseDataObject.SetInt(PdfName.R, value);
        }

        public bool PrePelease
        {
            get => BaseDataObject.GetBool(PdfName.PreRelease);
            set => BaseDataObject.SetBool(PdfName.PreRelease, value);
        }

        public string OS
        {
            get => BaseDataObject.Resolve(PdfName.REx) is PdfArray array 
                ? array.GetString(0) 
                : BaseDataObject.Resolve(PdfName.REx) is PdfString pdfString 
                    ? pdfString.StringValue 
                    : null;
            set
            {
                var array = BaseDataObject.Resolve<PdfArray>(PdfName.REx);
                array.SetName(0, value);
            }
        }

        public bool NonEFontNoWarn
        {
            get => BaseDataObject.GetBool(PdfName.NonEFontNoWarn, true);
            set => BaseDataObject.SetBool(PdfName.NonEFontNoWarn, value);
        }

        public bool TrustedMode
        {
            get => BaseDataObject.GetBool(PdfName.TrueType, false);
            set => BaseDataObject.SetBool(PdfName.TrustedMode, value);
        }
    }
}