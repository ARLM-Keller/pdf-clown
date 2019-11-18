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
using PdfClown.Util.Math.Geom;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Freehand "scribble" composed of one or more disjoint paths [PDF:1.6:8.4.5].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class Scribble : Markup
    {
        private IList<SKPath> paths;
        #region dynamic
        #region constructors
        public Scribble(Page page, IList<SKPath> paths, string text, DeviceColor color)
            : base(page, PdfName.Ink, new SKRect(), text)
        {
            Paths = paths;
            Color = color;
        }

        internal Scribble(PdfDirectObject baseObject) : base(baseObject)
        {
            paths = new List<SKPath>();
        }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the coordinates of each path.</summary>
        */
        public IList<SKPath> Paths
        {
            get
            {
                if (paths.Count > 0)
                    return paths;
                PdfArray pathsObject = (PdfArray)BaseDataObject[PdfName.InkList];
                double pageHeight = Page.Box.Height;
                for (int pathIndex = 0, pathLength = pathsObject.Count; pathIndex < pathLength; pathIndex++)
                {
                    var pathObject = (PdfArray)pathsObject[pathIndex];
                    var path = new SKPath();
                    var pointLength = pathObject.Count;
                    for (int pointIndex = 0; pointIndex < pointLength; pointIndex += 2)
                    {
                        var point = GetPoint(pageHeight, pathObject, pointIndex);
                        if (path.IsEmpty)
                        {
                            path.MoveTo(point);
                        }
                        else
                        {
                            path.LineTo(point);
                        }
                    }
                    paths.Add(path);
                }

                return paths;
            }
            set
            {
                paths = value;
                PdfArray pathsObject = new PdfArray();
                double pageHeight = Page.Box.Height;
                SKRect box = SKRect.Empty;
                foreach (var path in value)
                {
                    PdfArray pathObject = new PdfArray();
                    foreach (SKPoint point in path.Points)
                    {
                        if (box == SKRect.Empty)
                        { box = SKRect.Create(point.X, point.Y, 0, 0); }
                        else
                        { box.Add(point); }
                        pathObject.Add(PdfReal.Get(point.X)); // x.
                        pathObject.Add(PdfReal.Get(pageHeight - point.Y)); // y.
                    }
                    pathsObject.Add(pathObject);
                }
                Box = box;
                BaseDataObject[PdfName.InkList] = pathsObject;

            }
        }

        private static SKPoint GetPoint(double pageHeight, PdfArray pathObject, int pointIndex)
        {
            return new SKPoint((float)((IPdfNumber)pathObject[pointIndex]).RawValue,
                                        (float)(pageHeight - ((IPdfNumber)pathObject[pointIndex + 1]).RawValue));
        }

        public override void Draw(SKCanvas canvas)
        {
            base.Draw(canvas);
            var color = Color == null ? SKColors.Black : Color.ColorSpace.GetColor(Color, Alpha);
            using (var paint = new SKPaint { Color = color })
            {
                Border?.Apply(paint, null);
                foreach (var pathData in Paths)
                {
                    canvas.DrawPath(pathData, paint);
                }
            }
        }

        public override void MoveTo(SKRect newBox)
        {
            var oldBox = Box;
            //base.MoveTo(newBox);
            var dif = SKMatrix.MakeIdentity();
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(newBox.MidX, newBox.MidY));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height));
            SKMatrix.PreConcat(ref dif, SKMatrix.MakeTranslation(-oldBox.MidX, -oldBox.MidY));
            var oldPaths = Paths;
            var newPaths = new List<SKPath>();
            foreach (var path in oldPaths)
            {
                var vertices = path.Points;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = dif.MapPoint(vertices[i]);
                }
                var newPath = new SKPath();
                newPath.AddPoly(vertices, false);
                newPaths.Add(newPath);
            }
            Paths = newPaths;
            foreach (var oldPath in oldPaths)
            {
                oldPath.Dispose();
            }
        }
        #endregion
        #endregion
        #endregion
    }
}