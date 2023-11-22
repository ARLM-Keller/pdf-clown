/*
  Copyright 2010-2011 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Objects;

using System;
using SkiaSharp;
using System.Collections.Generic;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Documents.Interchange.Metadata;
using PdfClown.Documents.Contents.ColorSpaces;

namespace PdfClown.Documents.Contents.Patterns
{
    /**
      <summary>Pattern consisting of a small graphical figure called <i>pattern cell</i> [PDF:1.6:4.6.2].</summary>
      <remarks>Painting with the pattern replicates the cell at fixed horizontal and vertical intervals
      to fill an area.</remarks>
    */
    [PDF(VersionEnum.PDF12)]
    public class TilingPattern : Pattern, IContentContext
    {
        private SKPicture picture;
        private Stack<GraphicsState> states;

        /**
<summary>Uncolored tiling pattern ("stencil") associated to a color.</summary>
*/
        public sealed class Colorized : TilingPattern
        {
            private Color color;

            internal Colorized(TilingPattern uncoloredPattern, Color color)
                : base((PatternColorSpace)uncoloredPattern.ColorSpace, uncoloredPattern.BaseObject)
            { this.color = color; }

            /**
              <summary>Gets the color applied to the stencil.</summary>
            */
            public Color Color => color;
        }

        /**
          <summary>Pattern cell color mode.</summary>
        */
        public enum PaintTypeEnum
        {
            /**
              <summary>The pattern's content stream specifies the colors used to paint the pattern cell.</summary>
              <remarks>When the content stream begins execution, the current color is the one
              that was initially in effect in the pattern's parent content stream.</remarks>
            */
            Colored = 1,
            /**
              <summary>The pattern's content stream does NOT specify any color information.</summary>
              <remarks>
                <para>Instead, the entire pattern cell is painted with a separately specified color
                each time the pattern is used; essentially, the content stream describes a stencil
                through which the current color is to be poured.</para>
                <para>The content stream must not invoke operators that specify colors
                or other color-related parameters in the graphics state.</para>
              </remarks>
            */
            Uncolored = 2
        }

        /**
          Spacing adjustment of tiles relative to the device pixel grid.
        */
        public enum TilingTypeEnum
        {
            /**
              <summary>Pattern cells are spaced consistently, that is by a multiple of a device pixel.</summary>
            */
            ConstantSpacing = 1,
            /**
              <summary>The pattern cell is not distorted, but the spacing between pattern cells
              may vary by as much as 1 device pixel, both horizontally and vertically,
              when the pattern is painted.</summary>
            */
            VariableSpacing = 2,
            /**
              <summary>Pattern cells are spaced consistently as in tiling type 1
              but with additional distortion permitted to enable a more efficient implementation.</summary>
            */
            FasterConstantSpacing = 3
        }

        internal TilingPattern(PatternColorSpace colorSpace, PdfDirectObject baseObject)
            : base(colorSpace, baseObject)
        { }

        internal TilingPattern(PdfDirectObject baseObject)
            : base(baseObject)
        { }

        /**
          <summary>Gets the colorized representation of this pattern.</summary>
          <param name="color">Color to be applied to the pattern.</param>
        */
        public Colorized Colorize(Color color)
        {
            if (PaintType != PaintTypeEnum.Uncolored)
                throw new NotSupportedException("Only uncolored tiling patterns can be colorized.");

            return new Colorized(this, color);
        }

        /**
          <summary>Gets the pattern cell's bounding box (expressed in the pattern coordinate system)
          used to clip the pattern cell.</summary>
        */
        public SKRect Box
        {
            /*
                  NOTE: 'BBox' entry MUST be defined.
            */
            get => Wrap<Rectangle>(BaseHeader[PdfName.BBox]).ToRect();
        }

        /**
          <summary>Gets how the color of the pattern cell is to be specified.</summary>
        */
        public PaintTypeEnum PaintType => (PaintTypeEnum)BaseHeader.GetInt(PdfName.PaintType);

        /**
          <summary>Gets the named resources required by the pattern's content stream.</summary>
        */
        public Resources Resources => Wrap<Resources>(BaseHeader[PdfName.Resources]);

        /**
          <summary>Gets how to adjust the spacing of tiles relative to the device pixel grid.</summary>
        */
        public TilingTypeEnum TilingType => (TilingTypeEnum)BaseHeader.GetInt(PdfName.TilingType);

        /**
          <summary>Gets the horizontal spacing between pattern cells (expressed in the pattern coordinate system).</summary>
        */
        public float XStep => BaseHeader.GetFloat(PdfName.XStep);

        /**
          <summary>Gets the vertical spacing between pattern cells (expressed in the pattern coordinate system).</summary>
        */
        public float YStep => BaseHeader.GetFloat(PdfName.YStep);

        public ContentWrapper Contents => ContentWrapper.Wrap(BaseObject, this);

        public SKPicture GetPicture()
        {
            if (picture != null)
                return picture;
            var box = Box;
            using (var recorder = new SKPictureRecorder())// SKBitmap((int)box.Width, (int)box.Height);
            using (var canvas = recorder.BeginRecording(SKRect.Create(box.Width, box.Height)))
            {
                Render(canvas, box.Size, false);
                return picture = recorder.EndRecording();
            }
        }

        public SKShader GetShader(GraphicsState state)
        {
            return SKShader.CreatePicture(GetPicture(), SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKMatrix, SKRect.Create(XStep, Math.Abs(YStep)));
        }

        public void Render(SKCanvas context, SKSize size, bool clearContext = true)
        {
            var scanner = new ContentScanner(Contents)
            {
                ClearContext = clearContext
            };
            scanner.Render(context, size);
        }

        public AppData GetAppData(PdfName appName)
        {
            throw new NotImplementedException();
        }

        public void Touch(PdfName appName)
        {
            throw new NotImplementedException();
        }

        public void Touch(PdfName appName, DateTime modificationDate)
        {
            throw new NotImplementedException();
        }

        public ContentObject ToInlineObject(PrimitiveComposer composer)
        {
            throw new NotImplementedException();
        }

        public XObjects.XObject ToXObject(Document context)
        {
            throw new NotImplementedException();
        }

        private PdfDictionary BaseHeader => ((PdfStream)BaseDataObject).Header;

        public RotationEnum Rotation => RotationEnum.Downward;

        public int Rotate => 0;

        public SKMatrix RotateMatrix => SKMatrix.Identity;

        public AppDataCollection AppData => throw new NotImplementedException();

        public DateTime? ModificationDate => Dictionary.GetDate(PdfName.LastModified);

        public List<ITextString> Strings { get; } = new List<ITextString>();

        public TransparencyXObject Group => throw new NotImplementedException();

        public Stack<GraphicsState> GetGraphicsStateContext() => states ??= new Stack<GraphicsState>();
    }
}