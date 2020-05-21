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

using PdfClown.Bytes;
using PdfClown.Documents.Functions;
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PdfClown.Documents.Contents.ColorSpaces
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
            if (baseObject.Wrapper is Shading shading)
                return shading;
            if (baseObject is PdfReference reference && reference.DataObject?.Wrapper is Shading referenceShading)
            {
                baseObject.Wrapper = referenceShading;
                return referenceShading;
            }
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

        protected Shading(PdfDirectObject baseObject) : base(baseObject)
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

        public float[] Background
        {
            get => ((PdfArray)Dictionary.Resolve(PdfName.Background))?.Select(p => ((IPdfNumber)p).FloatValue).ToArray();
            set => Dictionary[PdfName.Background] = new PdfArray(value.Select(p => PdfReal.Get(p)));
        }

        public SKRect? Box
        {
            get
            {
                var box = Wrap<Rectangle>(Dictionary[PdfName.BBox]);
                return box == null ? (SKRect?)null : box.ToRect();
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
        public FunctionBasedShading(PdfDirectObject baseObject) : base(baseObject)
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
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1]),
                    new PdfReal(value[2]),
                    new PdfReal(value[3])
                    );
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
            set => Dictionary[PdfName.Matrix] =
                 new PdfArray(
                    PdfReal.Get(value.ScaleX),
                    PdfReal.Get(value.SkewY),
                    PdfReal.Get(value.SkewX),
                    PdfReal.Get(value.ScaleY),
                    PdfReal.Get(value.TransX),
                    PdfReal.Get(value.TransY)
                    );
        }

        public Functions.Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
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
                    new SKPoint(((IPdfNumber)array[0]).FloatValue, ((IPdfNumber)array[1]).FloatValue),
                    new SKPoint(((IPdfNumber)array[2]).FloatValue, ((IPdfNumber)array[3]).FloatValue)};
                return coords;
            }
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0].X), new PdfReal(value[0].Y),
                    new PdfReal(value[1].X), new PdfReal(value[1].Y)
                    );
        }

        public float[] Domain
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Domain) as PdfArray;
                if (array == null) return new float[] { 0F, 1F };
                return new float[] {
                    ((IPdfNumber)array[0]).FloatValue,
                    ((IPdfNumber)array[1]).FloatValue
                };
            }
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1])
                    );
        }

        public Functions.Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
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
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    PdfBoolean.Get(value[0]),
                    PdfBoolean.Get(value[1])
                    );
        }

        public override SKShader GetShader()
        {
            var coords = Coords;
            var colorSpace = ColorSpace;
            var compCount = colorSpace.ComponentCount;
            var colors = new SKColor[2];
            //var background = Background;
            var domain = Domain;
            for (int i = 0; i < domain.Length; i++)
            {
                var components = new float[1];
                components[0] = domain[i];
                var result = Function.Calculate(components);
                colors[i] = colorSpace.GetSKColor(result, null);
            }

            return SKShader.CreateLinearGradient(coords[0], coords[1], colors, domain, SKShaderTileMode.Clamp);
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
                    new SKPoint3(((PdfReal)array[0]).FloatValue, ((PdfReal)array[1]).FloatValue, ((PdfReal)array[2]).FloatValue),
                    new SKPoint3(((PdfReal)array[3]).FloatValue, ((PdfReal)array[4]).FloatValue, ((PdfReal)array[5]).FloatValue),
                };
                return coords;
            }
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0].X), new PdfReal(value[0].Y), new PdfReal(value[0].Z),
                    new PdfReal(value[1].X), new PdfReal(value[1].Y), new PdfReal(value[1].Z)
                    );
        }

        public float[] Domain
        {
            get
            {
                var array = Dictionary.Resolve(PdfName.Domain) as PdfArray;
                if (array == null) return new float[] { 0F, 1F };
                return new float[] {
                    ((PdfReal)array[0]).FloatValue,
                    ((PdfReal)array[1]).FloatValue
                };
            }
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    new PdfReal(value[0]),
                    new PdfReal(value[1])
                    );
        }

        public Functions.Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
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
            set => Dictionary[PdfName.Domain] = new PdfArray(
                    PdfBoolean.Get(value[0]),
                    PdfBoolean.Get(value[1])
                    );
        }

        public override SKShader GetShader()
        {
            var coords = Coords;
            var colorSpace = ColorSpace;
            var compCount = colorSpace.ComponentCount;
            var colors = new SKColor[2];
            //var background = Background;
            var domain = Domain;
            for (int i = 0; i < domain.Length; i++)
            {
                var components = new float[1];
                components[0] = domain[i];
                var result = Function.Calculate(components);
                colors[i] = colorSpace.GetSKColor(result, null);
            }

            return SKShader.CreateTwoPointConicalGradient(new SKPoint(coords[0].X, coords[0].Y), coords[0].Z,
                                                          new SKPoint(coords[1].X, coords[1].Y), coords[1].Z,
                                                          colors, domain, SKShaderTileMode.Clamp);
        }
    }

    public class FreeFormShading : Shading
    {
        private float[] decode;
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

        public Functions.Function Function
        {
            get => Functions.Function.Wrap(Dictionary[PdfName.Function]);
            set => Dictionary[PdfName.Function] = value.BaseObject;
        }

        public float[] Decode
        {
            get
            {
                if (decode == null)
                {
                    var array = Dictionary.Resolve(PdfName.Decode) as PdfArray;
                    if (array == null) decode = new float[0];
                    return decode = array.Select(p => ((PdfReal)p).FloatValue).ToArray();
                }
                return decode;
            }
            set => Dictionary[PdfName.Domain] = new PdfArray(value.Select(p => PdfReal.Get(p)));
        }

        public virtual void Load()
        {

        }

        internal int numComps;

        internal IBuffer GetBuffer()
        {
            throw new NotImplementedException();
        }
    }

    public class LatticeFormShading : FreeFormShading
    {
        internal LatticeFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public LatticeFormShading()
        {
            ShadingType = 5;
        }
    }

    public class CoonsFormShading : FreeFormShading
    {


        internal CoonsFormShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public CoonsFormShading()
        {
            ShadingType = 6;
        }



        public override SKShader GetShader()
        {
            return base.GetShader();
        }
    }

    public class TensorProductShading : FreeFormShading
    {
        internal TensorProductShading(PdfDirectObject baseObject) : base(baseObject)
        { }

        public TensorProductShading()
        {
            ShadingType = 7;
        }
    }

    public class MeshStreamReader
    {
        private IBuffer stream;
        private FreeFormShading context;
        private int buffer;
        private int bufferLength;
        private float[] tmpCompsBuf;
        private float[] tmpCsCompsBuf;

        public MeshStreamReader(Bytes.IBuffer stream, FreeFormShading context)
        {
            this.stream = stream;
            this.context = context;
            this.buffer = 0;
            this.bufferLength = 0;

            var numComps = context.numComps;
            this.tmpCompsBuf = new float[numComps];
            var csNumComps = context.ColorSpace.ComponentCount;
            this.tmpCsCompsBuf = context.Function != null
              ? new float[csNumComps]
              : this.tmpCompsBuf;
        }

        public bool hasData()
        {
            if (this.stream.Length != 0)
            {
                return this.stream.Position < this.stream.Length;
            }
            if (this.bufferLength > 0)
            {
                return true;
            }
            var nextByte = this.stream.ReadByte();
            if (nextByte < 0)
            {
                return false;
            }
            this.buffer = nextByte;
            this.bufferLength = 8;
            return true;
        }

        public int readBits(int n)
        {
            var buffer = this.buffer;
            var bufferLength = this.bufferLength;
            if (n == 32)
            {
                if (bufferLength == 0)
                {
                    return (int)(
                      (uint)((this.stream.ReadByte() << 24) |
                        (this.stream.ReadByte() << 16) |
                        (this.stream.ReadByte() << 8) |
                        this.stream.ReadByte()) >> 0
                    );
                }
                buffer =
                  (buffer << 24) |
                  (this.stream.ReadByte() << 16) |
                  (this.stream.ReadByte() << 8) |
                  this.stream.ReadByte();
                var nextByte = this.stream.ReadByte();
                this.buffer = nextByte & ((1 << bufferLength) - 1);
                return (int)(
                  (uint)((buffer << (8 - bufferLength)) |
                    ((nextByte & 0xff) >> bufferLength)) >> 0
                );
            }
            if (n == 8 && bufferLength == 0)
            {
                return this.stream.ReadByte();
            }
            while (bufferLength < n)
            {
                buffer = (buffer << 8) | this.stream.ReadByte();
                bufferLength += 8;
            }
            bufferLength -= n;
            this.bufferLength = bufferLength;
            this.buffer = buffer & ((1 << bufferLength) - 1);
            return buffer >> bufferLength;
        }

        public void align()
        {
            this.buffer = 0;
            this.bufferLength = 0;
        }

        public int readFlag()
        {
            return this.readBits(this.context.BitsPerFlag);
        }

        public SKPoint readCoordinate()
        {
            var bitsPerCoordinate = this.context.BitsPerCoordinate;
            var xi = this.readBits(bitsPerCoordinate);
            var yi = this.readBits(bitsPerCoordinate);
            var decode = this.context.Decode;
            var scale =
              bitsPerCoordinate < 32
                ? 1 / ((1 << bitsPerCoordinate) - 1)
                : 2.3283064365386963e-10F; // 2 ^ -32
            return new SKPoint(
                  xi * scale * (decode[1] - decode[0]) + decode[0],
                  yi * scale * (decode[3] - decode[2]) + decode[2]);
        }
        public SKColor readComponents()
        {
            var numComps = this.context.numComps;
            var bitsPerComponent = this.context.BitsPerComponent;
            var scale =
              bitsPerComponent < 32
                ? 1 / ((1 << bitsPerComponent) - 1)
                : 2.3283064365386963e-10F; // 2 ^ -32
            var decode = this.context.Decode;
            var components = this.tmpCompsBuf;
            for (int i = 0, j = 4; i < numComps; i++, j += 2)
            {
                var ci = this.readBits(bitsPerComponent);
                components[i] = ci * scale * (decode[j + 1] - decode[j]) + decode[j];
            }
            var color = this.tmpCsCompsBuf;
            if (this.context.Function != null)
            {
                color = this.context.Function.Calculate(components);
            }
            return this.context.ColorSpace.GetSKColor(color, null);
        }
    }

    // All mesh shading. For now, they will be presented as set of the triangles
    // to be drawn on the canvas and rgb color for each vertex.
    public class Mesh
    {


        public static void DecodeType4Shading(Mesh mesh, MeshStreamReader reader)
        {
            var coords = mesh.coords;
            var colors = mesh.colors;
            var operators = new List<int>();
            var ps = new List<int>(); // not maintaining cs since that will match ps
            var verticesLeft = 0; // assuming we have all data to start a new triangle
            while (reader.hasData())
            {
                var f = reader.readFlag();
                var coord = reader.readCoordinate();
                var color = reader.readComponents();
                if (verticesLeft == 0)
                {
                    // ignoring flags if we started a triangle
                    if (!(0 <= f && f <= 2))
                    {
                        throw new Exception("Unknown type4 flag");
                    }
                    switch (f)
                    {
                        case 0:
                            verticesLeft = 3;
                            break;
                        case 1:
                            ps.Add(ps[ps.Count - 2]);
                            ps.Add(ps[ps.Count - 1]);
                            verticesLeft = 1;
                            break;
                        case 2:
                            ps.Add(ps[ps.Count - 3]);
                            ps.Add(ps[ps.Count - 1]);
                            verticesLeft = 1;
                            break;
                    }
                    operators.Add(f);
                }
                ps.Add(coords.Count);
                coords.Add(coord);
                colors.Add(color);
                verticesLeft--;

                reader.align();
            }
            mesh.figures.Add(new MeshFigure(
                type: "triangles",
                coords: ps.ToArray(),
                colors: ps.ToArray()
                ));
        }

        public static void decodeType5Shading(Mesh mesh, MeshStreamReader reader, int verticesPerRow)
        {
            var coords = mesh.coords;
            var colors = mesh.colors;
            var ps = new List<int>(); // not maintaining cs since that will match ps
            while (reader.hasData())
            {
                var coord = reader.readCoordinate();
                var color = reader.readComponents();
                ps.Add(coords.Count);
                coords.Add(coord);
                colors.Add(color);
            }
            mesh.figures.Add(new MeshFigure(
                type: "lattice",
                coords: ps.ToArray(),
                colors: ps.ToArray(),
                verticesPerRow
                ));
        }

        public const int MIN_SPLIT_PATCH_CHUNKS_AMOUNT = 3;
        public const int MAX_SPLIT_PATCH_CHUNKS_AMOUNT = 20;
        public const int TRIANGLE_DENSITY = 20; // count of triangles per entire mesh bounds

        public static readonly Dictionary<int, List<float[]>> cache = new Dictionary<int, List<float[]>>();
        private float[] matrix;
        private int shadingType;
        private ColorSpace cs;
        private SKColor? background;
        private List<SKPoint> coords;
        private List<SKColor> colors;
        private List<MeshFigure> figures;
        private float[] coordsPacket;
        private byte[] colorsPacket;
        private float[] bounds;

        public static List<float[]> buildB(int count)
        {
            var lut = new List<float[]>();
            for (var i = 0; i <= count; i++)
            {
                var t = (float)i / count;
                var t_ = 1 - t;
                lut.Add(
                  new float[]{
                        t_ * t_ * t_,
                        3 * t * t_ * t_,
                        3 * t * t * t_,
                        t * t * t});
            }
            return lut;
        }


        // eslint-disable-next-line no-shadow
        public static List<float[]> getB(int count)
        {
            if (!cache.TryGetValue(count, out var lut))
            {
                cache[count] = lut = buildB(count);
            }
            return lut;
        }

        public static void buildFigureFromPatch(Mesh mesh, int index)
        {
            var figure = mesh.figures[index];
            Debug.Assert(figure.type == "patch", "Unexpected patch mesh figure");

            var coords = mesh.coords;
            var colors = mesh.colors;
            var pi = figure.coords;
            var ci = figure.colors;

            var figureMinX = Math.Min(
                 Math.Min(
              coords[pi[0]].X,
              coords[pi[3]].X),
               Math.Min(
              coords[pi[12]].X,
              coords[pi[15]].X)
            );
            var figureMinY = Math.Min(
                 Math.Min(
              coords[pi[0]].Y,
              coords[pi[3]].X),
               Math.Min(
              coords[pi[12]].X,
              coords[pi[15]].X)
            );
            var figureMaxX = Math.Max(
                Math.Max(
              coords[pi[0]].X,
              coords[pi[3]].X),
                Math.Max(
              coords[pi[12]].X,
              coords[pi[15]].X)
            );
            var figureMaxY = Math.Max(
                Math.Max(
              coords[pi[0]].Y,
              coords[pi[3]].Y),
                Math.Max(
              coords[pi[12]].Y,
              coords[pi[15]].Y)
            );
            var splitXBy = (int)Math.Ceiling(
              ((figureMaxX - figureMinX) * TRIANGLE_DENSITY) /
                (mesh.bounds[2] - mesh.bounds[0])
            );
            splitXBy = Math.Max(
              MIN_SPLIT_PATCH_CHUNKS_AMOUNT,
              Math.Min(MAX_SPLIT_PATCH_CHUNKS_AMOUNT, splitXBy)
            );
            var splitYBy = (int)Math.Ceiling(
              ((figureMaxY - figureMinY) * TRIANGLE_DENSITY) /
                (mesh.bounds[3] - mesh.bounds[1])
            );
            splitYBy = Math.Max(
              MIN_SPLIT_PATCH_CHUNKS_AMOUNT,
              Math.Min(MAX_SPLIT_PATCH_CHUNKS_AMOUNT, splitYBy)
            );

            var verticesPerRow = splitXBy + 1;
            var figureCoords = new Int32[(splitYBy + 1) * verticesPerRow];
            var figureColors = new Int32[(splitYBy + 1) * verticesPerRow];
            var k = 0;
            var cl = new byte[3];
            var cr = new byte[3];
            var c0 = colors[ci[0]];
            var c1 = colors[ci[1]];
            var c2 = colors[ci[2]];
            var c3 = colors[ci[3]];
            var bRow = getB(splitYBy);
            var bCol = getB(splitXBy);
            for (var row = 0; row <= splitYBy; row++)
            {
                cl[0] = (byte)(((c0.Red * (splitYBy - row) + c2.Red * row) / splitYBy) | 0);
                cl[1] = (byte)(((c0.Green * (splitYBy - row) + c2.Green * row) / splitYBy) | 0);
                cl[2] = (byte)(((c0.Blue * (splitYBy - row) + c2.Blue * row) / splitYBy) | 0);

                cr[0] = (byte)(((c1.Red * (splitYBy - row) + c3.Red * row) / splitYBy) | 0);
                cr[1] = (byte)(((c1.Green * (splitYBy - row) + c3.Green * row) / splitYBy) | 0);
                cr[2] = (byte)(((c1.Blue * (splitYBy - row) + c3.Blue * row) / splitYBy) | 0);

                for (var col = 0; col <= splitXBy; col++, k++)
                {
                    if ((row == 0 || row == splitYBy) &&
                      (col == 0 || col == splitXBy))
                    {
                        continue;
                    }
                    var x = 0F;
                    var y = 0F;
                    var q = 0;
                    for (var i = 0; i <= 3; i++)
                    {
                        for (var j = 0; j <= 3; j++, q++)
                        {
                            var m = bRow[row][i] * bCol[col][j];
                            x += coords[pi[q]].X * m;
                            y += coords[pi[q]].Y * m;
                        }
                    }
                    figureCoords[k] = coords.Count;
                    coords.Add(new SKPoint(x, y));
                    figureColors[k] = colors.Count;
                    var newColor = new SKColor(
                        (byte)(((cl[0] * (splitXBy - col) + cr[0] * col) / splitXBy) | 0),
                        (byte)(((cl[1] * (splitXBy - col) + cr[1] * col) / splitXBy) | 0),
                        (byte)(((cl[2] * (splitXBy - col) + cr[2] * col) / splitXBy) | 0));
                    colors.Add(newColor);
                }
            }
            figureCoords[0] = pi[0];
            figureColors[0] = ci[0];
            figureCoords[splitXBy] = pi[3];
            figureColors[splitXBy] = ci[1];
            figureCoords[verticesPerRow * splitYBy] = pi[12];
            figureColors[verticesPerRow * splitYBy] = ci[2];
            figureCoords[verticesPerRow * splitYBy + splitXBy] = pi[15];
            figureColors[verticesPerRow * splitYBy + splitXBy] = ci[3];

            mesh.figures[index] = new MeshFigure(
                type: "lattice",
                coords: figureCoords,
                colors: figureColors,
                verticesPerRow
                );
        }

        public static void decodeType6Shading(Mesh mesh, MeshStreamReader reader)
        {
            // A special case of Type 7. The p11, p12, p21, p22 automatically filled
            var coords = mesh.coords;
            var colors = mesh.colors;
            var ps = new Int32[16]; // p00, p10, ..., p30, p01, ..., p33
            var cs = new Int32[4]; // c00, c30, c03, c33
            while (reader.hasData())
            {
                var f = reader.readFlag();
                if (!(0 <= f && f <= 3))
                {
                    throw new FormatException("Unknown type6 flag");
                }
                int i, ii;
                var pi = coords.Count;
                for (i = 0, ii = f != 0 ? 8 : 12; i < ii; i++)
                {
                    coords.Add(reader.readCoordinate());
                }
                var ci = colors.Count;
                for (i = 0, ii = f != 0 ? 2 : 4; i < ii; i++)
                {
                    colors.Add(reader.readComponents());
                }
                int tmp1, tmp2, tmp3, tmp4;
                switch (f)
                {
                    // prettier-ignore
                    case 0:
                        ps[12] = pi + 3; ps[13] = pi + 4; ps[14] = pi + 5; ps[15] = pi + 6;
                        ps[8] = pi + 2; /* values for 5, 6, 9, 10 are    */ ps[11] = pi + 7;
                        ps[4] = pi + 1; /* calculated below              */ ps[7] = pi + 8;
                        ps[0] = pi; ps[1] = pi + 11; ps[2] = pi + 10; ps[3] = pi + 9;
                        cs[2] = ci + 1; cs[3] = ci + 2;
                        cs[0] = ci; cs[1] = ci + 3;
                        break;
                    // prettier-ignore
                    case 1:
                        tmp1 = ps[12]; tmp2 = ps[13]; tmp3 = ps[14]; tmp4 = ps[15];
                        ps[12] = tmp4; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = tmp3; /* values for 5, 6, 9, 10 are    */ ps[11] = pi + 3;
                        ps[4] = tmp2; /* calculated below              */ ps[7] = pi + 4;
                        ps[0] = tmp1; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        tmp1 = cs[2]; tmp2 = cs[3];
                        cs[2] = tmp2; cs[3] = ci;
                        cs[0] = tmp1; cs[1] = ci + 1;
                        break;
                    // prettier-ignore
                    case 2:
                        tmp1 = ps[15];
                        tmp2 = ps[11];
                        ps[12] = ps[3]; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = ps[7];  /* values for 5, 6, 9, 10 are    */ ps[11] = pi + 3;
                        ps[4] = tmp2;   /* calculated below              */ ps[7] = pi + 4;
                        ps[0] = tmp1; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        tmp1 = cs[3];
                        cs[2] = cs[1]; cs[3] = ci;
                        cs[0] = tmp1; cs[1] = ci + 1;
                        break;
                    // prettier-ignore
                    case 3:
                        ps[12] = ps[0]; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = ps[1];  /* values for 5, 6, 9, 10 are    */ ps[11] = pi + 3;
                        ps[4] = ps[2];  /* calculated below              */ ps[7] = pi + 4;
                        ps[0] = ps[3]; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        cs[2] = cs[0]; cs[3] = ci;
                        cs[0] = cs[1]; cs[1] = ci + 1;
                        break;
                }
                // set p11, p12, p21, p22
                ps[5] = coords.Count;
                coords.Add(new SKPoint(
                  (-4 * coords[ps[0]].X -
                    coords[ps[15]].X +
                    6 * (coords[ps[4]].X + coords[ps[1]].X) -
                    2 * (coords[ps[12]].X + coords[ps[3]].X) +
                    3 * (coords[ps[13]].X + coords[ps[7]].X)) /
                    9,
                  (-4 * coords[ps[0]].Y -
                    coords[ps[15]].Y +
                    6 * (coords[ps[4]].Y + coords[ps[1]].Y) -
                    2 * (coords[ps[12]].Y + coords[ps[3]].Y) +
                    3 * (coords[ps[13]].Y + coords[ps[7]].Y)) /
                    9
                ));
                ps[6] = coords.Count;
                coords.Add(new SKPoint(
                  (-4 * coords[ps[3]].X -
                    coords[ps[12]].X +
                    6 * (coords[ps[2]].X + coords[ps[7]].X) -
                    2 * (coords[ps[0]].X + coords[ps[15]].X) +
                    3 * (coords[ps[4]].X + coords[ps[14]].X)) /
                    9,
                  (-4 * coords[ps[3]].Y -
                    coords[ps[12]].Y +
                    6 * (coords[ps[2]].Y + coords[ps[7]].Y) -
                    2 * (coords[ps[0]].Y + coords[ps[15]].Y) +
                    3 * (coords[ps[4]].Y + coords[ps[14]].Y)) /
                    9
                ));
                ps[9] = coords.Count;
                coords.Add(new SKPoint(
                  (-4 * coords[ps[12]].X -
                    coords[ps[3]].X +
                    6 * (coords[ps[8]].X + coords[ps[13]].X) -
                    2 * (coords[ps[0]].X + coords[ps[15]].X) +
                    3 * (coords[ps[11]].X + coords[ps[1]].X)) /
                    9,
                  (-4 * coords[ps[12]].Y -
                    coords[ps[3]].Y +
                    6 * (coords[ps[8]].Y + coords[ps[13]].Y) -
                    2 * (coords[ps[0]].Y + coords[ps[15]].Y) +
                    3 * (coords[ps[11]].Y + coords[ps[1]].Y)) /
                    9
                ));
                ps[10] = coords.Count;
                coords.Add(new SKPoint(
                  (-4 * coords[ps[15]].X -
                    coords[ps[0]].X +
                    6 * (coords[ps[11]].X + coords[ps[14]].X) -
                    2 * (coords[ps[12]].X + coords[ps[3]].X) +
                    3 * (coords[ps[2]].X + coords[ps[8]].X)) /
                    9,
                  (-4 * coords[ps[15]].Y -
                    coords[ps[0]].Y +
                    6 * (coords[ps[11]].Y + coords[ps[14]].Y) -
                    2 * (coords[ps[12]].Y + coords[ps[3]].Y) +
                    3 * (coords[ps[2]].Y + coords[ps[8]].Y)) /
                    9
                ));
                mesh.figures.Add(new MeshFigure(
                    type: "patch",
                    coords: ps.ToArray(), // making copies of ps and cs
                    colors: cs.ToArray()
                    ));
            }
        }

        public static void decodeType7Shading(Mesh mesh, MeshStreamReader reader)
        {
            var coords = mesh.coords;
            var colors = mesh.colors;
            var ps = new Int32[16]; // p00, p10, ..., p30, p01, ..., p33
            var cs = new Int32[4]; // c00, c30, c03, c33
            while (reader.hasData())
            {
                var f = reader.readFlag();
                if (!(0 <= f && f <= 3))
                {
                    throw new FormatException("Unknown type7 flag");
                }
                int i, ii;
                var pi = coords.Count;
                for (i = 0, ii = f != 0 ? 12 : 16; i < ii; i++)
                {
                    coords.Add(reader.readCoordinate());
                }
                var ci = colors.Count;
                for (i = 0, ii = f != 0 ? 2 : 4; i < ii; i++)
                {
                    colors.Add(reader.readComponents());
                }
                int tmp1, tmp2, tmp3, tmp4;
                switch (f)
                {
                    // prettier-ignore
                    case 0:
                        ps[12] = pi + 3; ps[13] = pi + 4; ps[14] = pi + 5; ps[15] = pi + 6;
                        ps[8] = pi + 2; ps[9] = pi + 13; ps[10] = pi + 14; ps[11] = pi + 7;
                        ps[4] = pi + 1; ps[5] = pi + 12; ps[6] = pi + 15; ps[7] = pi + 8;
                        ps[0] = pi; ps[1] = pi + 11; ps[2] = pi + 10; ps[3] = pi + 9;
                        cs[2] = ci + 1; cs[3] = ci + 2;
                        cs[0] = ci; cs[1] = ci + 3;
                        break;
                    // prettier-ignore
                    case 1:
                        tmp1 = ps[12]; tmp2 = ps[13]; tmp3 = ps[14]; tmp4 = ps[15];
                        ps[12] = tmp4; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = tmp3; ps[9] = pi + 9; ps[10] = pi + 10; ps[11] = pi + 3;
                        ps[4] = tmp2; ps[5] = pi + 8; ps[6] = pi + 11; ps[7] = pi + 4;
                        ps[0] = tmp1; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        tmp1 = cs[2]; tmp2 = cs[3];
                        cs[2] = tmp2; cs[3] = ci;
                        cs[0] = tmp1; cs[1] = ci + 1;
                        break;
                    // prettier-ignore
                    case 2:
                        tmp1 = ps[15];
                        tmp2 = ps[11];
                        ps[12] = ps[3]; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = ps[7]; ps[9] = pi + 9; ps[10] = pi + 10; ps[11] = pi + 3;
                        ps[4] = tmp2; ps[5] = pi + 8; ps[6] = pi + 11; ps[7] = pi + 4;
                        ps[0] = tmp1; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        tmp1 = cs[3];
                        cs[2] = cs[1]; cs[3] = ci;
                        cs[0] = tmp1; cs[1] = ci + 1;
                        break;
                    // prettier-ignore
                    case 3:
                        ps[12] = ps[0]; ps[13] = pi + 0; ps[14] = pi + 1; ps[15] = pi + 2;
                        ps[8] = ps[1]; ps[9] = pi + 9; ps[10] = pi + 10; ps[11] = pi + 3;
                        ps[4] = ps[2]; ps[5] = pi + 8; ps[6] = pi + 11; ps[7] = pi + 4;
                        ps[0] = ps[3]; ps[1] = pi + 7; ps[2] = pi + 6; ps[3] = pi + 5;
                        cs[2] = cs[0]; cs[3] = ci;
                        cs[0] = cs[1]; cs[1] = ci + 1;
                        break;
                }
                mesh.figures.Add(new MeshFigure(
                    type: "patch",
                    coords: ps.ToArray(), // making copies of ps and cs
                    colors: cs.ToArray()
                    ));
            }
        }

        public static void updateBounds(Mesh mesh)
        {
            var minX = mesh.coords[0].X;
            var minY = mesh.coords[0].Y;
            var maxX = minX;
            var maxY = minY;
            for (int i = 1, ii = mesh.coords.Count; i < ii; i++)
            {
                var x = mesh.coords[i].X;
                var y = mesh.coords[i].Y;
                minX = minX > x ? x : minX;
                minY = minY > y ? y : minY;
                maxX = maxX < x ? x : maxX;
                maxY = maxY < y ? y : maxY;
            }
            mesh.bounds = new float[] { minX, minY, maxX, maxY };
        }

        public static void packData(Mesh mesh)
        {
            int i, ii, j, jj;

            var coords = mesh.coords;
            var coordsPacked = new float[coords.Count * 2];
            for (i = 0, j = 0, ii = coords.Count; i < ii; i++)
            {
                var xy = coords[i];
                coordsPacked[j++] = xy.X;
                coordsPacked[j++] = xy.Y;
            }
            mesh.coordsPacket = coordsPacked;

            var colors = mesh.colors;
            var colorsPacked = new byte[colors.Count * 3];
            for (i = 0, j = 0, ii = colors.Count; i < ii; i++)
            {
                var c = colors[i];
                colorsPacked[j++] = c.Red;
                colorsPacked[j++] = c.Green;
                colorsPacked[j++] = c.Blue;
            }
            mesh.colorsPacket = colorsPacked;

            var figures = mesh.figures;
            for (i = 0, ii = figures.Count; i < ii; i++)
            {
                var figure = figures[i];
                var ps = figure.coords;
                var cs = figure.colors;
                for (j = 0, jj = ps.Length; j < jj; j++)
                {
                    ps[j] *= 2;
                    cs[j] *= 3;
                }
            }
        }

        public Mesh(FreeFormShading context, float[] matrix)
        {
            this.matrix = matrix;
            this.shadingType = context.ShadingType;
            var bbox = context.Box;
            this.cs = context.ColorSpace;
            this.background = context.Background != null
              ? cs.GetSKColor(context.Background, null)
              : (SKColor?)null;

            var fn = context.Function;

            this.coords = new List<SKPoint>();
            this.colors = new List<SKColor>();
            this.figures = new List<MeshFigure>();
            context.numComps = fn != null ? 1 : cs.ComponentCount;
            var reader = new MeshStreamReader(context.GetBuffer(), context);

            var patchMesh = false;
            switch (shadingType)
            {
                case 4://FreeFormShading
                    DecodeType4Shading(this, reader);
                    break;
                case 5://LatticeFormShading
                    var verticesPerRow = 2;//TODO context.BaseDataObject("VerticesPerRow") | 0;
                    if (verticesPerRow < 2)
                    {
                        throw new FormatException("Invalid VerticesPerRow");
                    }
                    decodeType5Shading(this, reader, verticesPerRow);
                    break;
                case 6://CoonsFormShading
                    decodeType6Shading(this, reader);
                    patchMesh = true;
                    break;
                case 7://TensorProductShading
                    decodeType7Shading(this, reader);
                    patchMesh = true;
                    break;
                default:
                    throw new NotSupportedException("Unsupported mesh type.");
            }

            if (patchMesh)
            {
                // dirty bounds calculation for determining, how dense shall be triangles
                updateBounds(this);
                for (int i = 0, ii = this.figures.Count; i < ii; i++)
                {
                    buildFigureFromPatch(this, i);
                }
            }
            // calculate bounds
            updateBounds(this);

            packData(this);
        }
    }

    internal class MeshFigure
    {
        internal string type;
        internal int[] coords;
        internal int[] colors;

        public MeshFigure(string type, int[] coords, int[] colors, int vertex = 0)
        {
            this.type = type;
            this.coords = coords;
            this.colors = colors;
        }
    }
}
