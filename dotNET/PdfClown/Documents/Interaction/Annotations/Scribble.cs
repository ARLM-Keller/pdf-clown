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
using System.Linq;
using PdfClown.Documents.Interaction.Annotations.ControlPoints;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Freehand "scribble" composed of one or more disjoint paths [PDF:1.6:8.4.5].</summary>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class Scribble : Markup
    {
        private IList<SKPath> paths;
        private IList<SKPath> pagePaths;
        public Scribble(Page page, IList<SKPath> paths, string text, DeviceColor color)
            : base(page, PdfName.Ink, new SKRect(), text)
        {
            Paths = paths;
            Color = color;
        }

        public Scribble(PdfDirectObject baseObject) : base(baseObject)
        {
            paths = new List<SKPath>();
        }

        public PdfArray InkList
        {
            get => (PdfArray)BaseDataObject[PdfName.InkList];
            set
            {
                var oldValue = InkList;
                if (oldValue != value)
                {
                    BaseDataObject[PdfName.InkList] = value;
                    OnPropertyChanged(oldValue, value);
                }
            }
        }
        public IList<SKPath> PagePaths
        {
            get => pagePaths ??= GetPagePaths();
            set
            {
                ClearPath(pagePaths);
                pagePaths = value;
                var pathsObject = new PdfArray();
                SKRect box = SKRect.Empty;
                foreach (var path in value)
                {
                    var pathObject = new PdfArray();
                    foreach (SKPoint point in path.Points)
                    {
                        if (box == SKRect.Empty)
                        { box = SKRect.Create(point.X, point.Y, 0, 0); }
                        else
                        { box.Add(point); }
                        pathObject.Add(PdfReal.Get(point.X)); // x.
                        pathObject.Add(PdfReal.Get(point.Y)); // y.
                    }
                    pathsObject.Add(pathObject);
                }
                PageBox = box;
                InkList = pathsObject;
                ClearPath(paths);
            }
        }



        ///<summary>Gets/Sets the coordinates of each path.</summary>
        public IList<SKPath> Paths
        {
            get => paths ??= TransformPaths(PagePaths, PageMatrix);
            set
            {
                var newPaths = new List<SKPath>();
                TransformPaths(value, newPaths, InvertPageMatrix);
                PagePaths = newPaths;
                paths = value;
            }
        }


        private static SKPoint GetPagePoint(PdfArray pathObject, int pointIndex)
        {
            return new SKPoint(
                pathObject.GetFloat(pointIndex),
                pathObject.GetFloat(pointIndex + 1));
        }

        public override void DrawSpecial(SKCanvas canvas)
        {
            var color = Color == null ? SKColors.Black : DeviceColorSpace.CalcSKColor(Color, Alpha);
            using (var paint = new SKPaint { Color = color })
            {
                Border?.Apply(paint, null);
                foreach (var pathData in PagePaths)
                {
                    canvas.DrawPath(pathData, paint);
                }
            }
        }

        public override void PageMoveTo(SKRect newBox)
        {
            var oldBox = PageBox;
            if (oldBox.Width != newBox.Width
                || oldBox.Height != newBox.Height)
            {
                Appearance.Normal[null] = null;
            }
            //base.MoveTo(newBox);
            var dif = SKMatrix.CreateIdentity()
                .PreConcat(SKMatrix.CreateTranslation(newBox.MidX, newBox.MidY))
                .PreConcat(SKMatrix.CreateScale(newBox.Width / oldBox.Width, newBox.Height / oldBox.Height))
                .PreConcat(SKMatrix.CreateTranslation(-oldBox.MidX, -oldBox.MidY));
            var oldPaths = PagePaths;
            var newPaths = new List<SKPath>();
            TransformPaths(oldPaths, newPaths, dif);
            PagePaths = newPaths;
            ClearPath(oldPaths);
        }

        public override IEnumerable<ControlPoint> GetControlPoints()
        {
            foreach (var cpBase in GetDefaultControlPoint())
            {
                yield return cpBase;
            }
        }

        private IList<SKPath> GetPagePaths()
        {
            var list = new List<SKPath>();
            PdfArray pathsObject = InkList;
            for (int pathIndex = 0, pathLength = pathsObject.Count; pathIndex < pathLength; pathIndex++)
            {
                var pathObject = (PdfArray)pathsObject[pathIndex];
                var path = new SKPath();
                var pointLength = pathObject.Count;
                for (int pointIndex = 0; pointIndex < pointLength; pointIndex += 2)
                {
                    var point = GetPagePoint(pathObject, pointIndex);
                    if (path.IsEmpty)
                    {
                        path.MoveTo(point);
                    }
                    else
                    {
                        path.LineTo(point);
                    }
                }
                list.Add(path);
            }
            return list;
        }

        private void ClearPath(IList<SKPath> paths)
        {
            var temp = paths.ToList();
            paths.Clear();
            foreach (var path in temp)
            {
                path.Dispose();
            }
        }

        private IList<SKPath> TransformPaths(IList<SKPath> fromPaths, SKMatrix sKMatrix)
        {
            IList<SKPath> toPaths = new List<SKPath>();
            TransformPaths(fromPaths, toPaths, sKMatrix);
            return toPaths;
        }

        private void TransformPaths(IList<SKPath> fromPaths, IList<SKPath> toPaths, SKMatrix sKMatrix)
        {
            ClearPath(toPaths);
            foreach (var path in fromPaths)
            {
                var clone = new SKPath();
                //path.Transform(sKMatrix, clone);

                var vertices = sKMatrix.MapPoints(path.Points);
                //for (int i = 0; i < vertices.Length; i++)
                //{
                //    vertices[i] = sKMatrix.MapPoint(vertices[i]);
                //}                
                clone.AddPoly(vertices, false);

                toPaths.Add(clone);
            }
        }
    }
}