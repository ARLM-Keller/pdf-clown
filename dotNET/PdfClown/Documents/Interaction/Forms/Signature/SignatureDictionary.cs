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

using PdfClown.Bytes;
using PdfClown.Objects;
using PdfClown.Util;
using System;

namespace PdfClown.Documents.Interaction.Forms.Signature
{
    public class SignatureDictionary : PdfObjectWrapper<PdfDictionary>
    {
        private int[] byteRange;

        public SignatureDictionary(Document doc)
            : base(doc, new PdfDictionary {
                { PdfName.Type, PdfName.Sig },
                //{ PdfName.Filter, PdfName.AdobePPKLite }
            })
        { }
        public SignatureDictionary(PdfDirectObject baseObject)
            : base(baseObject)
        {
            if (Type == null)
                BaseDataObject[PdfName.Type] = PdfName.Sig;
        }

        public string Type
        {
            get => BaseDataObject.GetString(PdfName.Type);
            set => BaseDataObject.SetName(PdfName.Type, value);
        }

        public string Filter
        {
            get => BaseDataObject.GetString(PdfName.Filter);
            set => BaseDataObject.SetName(PdfName.Filter, value);
        }

        public string SubFilter
        {
            get => BaseDataObject.GetString(PdfName.SubFilter);
            set => BaseDataObject.SetName(PdfName.SubFilter, value);
        }

        public int[] ByteRange
        {
            get => byteRange ??= BaseDataObject.Resolve<PdfArray>(PdfName.ByteRange)?.ToIntArray();
            set
            {
                byteRange = value;
                BaseDataObject[PdfName.ByteRange] = value != null ? new PdfArray() : null;
            }
        }

        public Memory<byte> Contents
        {
            get => ((PdfString)BaseDataObject.Resolve(PdfName.Contents))?.AsMemory() ?? Memory<byte>.Empty;
            set => BaseDataObject[PdfName.Contents] = value.IsEmpty ? null : new PdfByteString(value);
        }

        public PdfDirectObject Cert
        {
            get => BaseDataObject[PdfName.CenterWindow];
            set => BaseDataObject[PdfName.Cert] = value;
        }

        public DateTime? DateM
        {
            get => BaseDataObject.GetNDate(PdfName.M);
            set => BaseDataObject.SetDate(PdfName.M, value);
        }

        public string Name
        {
            get => BaseDataObject.GetString(PdfName.Name);
            set => BaseDataObject.SetText(PdfName.Name, value);
        }

        public string Location
        {
            get => BaseDataObject.GetString(PdfName.Location);
            set => BaseDataObject.SetText(PdfName.Location, value);
        }

        public string Reason
        {
            get => BaseDataObject.GetString(PdfName.Reason);
            set => BaseDataObject.SetText(PdfName.Reason, value);
        }

        public PdfArray Reference
        {
            get => BaseDataObject.Resolve<PdfArray>(PdfName.Reference);
            set => BaseDataObject[PdfName.Reference] = value;
        }

        public PdfArray Changes
        {
            get => BaseDataObject.Resolve<PdfArray>(PdfName.Changes);
            set => BaseDataObject[PdfName.Changes] = value;
        }

        public PropBuild PropBuild
        {
            get => Wrap<PropBuild>(BaseDataObject.Resolve<PdfDictionary>(PdfName.Prop_Build));
            set => BaseDataObject[PdfName.Prop_Build] = value?.BaseDataObject;
        }


        /**
     * Will return the embedded signature between the byterange gap.
     *
     * @param pdfFile The signed pdf file as InputStream. It will be closed in this method.
     * @return a byte array containing the signature
     * @throws IOException if the pdfFile can't be read
     * @throws IndexOutOfBoundsException if the byterange array is not long enough
     */
        public Memory<byte> GetContents(IInputStream pdfFile)
        {
            var byteRange = ByteRange;
            int begin = byteRange[0] + byteRange[1] + 1;
            int len = byteRange[2] - begin;

            using var input = new ByteStream(pdfFile, begin, len);
            return GetConvertedContents(input);
        }

        /**
     * Will return the embedded signature between the byterange gap.
     *
     * @param pdfFile The signed pdf file as byte array
     * @return a byte array containing the signature
     * @throws IOException if the pdfFile can't be read
     * @throws IndexOutOfBoundsException if the byterange array is not long enough
     */
        public Memory<byte> GetContents(Memory<byte> pdfFile)
        {
            var byteRange = ByteRange;
            int begin = byteRange[0] + byteRange[1] + 1;
            int len = byteRange[2] - begin - 1;

            using var input = new ByteStream(pdfFile.Slice(begin, len));
            return GetConvertedContents(input);
        }


        private Memory<byte> GetConvertedContents(IInputStream input)
        {
            using var output = new ByteStream((int)input.Length / 2);
            Span<byte> buffer = stackalloc byte[2];
            while (input.IsAvailable)
            {
                var b = input.PeekByte();
                // Filter < and (
                if (b == 0x3C || b == 0x28)
                {
                    input.Skip(1);
                }
                // Filter > and ) at the end
                if (b == -1 || b == 0x3E || b == 0x29)
                {
                    break;
                }
                if (input.Read(buffer) == 2)
                    output.WriteByte(ConvertUtils.ReadHexByte(buffer));
            }
            return output.AsMemory();
        }
    }
}