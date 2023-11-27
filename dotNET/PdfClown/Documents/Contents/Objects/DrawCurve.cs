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
using PdfClown.Objects;

using System.Collections.Generic;
using SkiaSharp;
using System;

namespace PdfClown.Documents.Contents.Objects
{
    /**
      <summary>'Append a cubic Bezier curve to the current path' operation [PDF:1.6:4.4.1].</summary>
      <remarks>Such curves are defined by four points:
      the two endpoints (the current point and the final point)
      and two control points (the first control point, associated to the current point,
      and the second control point, associated to the final point).</remarks>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class DrawCurve : Operation
    {
        /**
          <summary>Specifies only the second control point
          (the first control point coincides with the initial point of the curve).</summary>
        */
        public static readonly string FinalOperatorKeyword = "v";
        /**
          <summary>Specifies both control points explicitly.</summary>
        */
        public static readonly string FullOperatorKeyword = "c";
        /**
          <summary>Specifies only the first control point
          (the second control point coincides with the final point of the curve).</summary>
        */
        public static readonly string InitialOperatorKeyword = "y";

        /**
          <summary>Creates a fully-explicit curve.</summary>
          <param name="point">Final endpoint.</param>
          <param name="control1">First control point.</param>
          <param name="control2">Second control point.</param>
        */
        public DrawCurve(SKPoint point, SKPoint control1, SKPoint control2)
            : this(point.X, point.Y, control1.X, control1.Y, control2.X, control2.Y)
        { }

        /**
          <summary>Creates a fully-explicit curve.</summary>
        */
        public DrawCurve(double pointX, double pointY, double control1X, double control1Y, double control2X, double control2Y)
            : base(FullOperatorKeyword, new List<PdfDirectObject>(6)
              {
                  PdfReal.Get(control1X),
                  PdfReal.Get(control1Y),
                  PdfReal.Get(control2X),
                  PdfReal.Get(control2Y),
                  PdfReal.Get(pointX),
                  PdfReal.Get(pointY)
              })
        { }

        /**
          <summary>Creates a partially-explicit curve.</summary>
          <param name="point">Final endpoint.</param>
          <param name="control">Explicit control point.</param>
          <param name="operator">Operator (either <code>InitialOperator</code> or <code>FinalOperator</code>).
          It defines how to interpret the <code>control</code> parameter.</param>
        */
        public DrawCurve(SKPoint point, SKPoint control, string @operator)
            : base(@operator.Equals(InitialOperatorKeyword, StringComparison.Ordinal)
                  ? InitialOperatorKeyword
                  : FinalOperatorKeyword, new List<PdfDirectObject>(4)
              {
                  PdfReal.Get(control.X),
                  PdfReal.Get(control.Y),
                  PdfReal.Get(point.X),
                  PdfReal.Get(point.Y)
              })
        { }

        public DrawCurve(string @operator, IList<PdfDirectObject> operands) : base(@operator, operands)
        { }

        /**
          <summary>Gets/Sets the first control point.</summary>
        */
        public SKPoint? Control1
        {
            get
            {
                return @operator.Equals(FinalOperatorKeyword, StringComparison.Ordinal)
                    ? null
                    : new SKPoint(
                      ((IPdfNumber)operands[0]).FloatValue,
                      ((IPdfNumber)operands[1]).FloatValue);
            }
            set
            {
                if (@operator.Equals(FinalOperatorKeyword, StringComparison.Ordinal))
                {
                    @operator = FullOperatorKeyword;
                    operands.Insert(0, PdfReal.Get(value.Value.X));
                    operands.Insert(1, PdfReal.Get(value.Value.Y));
                }
                else
                {
                    operands[0] = PdfReal.Get(value.Value.X);
                    operands[1] = PdfReal.Get(value.Value.Y);
                }
            }
        }

        /**
          <summary>Gets/Sets the second control point.</summary>
        */
        public SKPoint? Control2
        {
            get
            {
                return @operator.Equals(FinalOperatorKeyword, StringComparison.Ordinal)
                    ? new SKPoint(
                      ((IPdfNumber)operands[0]).FloatValue,
                      ((IPdfNumber)operands[1]).FloatValue)
                    : new SKPoint(
                      ((IPdfNumber)operands[2]).FloatValue,
                      ((IPdfNumber)operands[3]).FloatValue);
            }
            set
            {
                if (@operator.Equals(FinalOperatorKeyword, StringComparison.Ordinal))
                {
                    operands[0] = PdfReal.Get(value.Value.X);
                    operands[1] = PdfReal.Get(value.Value.Y);
                }
                else
                {
                    operands[2] = PdfReal.Get(value.Value.X);
                    operands[3] = PdfReal.Get(value.Value.Y);
                }
            }
        }

        /**
          <summary>Gets/Sets the final endpoint.</summary>
        */
        public SKPoint Point
        {
            get
            {
                return @operator.Equals(FullOperatorKeyword, StringComparison.Ordinal)
                    ? new SKPoint(
                      ((IPdfNumber)operands[4]).FloatValue,
                      ((IPdfNumber)operands[5]).FloatValue)
                    : new SKPoint(
                      ((IPdfNumber)operands[2]).FloatValue,
                      ((IPdfNumber)operands[3]).FloatValue);
            }
            set
            {
                if (@operator.Equals(FullOperatorKeyword, StringComparison.Ordinal))
                {
                    operands[4] = PdfReal.Get(value.X);
                    operands[5] = PdfReal.Get(value.Y);
                }
                else
                {
                    operands[2] = PdfReal.Get(value.X);
                    operands[3] = PdfReal.Get(value.Y);
                }
            }
        }

        public override void Scan(GraphicsState state)
        {
            var pathObject = state.Scanner.RenderObject;
            if (pathObject != null)
            {
                SKPoint controlPoint1 = Control1 ?? pathObject.LastPoint;
                SKPoint finalPoint = Point;
                SKPoint controlPoint2 = Control2 ?? finalPoint;
                pathObject.CubicTo(controlPoint1, controlPoint2, finalPoint);
            }
        }
    }
}