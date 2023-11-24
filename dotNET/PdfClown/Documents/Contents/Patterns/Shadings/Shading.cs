/*
  Copyright 2010-2012 Stefano Chizzolini. http://www.pdfclown.org

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

using Org.BouncyCastle.Utilities;
using PdfClown.Bytes;
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static PdfClown.Documents.Contents.Fonts.CCF.CFFParser;

namespace PdfClown.Documents.Contents.Patterns.Shadings
{
    /**
      <summary>Shading object [PDF:1.6:4.6.3].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public abstract class Shading : PdfObjectWrapper<PdfDataObject>
    {
        //TODO:shading types!
        public static Shading Wrap(PdfDirectObject baseObject)
        {
            if (baseObject == null)
                return null;
            if (baseObject.Wrapper is Shading shading)
                return shading;
            if (baseObject is PdfReference reference && reference.DataObject?.Wrapper is Shading referenceShading)
            {
                baseObject.Wrapper = referenceShading;
                return referenceShading;
            }
            var dataObject = baseObject.Resolve();
            var dictionary = TryGetDictionary(dataObject);
            var type = dictionary.GetInt(PdfName.ShadingType);
            switch (type)
            {
                case 1: return new FunctionBasedShading(baseObject);
                case 2: return new AxialShading(baseObject);
                case 3: return new RadialShading(baseObject);
                case 4: return new FreeFormShading(baseObject);
                case 5: return new LatticeFormShading(baseObject);
                case 6: return new CoonsFormShading(baseObject);
                case 7: return new TensorProductShading(baseObject);
            }
            return new AxialShading(baseObject);
        }  //TODO:shading types!

        //TODO:IMPL new element constructor!

        protected Shading(PdfDirectObject baseObject) : base(baseObject)
        { }

        internal Shading() : base(new PdfDictionary())
        { }


        public int ShadingType
        {
            get => Dictionary.GetInt(PdfName.ShadingType);
            set => Dictionary.SetInt(PdfName.ShadingType, value);
        }

        public ColorSpace ColorSpace
        {
            get => ColorSpace.Wrap(Dictionary[PdfName.ColorSpace]);
            set => Dictionary[PdfName.ColorSpace] = value?.BaseObject;
        }

        public PdfArray Background
        {
            get => Dictionary.GetArray(PdfName.Background);
            set => Dictionary[PdfName.Background] = value;
        }

        public Color BackgroundColor => Background is PdfArray array ? ColorSpace.GetColor(array, null) : null;

        public virtual SKRect Box
        {
            get
            {
                var box = Wrap<Rectangle>(Dictionary[PdfName.BBox]);
                return box?.ToRect() ?? SKRect.Empty;
            }
        }

        public bool AntiAlias
        {
            get => Dictionary.GetBool(PdfName.AntiAlias);
            set => Dictionary.SetBool(PdfName.AntiAlias, value);
        }

        public abstract SKShader GetShader(SKMatrix sKMatrix, GraphicsState state);
    }
}
