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
using xObjects = PdfClown.Documents.Contents.XObjects;
using PdfClown.Objects;

using System.Collections.Generic;
using SkiaSharp;
using System;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Paint the specified XObject' operation [PDF:1.6:4.7].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class PaintXObject : Operation, IResourceReference<xObjects::XObject>
    {
        #region static
        #region fields
        public static readonly string OperatorKeyword = "Do";
        public static readonly SKPaint ImagePaint = new SKPaint { FilterQuality = SKFilterQuality.Low };
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public PaintXObject(PdfName name) : base(OperatorKeyword, name)
        { }

        public PaintXObject(IList<PdfDirectObject> operands) : base(OperatorKeyword, operands)
        { }
        #endregion
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the scanner for the contents of the painted external object.</summary>
          <param name="context">Scanning context.</param>
        */
        public ContentScanner GetScanner(ContentScanner context)
        {
            xObjects::XObject xObject = GetXObject(context.ContentContext);
            return xObject is xObjects::FormXObject
              ? new ContentScanner((xObjects::FormXObject)xObject, context)
              : null;
        }

        /**
          <summary>Gets the <see cref="xObjects::XObject">external object</see> resource to be painted.
          </summary>
          <param name="context">Content context.</param>
        */
        public xObjects::XObject GetXObject(IContentContext context)
        { return GetResource(context); }

        #region IResourceReference
        public xObjects::XObject GetResource(IContentContext context)
        { return context.Resources.XObjects[Name]; }

        public PdfName Name
        {
            get => (PdfName)operands[0];
            set => operands[0] = value;
        }

        public override void Scan(GraphicsState state)
        {
            var scanner = state.Scanner;
            var canvas = scanner.RenderContext;
            if (canvas == null)
                return;
            var xObject = GetXObject(scanner.ContentContext);
            if (xObject is xObjects.ImageXObject imageObject)
            {
                var image = imageObject.LoadImage(state);
                if (image != null)
                {
                    var size = imageObject.Size;
                    var imageMatrix = imageObject.Matrix;
                    imageMatrix.ScaleY *= -1;
                    SKMatrix.PreConcat(ref imageMatrix, SKMatrix.MakeTranslation(0, -size.Height));
                    canvas.Concat(ref imageMatrix);
                    canvas.DrawBitmap(image, 0, 0, ImagePaint);
                    //using (var surf = SKSurface.Create(canvas.GRContext, true, new SKImageInfo(image.Width, image.Height)))
                    //{
                    //    surf.Canvas.DrawBitmap(original, 0, 0);
                    //    surf.Canvas.Flush();
                    //    intermediates[i] = surf.Snapshot();
                    //}

                }
            }
            else if (xObject is xObjects.FormXObject formObject)
            {
                var picture = formObject.Render();
                canvas.DrawPicture(picture);
            }
        }

        #endregion
        #endregion
        #endregion
    }
}