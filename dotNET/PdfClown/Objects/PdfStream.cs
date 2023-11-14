/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Bytes.Filters;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Files;
using PdfClown.Files;
using PdfClown.Tokens;

using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfClown.Objects
{
    /**
      <summary>PDF stream object [PDF:1.6:3.2.7].</summary>
    */
    public class PdfStream : PdfDataObject, IFileResource
    {
        private static readonly byte[] BeginStreamBodyChunk = Encoding.Pdf.Encode(Symbol.LineFeed + Keyword.BeginStream + Symbol.LineFeed);
        private static readonly byte[] EndStreamBodyChunk = Encoding.Pdf.Encode(Symbol.LineFeed + Keyword.EndStream);

        internal IByteStream body;
        internal PdfDictionary header;

        private PdfObject parent;
        private bool updateable = true;
        private bool updated;
        private bool virtual_;

        /**
          <summary>Indicates whether {@link #body} has already been resolved and therefore contains the
          actual stream data.</summary>
        */
        private bool bodyResolved;
        internal EncodeState encoded = EncodeState.None;

        public PdfStream() : this(new PdfDictionary(), new ByteStream())
        { }

        public PdfStream(PdfDictionary header) : this(header, new ByteStream())
        { }

        public PdfStream(IByteStream body) : this(new PdfDictionary(), body)
        { }

        public PdfStream(PdfDictionary header, IByteStream body)
        {
            this.header = (PdfDictionary)Include(header);

            this.body = body;
            body.Dirty = false;
            body.OnChange += delegate (object sender, EventArgs args)
            { Update(); };
        }

        public override PdfObject Accept(IVisitor visitor, object data)
        { return visitor.Visit(this, data); }

        /**
          <summary>Gets the decoded stream body.</summary>
        */
        public IByteStream Body =>
                /*
NOTE: Encoding filters are removed by default because they belong to a lower layer (token
layer), so that it's appropriate and consistent to transparently keep the object layer
unaware of such a facility.
*/
                GetBody(true);

        public PdfDirectObject Filter
        {
            get => (PdfDirectObject)(header[PdfName.F] == null
                  ? header.Resolve(PdfName.Filter)
                  : header.Resolve(PdfName.FFilter));
            protected set => header[
                  header[PdfName.F] == null
                    ? PdfName.Filter
                    : PdfName.FFilter
                  ] = value;
        }

        /**
          <summary>Gets the stream body.</summary>
          <param name="decode">Defines whether the body has to be decoded.</param>
        */
        public IByteStream GetBody(bool decode)
        {
            if (!bodyResolved)
            {
                /*
                  NOTE: In case of stream data from external file, a copy to the local buffer has to be done.
                */
                FileSpecification dataFile = DataFile;
                if (dataFile != null)
                {
                    Updateable = false;
                    body.SetLength(0);
                    body.Write(dataFile.GetInputStream());
                    body.Dirty = false;
                    Updateable = true;
                }
                bodyResolved = true;
            }
            if (decode)
            {
                PdfDataObject filter = Filter;
                if (filter != null) // Stream encoded.
                {
                    header.Updateable = false;
                    body.Decode(filter, Parameters, Header);
                    // The stream is free from encodings.
                    Filter = null;
                    Parameters = null;
                    header.Updateable = true;
                }
            }
            body.Seek(0);
            return body;
        }

        public IByteStream ExtractBody(bool decode)
        {
            var buffer = GetBody(false);
            if (decode)
            {
                PdfDataObject filter = Filter;
                if (filter != null) // Stream encoded.
                {
                    buffer = buffer.Extract(filter, Parameters, Header);
                }
            }
            return buffer;
        }

        /**
          <summary>Gets the stream header.</summary>
        */
        public PdfDictionary Header => header;

        public PdfDirectObject Parameters
        {
            get => (PdfDirectObject)(header[PdfName.F] == null
                  ? header.Resolve(PdfName.DecodeParms)
                  : header.Resolve(PdfName.FDecodeParms));
            protected set => header[
                  header[PdfName.F] == null
                    ? PdfName.DecodeParms
                    : PdfName.FDecodeParms
                  ] = value;
        }

        public override PdfObject Parent
        {
            get => parent;
            internal set => parent = value;
        }

        /**
          <param name="preserve">Indicates whether the data from the old data source substitutes the
          new one. This way data can be imported to/exported from local or preserved in case of external
          file location changed.</param>
          <seealso cref="DataFile"/>
        */
        public void SetDataFile(FileSpecification value, bool preserve)
        {
            /*
              NOTE: If preserve argument is set to true, body's dirtiness MUST be forced in order to ensure
              data serialization to the new external location.

              Old data source | New data source | preserve | Action
              ----------------------------------------------------------------------------------------------
              local           | not null        | false     | A. Substitute local with new file.
              local           | not null        | true      | B. Export local to new file.
              external        | not null        | false     | C. Substitute old file with new file.
              external        | not null        | true      | D. Copy old file data to new file.
              local           | null            | (any)     | E. No action.
              external        | null            | false     | F. Empty local.
              external        | null            | true      | G. Import old file to local.
              ----------------------------------------------------------------------------------------------
            */
            FileSpecification oldDataFile = DataFile;
            PdfDirectObject dataFileObject = (value != null ? value.BaseObject : null);
            if (value != null)
            {
                if (preserve)
                {
                    if (oldDataFile != null) // Case D (copy old file data to new file).
                    {
                        if (!bodyResolved)
                        {
                            // Transfer old file data to local!
                            GetBody(false); // Ensures that external data is loaded as-is into the local buffer.
                        }
                    }
                    else // Case B (export local to new file).
                    {
                        // Transfer local settings to file!
                        header[PdfName.FFilter] = header[PdfName.Filter]; header.Remove(PdfName.Filter);
                        header[PdfName.FDecodeParms] = header[PdfName.DecodeParms]; header.Remove(PdfName.DecodeParms);

                        // Ensure local data represents actual data (otherwise it would be substituted by resolved file data)!
                        bodyResolved = true;
                    }
                    // Ensure local data has to be serialized to new file!
                    body.Dirty = true;
                }
                else // Case A/C (substitute local/old file with new file).
                {
                    // Dismiss local/old file data!
                    body.SetLength(0);
                    // Dismiss local/old file settings!
                    Filter = null;
                    Parameters = null;
                    // Ensure local data has to be loaded from new file!
                    bodyResolved = false;
                }
            }
            else
            {
                if (oldDataFile != null)
                {
                    if (preserve) // Case G (import old file to local).
                    {
                        // Transfer old file data to local!
                        GetBody(false); // Ensures that external data is loaded as-is into the local buffer.
                                        // Transfer old file settings to local!
                        header[PdfName.Filter] = header[PdfName.FFilter];
                        header.Remove(PdfName.FFilter);
                        header[PdfName.DecodeParms] = header[PdfName.FDecodeParms];
                        header.Remove(PdfName.FDecodeParms);
                    }
                    else // Case F (empty local).
                    {
                        // Dismiss old file data!
                        body.SetLength(0);
                        // Dismiss old file settings!
                        Filter = null;
                        Parameters = null;
                        // Ensure local data represents actual data (otherwise it would be substituted by resolved file data)!
                        bodyResolved = true;
                    }
                }
                else // E (no action).
                { /* NOOP */ }
            }
            header[PdfName.F] = dataFileObject;
        }

        public override PdfObject Swap(PdfObject other)
        {
            PdfStream otherStream = (PdfStream)other;
            PdfDictionary otherHeader = otherStream.header;
            IByteStream otherBody = otherStream.body;
            // Update the other!
            otherStream.header = this.header;
            otherStream.body = this.body;
            otherStream.Update();
            // Update this one!
            this.header = otherHeader;
            this.body = otherBody;
            this.Update();
            return this;
        }

        public override bool Updateable
        {
            get => updateable;
            set => updateable = value;
        }

        public override bool Updated
        {
            get => updated;
            protected internal set => updated = value;
        }

        public override void WriteTo(IOutputStream stream, File context)
        {
            /*
              NOTE: The header is temporarily tweaked to accommodate serialization settings.
            */
            header.Updateable = false;

            Memory<byte> bodyData = null;
            {
                bool filterApplied = false;
                {
                    /*
                      NOTE: In case of external file, the body buffer has to be saved back only if the file was
                      actually resolved (that is brought into the body buffer) and modified.
                    */
                    FileSpecification dataFile = DataFile;
                    if (dataFile == null || (bodyResolved && body.Dirty))
                    {
                        /*
                          NOTE: In order to keep the contents of metadata streams visible as plain text to tools
                          that are not PDF-aware, no filter is applied to them [PDF:1.7:10.2.2].
                        */
                        if (Filter == null
                           && context.Configuration.StreamFilterEnabled
                           && !PdfName.Metadata.Equals(header[PdfName.Type])) // Filter needed.
                        {
                            // Apply the filter to the stream!
                            bodyData = body.Encode(Bytes.Filters.Filter.Get((PdfName)(Filter = PdfName.FlateDecode)), null, Header);
                            filterApplied = true;
                        }
                        else // No filter needed.
                        { bodyData = body.AsMemory(); }

                        if (dataFile != null)
                        {
                            try
                            {
                                using (var dataFileOutputStream = dataFile.GetOutputStream())
                                { dataFileOutputStream.Write(bodyData.Span); }
                            }
                            catch (Exception e)
                            { throw new Exception("Data writing into " + dataFile.Path + " failed.", e); }
                        }
                    }
                    if (dataFile != null)
                    { bodyData = new byte[] { }; }
                }

                // Set the encoded data length!
                header[PdfName.Length] = PdfInteger.Get(bodyData.Length);

                // 1. Header.
                header.WriteTo(stream, context);

                if (filterApplied)
                {
                    // Restore actual header entries!
                    header[PdfName.Length] = PdfInteger.Get((int)body.Length);
                    Filter = null;
                }
            }

            // 2. Body.
            stream.Write(BeginStreamBodyChunk);
            stream.Write(bodyData.Span);
            stream.Write(EndStreamBodyChunk);

            header.Updateable = true;
        }

        [PDF(VersionEnum.PDF12)]
        public FileSpecification DataFile
        {
            get => FileSpecification.Wrap(header[PdfName.F]);
            set => SetDataFile(value, false);
        }

        protected internal override bool Virtual
        {
            get => virtual_;
            set => virtual_ = value;
        }
    }

    public enum EncodeState
    {
        None,
        Encoded,
        Decoded,
        Decoding,
        Identity,
        SkipXRef,
        SkipMetadata,
    }
}