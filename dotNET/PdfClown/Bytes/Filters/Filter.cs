/*
  Copyright 2006-2010 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown;
using PdfClown.Objects;

using System;
using System.Collections.Generic;

namespace PdfClown.Bytes.Filters
{
    /**
      <summary>Abstract filter [PDF:1.6:3.3].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public abstract class Filter
    {
        #region static
        #region fields
        private static readonly Filter ASCII85Filter = new ASCII85Filter();
        private static readonly Filter ASCIIHexFilter = new ASCIIHexFilter();
        private static readonly Filter FlateDecode = new FlateFilter();
        private static readonly Filter CCITTFaxDecode = new CCITTFaxFilter();
        private static readonly Filter JBIG2Decode = new JBIG2Filter();
        private static readonly Filter JPXDecode = new JPXFilter();
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets a specific filter object.</summary>
          <param name="name">Name of the requested filter.</param>
          <returns>Filter object associated to the name.</returns>
        */
        public static Filter Get(PdfName name)
        {
            /*
              NOTE: This is a factory singleton method for any filter-derived object.
            */
            if (name == null)
                return null;

            if (name.Equals(PdfName.FlateDecode)
              || name.Equals(PdfName.Fl))
                return FlateDecode;
            else if (name.Equals(PdfName.LZWDecode)
              || name.Equals(PdfName.LZW))
                throw new NotImplementedException("LZWDecode");
            else if (name.Equals(PdfName.ASCIIHexDecode)
              || name.Equals(PdfName.AHx))
                return ASCIIHexFilter;
            else if (name.Equals(PdfName.ASCII85Decode)
              || name.Equals(PdfName.A85))
                return ASCII85Filter;
            else if (name.Equals(PdfName.RunLengthDecode)
              || name.Equals(PdfName.RL))
                throw new NotImplementedException("RunLengthDecode");
            else if (name.Equals(PdfName.CCITTFaxDecode)
              || name.Equals(PdfName.CCF))
                return CCITTFaxDecode;
            else if (name.Equals(PdfName.JBIG2Decode))
                return JBIG2Decode;
            else if (name.Equals(PdfName.DCTDecode)
              || name.Equals(PdfName.DCT))
                throw new NotImplementedException("DCTDecode");
            else if (name.Equals(PdfName.JPXDecode))
                return JPXDecode;
            else if (name.Equals(PdfName.Crypt))
                throw new NotImplementedException("Crypt");

            return null;
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        protected Filter()
        { }
        #endregion

        #region interface
        #region public
        public abstract byte[] Decode(byte[] data, int offset, int length, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header);

        public abstract byte[] Encode(byte[] data, int offset, int length, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header);
        #endregion
        #endregion
        #endregion
    }
}