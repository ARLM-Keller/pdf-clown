/*
  Copyright 2008-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;
using PdfClown.Util.Math.Geom;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Abstract vertexed shape annotation.</summary>
    */
    [PDF(VersionEnum.PDF15)]
    public abstract class VertexShape : Shape
    {
        List<SKPoint> vertices = new List<SKPoint>();
        #region dynamic
        #region constructors
        protected VertexShape(Page page, SKRect box, string text, PdfName subtype)
            : base(page, box, text, subtype)
        { }

        protected VertexShape(PdfDirectObject baseObject)
            : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the coordinates of each vertex.</summary>
        */
        public IList<SKPoint> Vertices
        {
            get
            {
                if (vertices.Count == 0)
                {
                    PdfArray verticesObject = (PdfArray)BaseDataObject[PdfName.Vertices];

                    float pageHeight = Page.Box.Height;
                    var length = verticesObject.Count;
                    for (int index = 0; index < length; index += 2)
                    {
                        vertices.Add(
                          new SKPoint(
                            ((IPdfNumber)verticesObject[index]).FloatValue,
                            pageHeight - ((IPdfNumber)verticesObject[index + 1]).FloatValue
                            )
                          );
                    }
                }
                return vertices;
            }
            set
            {
                if (vertices != value)
                {
                    vertices.Clear();
                    vertices.AddRange(value);
                }
                PdfArray verticesObject = new PdfArray();
                float pageHeight = Page.Box.Height;
                foreach (SKPoint vertex in value)
                {
                    verticesObject.Add(PdfReal.Get(vertex.X)); // x.
                    verticesObject.Add(PdfReal.Get(pageHeight - vertex.Y)); // y.
                }

                BaseDataObject[PdfName.Vertices] = verticesObject;
            }
        }

        public SKPoint FirstPoint
        {
            get => Vertices.Count == 0 ? SKPoint.Empty : vertices[0];
            set
            {
                if (Vertices.Count > 0)
                {
                    vertices[0] = value;
                    Vertices = vertices;
                }
            }
        }

        public SKPoint LastPoint
        {
            get => Vertices.Count == 0 ? SKPoint.Empty : vertices[vertices.Count - 1];
            set
            {
                if (Vertices.Count > 0)
                {
                    vertices[vertices.Count - 1] = value;
                    Vertices = vertices;
                }
            }
        }

        public void AddPoint(SKPoint point)
        {
            Vertices.Add(point);
            Vertices = vertices;
        }

        public override void RefreshBox()
        {
            SKRect box = SKRect.Empty;
            foreach (SKPoint point in Vertices)
            {
                if (box == SKRect.Empty)
                { box = SKRect.Create(point.X, point.Y, 0, 0); }
                else
                { box.Add(point); }

            }
            Box = box;
        }


        public override void DrawSpecial(SKCanvas canvas)
        {
            using (var path = new SKPath())
            {
                path.AddPoly(Vertices.ToArray());
                path.Close();
                DrawPath(canvas, path);
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
            for (int i = 0; i < Vertices.Count; i++)
            {
                vertices[i] = dif.MapPoint(vertices[i]);
            }
            Vertices = vertices;
            base.MoveTo(newBox);
        }

        #endregion
        #endregion
        #endregion
    }
}