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
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Files;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using System.Reflection;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Resources collection [PDF:1.6:3.7.2].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class Resources : PdfObjectWrapper<PdfDictionary>, ICompositeDictionary<PdfName>
    {
        #region dynamic
        #region constructors
        public Resources(Document context) : base(context, new PdfDictionary())
        { }

        public Resources(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public ColorSpaceResources ColorSpaces
        {
            get => Wrap<ColorSpaceResources>(BaseDataObject.Get<PdfDictionary>(PdfName.ColorSpace));
            set => BaseDataObject[PdfName.ColorSpace] = value.BaseObject;
        }

        public ExtGStateResources ExtGStates
        {
            get => Wrap<ExtGStateResources>(BaseDataObject.Get<PdfDictionary>(PdfName.ExtGState));
            set => BaseDataObject[PdfName.ExtGState] = value.BaseObject;
        }

        public FontResources Fonts
        {
            get => Wrap<FontResources>(BaseDataObject.Get<PdfDictionary>(PdfName.Font));
            set => BaseDataObject[PdfName.Font] = value.BaseObject;
        }

        public PatternResources Patterns
        {
            get => Wrap<PatternResources>(BaseDataObject.Get<PdfDictionary>(PdfName.Pattern));
            set => BaseDataObject[PdfName.Pattern] = value.BaseObject;
        }

        [PDF(VersionEnum.PDF12)]
        public PropertyListResources PropertyLists
        {
            get => Wrap<PropertyListResources>(BaseDataObject.Get<PdfDictionary>(PdfName.Properties));
            set
            {
                CheckCompatibility("PropertyLists");
                BaseDataObject[PdfName.Properties] = value.BaseObject;
            }
        }

        [PDF(VersionEnum.PDF13)]
        public ShadingResources Shadings
        {
            get => Wrap<ShadingResources>(BaseDataObject.Get<PdfDictionary>(PdfName.Shading));
            set => BaseDataObject[PdfName.Shading] = value.BaseObject;
        }

        public XObjectResources XObjects
        {
            get => Wrap<XObjectResources>(BaseDataObject.Get<PdfDictionary>(PdfName.XObject));
            set => BaseDataObject[PdfName.XObject] = value.BaseObject;
        }

        #region ICompositeDictionary
        public PdfObjectWrapper Get(Type type)
        {
            if (typeof(ColorSpace).IsAssignableFrom(type))
                return ColorSpaces;
            else if (typeof(ExtGState).IsAssignableFrom(type))
                return ExtGStates;
            else if (typeof(Font).IsAssignableFrom(type))
                return Fonts;
            else if (typeof(Pattern).IsAssignableFrom(type))
                return Patterns;
            else if (typeof(PropertyList).IsAssignableFrom(type))
                return PropertyLists;
            else if (typeof(Shading).IsAssignableFrom(type))
                return Shadings;
            else if (typeof(XObject).IsAssignableFrom(type))
                return XObjects;
            else
                throw new ArgumentException(type.Name + " does NOT represent a valid resource class.");
        }

        public T Get<T>(PdfName key) where T : PdfObjectWrapper
        {
            PdfObjectWrapper resources = Get(typeof(T));
            return (T)resources.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.Instance).GetValue(resources, new object[] { key });
        }
        #endregion
        #endregion
        #endregion
        #endregion
    }
}