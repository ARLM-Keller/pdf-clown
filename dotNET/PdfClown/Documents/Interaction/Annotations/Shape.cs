/*
  Copyright 2008-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents;
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Objects;

using System;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Abstract shape annotation.</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public abstract class Shape : Markup
    {
        #region dynamic
        #region constructors
        protected Shape(Page page, SKRect box, string text, PdfName subtype)
            : base(page, subtype, box, text)
        { }

        protected Shape(PdfDirectObject baseObject)
            : base(baseObject)
        { }
        #endregion

        #region interface
        #region public


        public void DrawPath(SKCanvas canvas, SKPath path)
        {
            if (InteriorColor != null)
            {
                var fillColor = InteriorSKColor.Value;
                using (var paint = new SKPaint { Color = fillColor, Style = SKPaintStyle.Fill })
                {
                    var cloudPath = BorderEffect?.Apply(paint, path) ?? path;
                    canvas.DrawPath(cloudPath, paint);
                    if (cloudPath != path)
                        cloudPath.Dispose();
                }
            }
            if (Border != null)
            {
                var color = Color == null ? SKColors.Black : DeviceColorSpace.CalcSKColor(Color, Alpha);
                using (var paint = new SKPaint { Color = color })
                {
                    Border?.Apply(paint, BorderEffect);
                    canvas.DrawPath(path, paint);
                }
            }
        }

        public override void MoveTo(SKRect newBox)
        {
            var oldBox = Box;
            if (oldBox.Width != newBox.Width
                || oldBox.Height != newBox.Height)
            {
                Appearance.Normal[null] = null;
            }

            base.MoveTo(newBox);
        }

        public override IEnumerable<ControlPoint> GetControlPoints()
        {
            foreach (var cpBase in GetDefaultControlPoint())
            {
                yield return cpBase;
            }
        }

        #endregion
        #endregion
        #endregion
    }
}