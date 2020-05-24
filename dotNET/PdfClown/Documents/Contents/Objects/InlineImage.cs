/*
  Copyright 2007-2011 Stefano Chizzolini. http://www.pdfclown.org

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

using System.Collections.Generic;
using SkiaSharp;
using System;
using PdfClown.Documents.Contents.ColorSpaces;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>Inline image object [PDF:1.6:4.8.6].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class InlineImage : GraphicsObject, IImageObject
    {
        #region static
        #region fields
        public static readonly string BeginOperatorKeyword = BeginInlineImage.OperatorKeyword;
        public static readonly string EndOperatorKeyword = EndInlineImage.OperatorKeyword;

        private static readonly string DataOperatorKeyword = "ID";
        private SKBitmap image;
        private IContentContext Context;
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public InlineImage(InlineImageHeader header, InlineImageBody body)
        {
            objects.Add(header);
            objects.Add(body);
        }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the image body.</summary>
        */
        public Operation Body => (Operation)Objects[1];

        /**
          <summary>Gets the image header.</summary>
        */
        public override Operation Header => (Operation)Objects[0];

        IDictionary<PdfName, PdfDirectObject> IImageObject.Header => ImageHeader;

        public InlineImageHeader ImageHeader => (InlineImageHeader)Header;

        public InlineImageBody ImageBody => (InlineImageBody)Body;

        /**
          <summary>Gets the image size.</summary>
        */
        public SKSize Size
        {
            get
            {
                InlineImageHeader header = ImageHeader;
                return new SKSize(header.Width, header.Height);
            }
        }

        public SKMatrix Matrix => SKMatrix.MakeScale(1F / ImageHeader.Width, -1F / ImageHeader.Height);

        public IImageObject SMask => null;

        public bool ImageMask => bool.TryParse(ImageHeader.ImageMask, out var isMask) ? isMask : false;

        public IBuffer Data => ImageBody.Value;

        public float[] Decode => ImageHeader.Decode;

        public PdfDirectObject Filter => ImageHeader.Filter;

        public PdfDirectObject Parameters => ImageHeader.DecodeParms;

        public int BitsPerComponent => ImageHeader.BitsPerComponent;

        public ColorSpace ColorSpace
        {
            get => ImageHeader.ColorSpace ?? Context.Resources.ColorSpaces[(PdfName)ImageHeader.ColorSpaceObject];
        }

        public PdfArray Matte => null;

        public override void Scan(GraphicsState state)
        {
            Context = state.Scanner.ContentContext;
            if (state.Scanner?.RenderContext != null)
            {
                var canvas = state.Scanner.RenderContext;
                var image = Load(state);
                if (image != null)
                {
                    var size = Size;
                    var matrix = canvas.TotalMatrix;

                    matrix = matrix.PreConcat(Matrix);
                    matrix = matrix.PreConcat(SKMatrix.MakeTranslation(0, -size.Height));
                    canvas.SetMatrix(matrix);

                    if (ImageMask)
                    {
                        using (var paint = state.CreateFillPaint())
                        {
                            var r = paint.Color.Red / 255F;
                            var g = paint.Color.Green / 255F;
                            var b = paint.Color.Blue / 255F;
                            var a = paint.Color.Alpha / 255F;
                            paint.ColorFilter = SKColorFilter.CreateColorMatrix(
                                new float[] {
                                    r, 0, 0, 0, 0,
                                    0, g, 0, 0, 0,
                                    0, 0, b, 0, 0,
                                    0, 0, 0, a, 0});
                            canvas.DrawBitmap(image, 0, 0, paint);
                        }
                    }
                    else
                    {
                        using (var paint = state.CreateFillPaint())
                        {
                            canvas.DrawBitmap(image, 0, 0, paint);
                        }
                    }
                }
            }
        }

        public SKBitmap Load(GraphicsState state)
        {
            if (image != null)
                return image;
            return image = BitmapLoader.Load(this, state);
        }

        public override void WriteTo(IOutputStream stream, Document context)
        {
            stream.Write(BeginOperatorKeyword); stream.Write("\n");
            Header.WriteTo(stream, context);
            stream.Write(DataOperatorKeyword); stream.Write("\n");
            Body.WriteTo(stream, context); stream.Write("\n");
            stream.Write(EndOperatorKeyword);
        }

        #endregion
        #endregion
        #endregion
    }
}