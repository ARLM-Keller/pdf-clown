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

using bytes = PdfClown.Bytes;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Contents.Tokens;
using PdfClown.Files;
using PdfClown.Objects;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Content stream [PDF:1.6:3.7.1].</summary>
      <remarks>During its loading, this content stream is parsed and its instructions
      are exposed as a list; in case of modifications, it's user responsability
      to call the <see cref="Flush()"/> method in order to serialize back the instructions
      into this content stream.</remarks>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class ContentWrapper : PdfObjectWrapper<PdfDataObject>, IList<ContentObject>
    {

        public static ContentWrapper Wrap(PdfDirectObject baseObject, IContentContext contentContext)
        { return baseObject != null ? baseObject.ContentsWrapper ??= new ContentWrapper(baseObject, contentContext) : null; }

        private IList<ContentObject> items;

        private IContentContext contentContext;

        private ContentWrapper(PdfDirectObject baseObject, IContentContext contentContext)
        {
            this.contentContext = contentContext;
            BaseObject = baseObject;
            Load();
        }

        public override object Clone(Document context)
        { throw new NotSupportedException(); }

        /**
          <summary>Serializes the contents into the content stream.</summary>
        */
        public void Flush()
        {
            PdfStream stream;
            PdfDataObject baseDataObject = BaseDataObject;
            // Are contents just a single stream object?
            if (baseDataObject is PdfStream pdfStream) // Single stream.
            { stream = pdfStream; }
            else // Array of streams.
            {
                var streams = (PdfArray)baseDataObject;
                // No stream available?
                if (streams.Count == 0) // No stream.
                {
                    // Add first stream!
                    stream = new PdfStream();
                    streams.Add( // Inserts the new stream into the content stream.
                      File.Register(stream)); // Inserts the new stream into the file.
                }
                else // Streams exist.
                {
                    // Eliminating exceeding streams...
                    /*
                      NOTE: Applications that consume or produce PDF files are not required to preserve
                      the existing structure of the Contents array [PDF:1.6:3.6.2].
                    */
                    while (streams.Count > 1)
                    {
                        File.Unregister((PdfReference)streams[1]); // Removes the exceeding stream from the file.
                        streams.RemoveAt(1); // Removes the exceeding stream from the content stream.
                    }
                    stream = (PdfStream)streams.Resolve(0);
                }
            }

            // Get the stream buffer!
            var buffer = stream.Body;
            // Delete old contents from the stream buffer!
            buffer.SetLength(0);
            // Serializing the new contents into the stream buffer...
            Document context = Document;
            foreach (ContentObject item in items)
            {
                item.WriteTo(buffer, context);
            }
        }

        public IContentContext ContentContext => contentContext ??= BaseObject.GetContentContext();

        public int IndexOf(ContentObject obj) => items.IndexOf(obj);

        public void Insert(int index, ContentObject obj) => items.Insert(index, obj);

        public void RemoveAt(int index) => items.RemoveAt(index);

        public ContentObject this[int index]
        {
            get => items[index];
            set => items[index] = value;
        }

        public void Add(ContentObject obj) => items.Add(obj);

        public void Clear() => items.Clear();

        public bool Contains(ContentObject obj) => items.Contains(obj);

        public void CopyTo(ContentObject[] objs, int index) => items.CopyTo(objs, index);

        public int Count => items.Count;

        public bool IsReadOnly => false;

        public bool Remove(ContentObject obj) => items.Remove(obj);

        public IEnumerator<ContentObject> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ContentObject>)this).GetEnumerator();

        private void Load()
        {
            var parser = new ContentParser(new ContentStream(BaseDataObject));
            items = parser.ParseContentObjects();
        }
    }
}
