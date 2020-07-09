/*
  Copyright 2008-2013 Stefano Chizzolini. http://www.pdfclown.org

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
      <summary>Page annotations [PDF:1.6:3.6.2].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class PageAnnotations : PageElements<Annotation>
    {
        public class AnnotationWrapper : IWrapper<Annotation>
        {
            public AnnotationWrapper(Page page)
            {
                Page = page;
            }

            public Page Page { get; }

            public Annotation Wrap(PdfDirectObject baseObject)
            {
                var annotation = Annotation.Wrap(baseObject);
                if (annotation != null)
                {
                    Page.Annotations.AddIndex(annotation);
                }
                return annotation;
            }
        }

        public static PageAnnotations Wrap(PdfDirectObject baseObject, Page page)
        {
            return baseObject?.Wrapper as PageAnnotations ?? new PageAnnotations(baseObject, page);
        }

        #region dynamic

        private readonly Dictionary<string, Annotation> nameIndex = new Dictionary<string, Annotation>(StringComparer.Ordinal);

        #region constructors
        internal PageAnnotations(PdfDirectObject baseObject, Page page)
            : base(new AnnotationWrapper(page), baseObject, page)
        { }

        public Annotation this[string name]
        {
            get
            {
                if (nameIndex.Count < Count)
                {
                    //Refresh cache;
                    for (int i = 0, length = Count; i < length; i++)
                    {
                        var item = this[i];
                    }
                }
                return nameIndex.TryGetValue(name, out var annotation) ? annotation : null;
            }
        }

        private void AddIndex(Annotation annotation)
        {
            //Recovery
            if (annotation.Page == null)
            {
                DoAdd(annotation);
            }
            if (string.IsNullOrEmpty(annotation.Name)
                || (nameIndex.TryGetValue(annotation.Name, out var existing)
                && existing != annotation))
            {
                annotation.GenerateName();
            }
            nameIndex[annotation.Name] = annotation;
        }

        public override void Add(Annotation item)
        {
            AddIndex(item);
            base.Add(item);
        }

        public override void Insert(int index, Annotation item)
        {
            AddIndex(item);
            base.Insert(index, item);
        }

        public override bool Remove(Annotation item)
        {
            nameIndex.Remove(item.Name);
            return base.Remove(item);
        }

        public override void RemoveAt(int index)
        {
            var item = this[index];
            nameIndex.Remove(item.Name);
            base.RemoveAt(index);
        }
        #endregion
        #endregion
    }
}