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

using org.pdfclown.objects;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace org.pdfclown.documents.contents.colorSpaces
{
    /**
      <summary>Shading object [PDF:1.6:4.6.3].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public class Shading : PdfObjectWrapper<PdfDataObject>
    {
        //TODO:shading types!
        #region static
        #region interface
        #region public
        public static Shading Wrap(PdfDirectObject baseObject)
        {
            if (baseObject == null)
                return null;

            var dataObject = baseObject.Resolve();
            var dictionary = TryGetDictionary(dataObject);
            var type = ((PdfInteger)dictionary.Resolve(PdfName.ShadingType))?.RawValue;
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
            return new Shading(baseObject);
        }  //TODO:shading types!
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        //TODO:IMPL new element constructor!

        internal Shading(PdfDirectObject baseObject) : base(baseObject)
        { }

        internal Shading() : base(new PdfDictionary())
        { }
        #endregion

        #region interface

        public int ShadingType
        {
            get => ((PdfInteger)Dictionary.Resolve(PdfName.ShadingType))?.RawValue ?? 0;
            set => Dictionary[PdfName.ShadingType] = new PdfInteger(value);
        }

        public ColorSpace ColorSpace
        {
            get => ColorSpace.Wrap(Dictionary[PdfName.ColorSpace]);
            set => Dictionary[PdfName.ColorSpace] = value?.BaseObject;
        }

        public PdfArray Background
        {
            get => Dictionary.Resolve(PdfName.Background) as PdfArray;
            set => Dictionary[PdfName.Background] = value;
        }

        public SKRect Box
        {
            get
            {
                var box = Rectangle.Wrap(Dictionary[PdfName.BBox]);
                return SKRect.Create((float)box.X, (float)box.Y, (float)box.Width, (float)box.Height);
            }
        }

        public bool AntiAlias
        {
            get => ((PdfBoolean)Dictionary.Resolve(PdfName.AntiAlias))?.RawValue ?? false;
            set => Dictionary[PdfName.AntiAlias] = PdfBoolean.Get(value);
        }

        public virtual SKShader GetShader()
        {
            return null;
        }

        #endregion
        #endregion
    }

    public class FunctionBasedShading : Shading
    {
        internal FunctionBasedShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public FunctionBasedShading()
        {
            ShadingType = 1;
        }

        public float[] Domain
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Domain) as PdfArray;
                if (array == null) return new float[] { 0F, 1F, 0F, 1F };
                return new float[] {
                    (float)((PdfReal)array[0]).RawValue,
                    (float)((PdfReal)array[1]).RawValue,
                    (float)((PdfReal)array[2]).RawValue,
                    (float)((PdfReal)array[3]).RawValue,
                };
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1]),
                    new PdfReal(value[2]),
                    new PdfReal(value[3])
                    );
            }
        }

        public SKMatrix Matrix
        {
            get
            {
                PdfArray matrix = (PdfArray)Dictionary.Resolve(PdfName.Matrix);
                if (matrix == null)
                    return SKMatrix.MakeIdentity();
                else
                    return new SKMatrix
                    {
                        ScaleX = ((IPdfNumber)matrix[0]).FloatValue,
                        SkewY = ((IPdfNumber)matrix[1]).FloatValue,
                        SkewX = ((IPdfNumber)matrix[2]).FloatValue,
                        ScaleY = ((IPdfNumber)matrix[3]).FloatValue,
                        TransX = ((IPdfNumber)matrix[4]).FloatValue,
                        TransY = ((IPdfNumber)matrix[5]).FloatValue,
                        Persp2 = 1
                    };
            }
            set
            {
                Dictionary[PdfName.Matrix] =
                 new PdfArray(
                    PdfReal.Get(value.ScaleX),
                    PdfReal.Get(value.SkewY),
                    PdfReal.Get(value.SkewX),
                    PdfReal.Get(value.ScaleY),
                    PdfReal.Get(value.TransX),
                    PdfReal.Get(value.TransY)
                    );
            }
        }

        public functions.Function Function
        {
            get => functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }
    }

    public class AxialShading : Shading
    {
        internal AxialShading(PdfDirectObject baseObject) : base(baseObject)
        { }
        public AxialShading()
        {
            ShadingType = 2;
        }

        public SKPoint[] Coords
        {
            get
            {
                var array = Dictionary[PdfName.Coords] as PdfArray;
                var coords = new SKPoint[] {
                    new SKPoint((float)((PdfReal)array[0]).RawValue,
                    (float)((PdfReal)array[1]).RawValue),
                    new SKPoint((float)((PdfReal)array[2]).RawValue,
                    (float)((PdfReal)array[3]).RawValue)};
                return coords;
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0].X), new PdfReal(value[0].Y),
                    new PdfReal(value[1].X), new PdfReal(value[1].Y)
                    );
            }
        }

        public float[] Domain
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Domain) as PdfArray;
                if (array == null) return new float[] { 0F, 1F };
                return new float[] {
                    (float)((PdfReal)array[0]).RawValue,
                    (float)((PdfReal)array[1]).RawValue
                };
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1])
                    );
            }
        }

        public functions.Function Function
        {
            get => functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public bool[] Extend
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Extend) as PdfArray;
                if (array == null) return new bool[] { false, false };
                return new bool[] {
                    ((PdfBoolean)array[0]).RawValue,
                    ((PdfBoolean)array[1]).RawValue
                };
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    PdfBoolean.Get(value[0]),
                    PdfBoolean.Get(value[1])
                    );
            }
        }

        public override SKShader GetShader()
        {
            var colorSpace = ColorSpace;
            //var indexed = colorSpace is IndexedColorSpace;
            //var compCount = colorSpace.ComponentCount;
            //var count = Background.Count / compCount;
            //var colors = new SKColor[count];
            //var background = Background;
            //for (int i = 0; i < count; i++)
            //{
            //    var components = new PdfDirectObject[compCount];
            //    for (int j = 0; j < compCount; j++)
            //    {
            //        components[j] = background[i * j];
            //    }
            //    var pdfColor = colorSpace.GetColor(components, null);
            //    colors[i] = colorSpace.GetColor(pdfColor);
            //}
            // Function.Calculate()
            return null;// SKShader.CreateLinearGradient(Coords[0], Coords[1], colors, Domain, SKShaderTileMode.Clamp);

        }


    }

    public class RadialShading : Shading
    {
        internal RadialShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public RadialShading()
        {
            ShadingType = 3;
        }

        public SKPoint3[] Coords
        {
            get
            {
                var array = Dictionary[PdfName.Coords] as PdfArray;
                var coords = new SKPoint3[] {
                    new SKPoint3((float)((PdfReal)array[0]).RawValue,
                    (float)((PdfReal)array[1]).RawValue,
                    (float)((PdfReal)array[2]).RawValue),
                    new SKPoint3((float)((PdfReal)array[3]).RawValue,
                    (float)((PdfReal)array[4]).RawValue,
                    (float)((PdfReal)array[5]).RawValue),
                };
                return coords;
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0].X), new PdfReal(value[0].Y), new PdfReal(value[0].Z),
                    new PdfReal(value[1].X), new PdfReal(value[1].Y), new PdfReal(value[1].Z)
                    );
            }
        }

        public float[] Domain
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Domain) as PdfArray;
                if (array == null) return new float[] { 0F, 1F };
                return new float[] {
                    (float)((PdfReal)array[0]).RawValue,
                    (float)((PdfReal)array[1]).RawValue
                };
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1])
                    );
            }
        }

        public functions.Function Function
        {
            get => functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public bool[] Extend
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Extend) as PdfArray;
                if (array == null) return new bool[] { false, false };
                return new bool[] {
                    ((PdfBoolean)array[0]).RawValue,
                    ((PdfBoolean)array[1]).RawValue
                };
            }
            set
            {
                Dictionary[PdfName.Domain] = new PdfArray(
                    PdfBoolean.Get(value[0]),
                    PdfBoolean.Get(value[1])
                    );
            }
        }
    }

    public class FreeFormShading : Shading
    {
        internal FreeFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public FreeFormShading()
        {
            ShadingType = 4;
        }

        public int BitsPerCoordinate
        {
            get => ((PdfInteger)Dictionary.Resolve(PdfName.BitsPerCoordinate))?.RawValue ?? 0;
            set => Dictionary[PdfName.BitsPerCoordinate] = new PdfInteger(value);
        }

        public int BitsPerComponent
        {
            get => ((PdfInteger)Dictionary.Resolve(PdfName.BitsPerComponent))?.RawValue ?? 0;
            set => Dictionary[PdfName.BitsPerComponent] = new PdfInteger(value);
        }

        public int BitsPerFlag
        {
            get => ((PdfInteger)Dictionary.Resolve(PdfName.BitsPerFlag))?.RawValue ?? 0;
            set => Dictionary[PdfName.BitsPerFlag] = new PdfInteger(value);
        }
    }

    public class LatticeFormShading : Shading
    {
        internal LatticeFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public LatticeFormShading()
        {
            ShadingType = 5;
        }
    }

    public class CoonsFormShading : Shading
    {
        internal CoonsFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public CoonsFormShading()
        {
            ShadingType = 6;
        }
    }

    public class TensorProductShading : Shading
    {
        internal TensorProductShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public TensorProductShading()
        {
            ShadingType = 7;
        }
    }
}