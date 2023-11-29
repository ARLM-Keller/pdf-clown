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
using PdfClown.Tools;
using PdfClown.Documents.Contents.XObjects;
using static PdfClown.Documents.Contents.Objects.ShowText;
using PdfClown.Bytes.Filters.Jpx;
using PdfClown.Documents.Interaction.Forms;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Paint the specified XObject' operation [PDF:1.6:4.7].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class PaintXObject : Operation, IResourceReference<xObjects::XObject>
    {
        public static readonly string OperatorKeyword = "Do";
        public static readonly SKPaint ImagePaint = new SKPaint { FilterQuality = SKFilterQuality.Low, BlendMode = SKBlendMode.SrcOver };

        public PaintXObject(PdfName name) : base(OperatorKeyword, name)
        { }

        public PaintXObject(IList<PdfDirectObject> operands) : base(OperatorKeyword, operands)
        { }

        /**
          <summary>Gets the scanner for the contents of the painted external object.</summary>
          <param name="context">Scanning context.</param>
        */
        public ContentScanner GetScanner(ContentScanner context)
        {
            xObjects::XObject xObject = GetXObject(context);
            return xObject is FormXObject form
              ? new ContentScanner(form, context)
              : null;
        }

        /**
          <summary>Gets the <see cref="xObjects::XObject">external object</see> resource to be painted.
          </summary>
          <param name="context">Content context.</param>
        */
        public xObjects::XObject GetXObject(ContentScanner scanner) => GetResource(scanner);

        public xObjects::XObject GetResource(ContentScanner scanner)
        {
            var pscanner = scanner;
            xObjects::XObject xobj;

            while ((xobj = pscanner.ContentContext.Resources.XObjects[Name]) == null
                && (pscanner = pscanner.ParentLevel) != null)
            { }
            return xobj;
        }

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
            var xObject = GetXObject(scanner);

            try
            {
                canvas.Save();
                if (xObject is ImageXObject imageObject)
                {
                    var image = imageObject.Load(state);
                    if (image != null)
                    {
                        var size = imageObject.Size;
                        var imageMatrix = imageObject.Matrix;
                        imageMatrix.ScaleY *= -1;
                        imageMatrix = imageMatrix.PreConcat(SKMatrix.CreateTranslation(0, -size.Height));
                        canvas.Concat(ref imageMatrix);

                        if (imageObject.ImageMask)
                        {
                            using (var paint = state.CreateFillPaint())
                            {
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
                else if (xObject is FormXObject formObject)
                {
                    var picture = formObject.Render(scanner);

                    var ctm = state.Ctm;
                    ctm = ctm.PreConcat(formObject.Matrix);
                    canvas.SetMatrix(ctm);

                    using (var paint = state.CreateFillPaint())
                    {
                        canvas.DrawPicture(picture, paint);
                    }

                    foreach (var textString in formObject.Strings)
                    {
                        scanner.ContentContext.Strings.Add(TextString.Transform(textString, ctm, scanner.ContentContext));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Some ({ex}) trouble with {xObject}");
            }
            finally
            {
                canvas.Restore();
            }
        }



    }
}
