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

using PdfClown.Documents;
using PdfClown.Files;
using PdfClown.Objects;

using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfClown.Documents.Interchange.Metadata
{
    /**
      <summary>Document information [PDF:1.6:10.2.1].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class Information : PdfObjectWrapper<PdfDictionary>, IDictionary<PdfName, object>
    {
        public Information(Document context) : base(context, new PdfDictionary())
        { }

        public Information(PdfDirectObject baseObject) : base(baseObject)
        { }

        public string Author
        {
            get => BaseDataObject.GetString(PdfName.Author);
            set => BaseDataObject.SetText(PdfName.Author, value);
        }

        public DateTime? CreationDate
        {
            get => BaseDataObject.GetDate(PdfName.CreationDate);
            set => BaseDataObject.SetDate(PdfName.CreationDate, value);
        }

        public string Creator
        {
            get => BaseDataObject.GetString(PdfName.Creator);
            set => BaseDataObject.SetText(PdfName.Creator, value);
        }

        [PDF(VersionEnum.PDF11)]
        public string Keywords
        {
            get => BaseDataObject.GetString(PdfName.Keywords);
            set => BaseDataObject.SetText(PdfName.Keywords, value);
        }

        [PDF(VersionEnum.PDF11)]
        public DateTime? ModificationDate
        {
            get => BaseDataObject.GetDate(PdfName.ModDate);
            set => BaseDataObject.SetDate(PdfName.ModDate, value);
        }

        public string Producer
        {
            get => BaseDataObject.GetString(PdfName.Producer);
            set => BaseDataObject.SetText(PdfName.Producer, value);
        }

        [PDF(VersionEnum.PDF11)]
        public string Subject
        {
            get => BaseDataObject.GetString(PdfName.Subject);
            set => BaseDataObject.SetText(PdfName.Subject, value);
        }

        [PDF(VersionEnum.PDF11)]
        public string Title
        {
            get => BaseDataObject.GetString(PdfName.Title);
            set => BaseDataObject.SetText(PdfName.Title, value);
        }

        public void Add(PdfName key, object value) => BaseDataObject.Add(key, PdfSimpleObject<object>.Get(value));

        public bool ContainsKey(PdfName key) => BaseDataObject.ContainsKey(key);

        public ICollection<PdfName> Keys => BaseDataObject.Keys;

        public bool Remove(PdfName key) => BaseDataObject.Remove(key);

        public object this[PdfName key]
        {
            get => PdfSimpleObject<object>.GetValue(BaseDataObject[key]);
            set => BaseDataObject[key] = PdfSimpleObject<object>.Get(value);
        }

        public bool TryGetValue(PdfName key, out object value)
        {
            PdfDirectObject valueObject;
            if (BaseDataObject.TryGetValue(key, out valueObject))
            {
                value = PdfSimpleObject<object>.GetValue(valueObject);
                return true;
            }
            else
                value = null;
            return false;
        }

        public ICollection<object> Values
        {
            get
            {
                IList<object> values = new List<object>();
                foreach (PdfDirectObject item in BaseDataObject.Values)
                { values.Add(PdfSimpleObject<object>.GetValue(item)); }
                return values;
            }
        }

        void ICollection<KeyValuePair<PdfName, object>>.Add(KeyValuePair<PdfName, object> entry) => Add(entry.Key, entry.Value);

        public void Clear() => BaseDataObject.Clear();

        bool ICollection<KeyValuePair<PdfName, object>>.Contains(KeyValuePair<PdfName, object> entry) => entry.Value.Equals(this[entry.Key]);

        public void CopyTo(KeyValuePair<PdfName, object>[] entries, int index) => throw new NotImplementedException();

        public int Count => BaseDataObject.Count;

        public bool IsReadOnly => false;

        public bool Remove(KeyValuePair<PdfName, object> entry) => throw new NotImplementedException();

        IEnumerator<KeyValuePair<PdfName, object>> IEnumerable<KeyValuePair<PdfName, object>>.GetEnumerator()
        {
            foreach (KeyValuePair<PdfName, PdfDirectObject> entry in BaseDataObject)
            {
                yield return new KeyValuePair<PdfName, object>(
                  entry.Key,
                  PdfSimpleObject<object>.GetValue(entry.Value)
                  );
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<PdfName, object>>)this).GetEnumerator();
    }
}