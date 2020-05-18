/*
  Copyright 2007-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using colors = PdfClown.Documents.Contents.ColorSpaces;
using fonts = PdfClown.Documents.Contents.Fonts;
using PdfClown.Documents.Contents.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Objects;
using PdfClown.Util.Math.Geom;
using PdfClown.Documents.Contents.Fonts;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Graphics state [PDF:1.6:4.3].</summary>
    */
    public sealed class GraphicsState : ICloneable
    {
        #region dynamic
        #region fields
        private IList<BlendModeEnum> blendMode;
        private SKMatrix ctm;
        private colors::Color fillColor;
        private colors::ColorSpace fillColorSpace;
        private colors::Color strokeColor;
        private colors::ColorSpace strokeColorSpace;
        private LineCapEnum lineCap;
        private LineDash lineDash;
        private LineJoinEnum lineJoin;
        private float lineWidth;
        private float miterLimit;

        private fonts::Font font;
        private float fontSize;

        private TextRenderModeEnum renderMode;
        private float rise;
        private float scale;

        private float charSpace;
        private float wordSpace;
        private float lead;

        private TextGraphicsState textState;
        private ContentScanner scanner;
        private static Stack<GraphicsState> stack;
        #endregion

        #region constructors
        private GraphicsState()
        { }

        internal GraphicsState(ContentScanner scanner)
        {
            this.scanner = scanner;
            Initialize();
        }
        #endregion

        #region interface
        #region public

        /**
         <summary>Gets/Sets the current font [PDF:1.6:5.2].</summary>
       */
        public fonts::Font Font
        {
            get => font;
            set => font = value;
        }

        /**
          <summary>Gets/Sets the current font size [PDF:1.6:5.2].</summary>
        */
        public float FontSize
        {
            get => fontSize;
            set => fontSize = value;
        }

        /**
          <summary>Gets/Sets the current text rendering mode [PDF:1.6:5.2.5].</summary>
        */
        public TextRenderModeEnum RenderMode
        {
            get => renderMode;
            set => renderMode = value;
        }

        /**
          <summary>Gets/Sets the current text rise [PDF:1.6:5.2.6].</summary>
        */
        public float Rise
        {
            get => rise;
            set => rise = value;
        }

        /**
          <summary>Gets/Sets the current horizontal scaling [PDF:1.6:5.2.3], normalized to 1.</summary>
        */
        public float Scale
        {
            get => scale;
            set => scale = value;
        }

        /**
          <summary>Gets/Sets the current character spacing [PDF:1.6:5.2.1].</summary>
        */
        public float CharSpace
        {
            get => charSpace;
            set => charSpace = value;
        }

        /**
          <summary>Gets/Sets the current word spacing [PDF:1.6:5.2.2].</summary>
        */
        public float WordSpace
        {
            get => wordSpace;
            set => wordSpace = value;
        }

        /**
         <summary>Gets/Sets the current leading [PDF:1.6:5.2.4].</summary>
       */
        public float Lead
        {
            get => lead;
            set => lead = value;
        }

        public bool RenderModeFill => RenderMode == TextRenderModeEnum.Fill
                    || RenderMode == TextRenderModeEnum.FillStroke
                    || RenderMode == TextRenderModeEnum.FillClip
                    || RenderMode == TextRenderModeEnum.FillStrokeClip;

        public bool RenderModeStroke => RenderMode == TextRenderModeEnum.Stroke
                    || RenderMode == TextRenderModeEnum.FillStroke
                    || RenderMode == TextRenderModeEnum.StrokeClip
                    || RenderMode == TextRenderModeEnum.FillStrokeClip;

        public bool RenderModeClip => RenderMode == TextRenderModeEnum.Clip
                    || RenderMode == TextRenderModeEnum.FillClip
                    || RenderMode == TextRenderModeEnum.StrokeClip
                    || RenderMode == TextRenderModeEnum.FillStrokeClip;

        public TextGraphicsState TextState
        {
            get => textState;
            set => textState = value;
        }

        /**
          <summary>Gets/Sets the current blend mode to be used in the transparent imaging model
          [PDF:1.6:5.2.1].</summary>
          <remarks>The application should use the first blend mode in the list that it recognizes.
          </remarks>
        */
        public IList<BlendModeEnum> BlendMode
        {
            get => blendMode;
            set => blendMode = value;
        }

        /**
          <summary>Gets/Sets the current transformation matrix.</summary>
        */
        public SKMatrix Ctm
        {
            get => ctm;
            set => ctm = value;
        }

        /**
          <summary>Gets/Sets the current color for nonstroking operations [PDF:1.6:4.5.1].</summary>
        */
        public colors::Color FillColor
        {
            get => fillColor;
            set => fillColor = value;
        }

        /**
          <summary>Gets/Sets the current color space for nonstroking operations [PDF:1.6:4.5.1].</summary>
        */
        public colors::ColorSpace FillColorSpace
        {
            get => fillColorSpace;
            set => fillColorSpace = value;
        }

        /**
          <summary>Gets/Sets the current line cap style [PDF:1.6:4.3.2].</summary>
        */
        public LineCapEnum LineCap
        {
            get => lineCap;
            set => lineCap = value;
        }

        /**
          <summary>Gets/Sets the current line dash pattern [PDF:1.6:4.3.2].</summary>
        */
        public LineDash LineDash
        {
            get => lineDash;
            set => lineDash = value;
        }

        /**
          <summary>Gets/Sets the current line join style [PDF:1.6:4.3.2].</summary>
        */
        public LineJoinEnum LineJoin
        {
            get => lineJoin;
            set => lineJoin = value;
        }

        /**
          <summary>Gets/Sets the current line width [PDF:1.6:4.3.2].</summary>
        */
        public float LineWidth
        {
            get => lineWidth;
            set => lineWidth = value;
        }

        /**
          <summary>Gets/Sets the current miter limit [PDF:1.6:4.3.2].</summary>
        */
        public float MiterLimit
        {
            get => miterLimit;
            set => miterLimit = value;
        }

        /**
          <summary>Gets the scanner associated to this state.</summary>
        */
        public ContentScanner Scanner => scanner;

        /**
          <summary>Gets/Sets the current color for stroking operations [PDF:1.6:4.5.1].</summary>
        */
        public colors::Color StrokeColor
        {
            get => strokeColor;
            set => strokeColor = value;
        }

        /**
          <summary>Gets/Sets the current color space for stroking operations [PDF:1.6:4.5.1].</summary>
        */
        public colors::ColorSpace StrokeColorSpace
        {
            get => strokeColorSpace;
            set => strokeColorSpace = value;
        }

        public float? StrokeAlpha { get; set; }

        public float? FillAlpha { get; set; }

        public bool AlphaIsShape { get; set; }

        public SoftMask SMask { get; set; }

        public colors.Shading Shading { get; internal set; }

        /**
  <summary>Gets the initial current transformation matrix.</summary>
*/
        public SKMatrix GetInitialCtm()
        {
            return GetInitialMatrix(Scanner.ContentContext, Scanner.CanvasSize);
        }

        public static SKMatrix GetInitialMatrix(IContentContext contentContext, SKSize canvasSize)
        {
            if (contentContext == null)
                return SKMatrix.MakeIdentity();
            return GetInitialMatrix(contentContext, canvasSize, contentContext.Box);
        }

        public static SKMatrix GetInitialMatrix(IContentContext contentContext, SKSize canvasSize, SKRect contentBox)
        {
            SKMatrix initialCtm;
            var rotation = contentContext.Rotation;
            if (contentContext is colors.TilingPattern tiling
                || contentContext is FormXObject xObject
                || contentContext is PdfType3CharProc charProc)
            {
                return SKMatrix.MakeIdentity();
            }
            else
            {
                // Axes orientation.
                initialCtm = GetRotationMatrix(canvasSize, rotation);
            }

            // Scaling.
            SKSize rotatedCanvasSize = rotation.Transform(canvasSize);
            initialCtm = initialCtm.PreConcat(SKMatrix.MakeScale(
               rotatedCanvasSize.Width / contentBox.Width,
               rotatedCanvasSize.Height / contentBox.Height
               ));

            // Origin alignment.
            initialCtm = initialCtm.PreConcat(SKMatrix.MakeTranslation(-contentBox.Left, -contentBox.Top)); //TODO: verify minimum coordinates!
            return initialCtm;
        }

        public SKPaint CreateStrokePaint()
        {
            var paint = StrokeColorSpace?.GetPaint(StrokeColor, StrokeAlpha);
            if (paint != null)
            {
                //paint.TextSize = (float)FontSize;
                //paint.TextScaleX = (float)Scale;

                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = LineWidth;
                paint.StrokeCap = LineCap.ToSkia();
                paint.StrokeJoin = LineJoin.ToSkia();
                paint.StrokeMiter = MiterLimit;

                LineDash?.Apply(paint);

                if ((BlendMode?.Count ?? 0) > 0)
                {
                    foreach (var mode in BlendMode)
                    {
                        ApplyBlend(paint, mode);
                    }
                }
            }
            return paint;
        }

        public SKPaint CreateFillPaint()
        {
            var paint = FillColorSpace?.GetPaint(FillColor, FillAlpha);
            if (paint != null)
            {
                //paint.TextSize = (float)FontSize;
                //paint.TextScaleX = (float)Scale;

                if ((BlendMode?.Count ?? 0) > 0)
                {
                    foreach (var mode in BlendMode)
                    {
                        ApplyBlend(paint, mode);
                    }
                }
            }
            return paint;
        }

        private static void ApplyBlend(SKPaint paint, BlendModeEnum mode)
        {
            switch (mode)
            {
                case BlendModeEnum.Multiply:
                    paint.BlendMode = SKBlendMode.Multiply;
                    break;
                case BlendModeEnum.Lighten:
                    paint.BlendMode = SKBlendMode.Lighten;
                    break;
                case BlendModeEnum.Luminosity:
                    paint.BlendMode = SKBlendMode.Luminosity;
                    break;
                case BlendModeEnum.Overlay:
                    paint.BlendMode = SKBlendMode.Overlay;
                    break;
                case BlendModeEnum.Normal:
                    paint.BlendMode = SKBlendMode.SrcOver;
                    break;
                case BlendModeEnum.ColorBurn:
                    paint.BlendMode = SKBlendMode.ColorBurn;
                    break;
                case BlendModeEnum.Screen:
                    paint.BlendMode = SKBlendMode.Screen;
                    break;
                case BlendModeEnum.Darken:
                    paint.BlendMode = SKBlendMode.Darken;
                    break;
                case BlendModeEnum.ColorDodge:
                    paint.BlendMode = SKBlendMode.ColorDodge;
                    break;
                case BlendModeEnum.Compatible:
                    paint.BlendMode = SKBlendMode.SrcOver;
                    break;
                case BlendModeEnum.HardLight:
                    paint.BlendMode = SKBlendMode.HardLight;
                    break;
                case BlendModeEnum.SoftLight:
                    paint.BlendMode = SKBlendMode.SoftLight;
                    break;
                case BlendModeEnum.Difference:
                    paint.BlendMode = SKBlendMode.Difference;
                    break;
                case BlendModeEnum.Exclusion:
                    paint.BlendMode = SKBlendMode.Exclusion;
                    break;
                case BlendModeEnum.Hue:
                    paint.BlendMode = SKBlendMode.Hue;
                    break;
                case BlendModeEnum.Saturation:
                    paint.BlendMode = SKBlendMode.Saturation;
                    break;
                case BlendModeEnum.Color:
                    paint.BlendMode = SKBlendMode.Color;
                    break;
            }
        }

        public static SKMatrix GetRotationMatrix(SKSize canvasSize, RotationEnum rotation)
        {
            switch (rotation)
            {
                case RotationEnum.Downward:
                    return new SKMatrix(1, 0, 0, 0, -1, canvasSize.Height, 0, 0, 1);
                case RotationEnum.Leftward:
                    return new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1);
                case RotationEnum.Upward:
                    return new SKMatrix(-1, 0, canvasSize.Width, 0, 1, 0, 0, 0, 1);
                case RotationEnum.Rightward:
                    return new SKMatrix(0, -1, canvasSize.Width, -1, 0, canvasSize.Height, 0, 0, 1);
                default:
                    throw new NotImplementedException();
            }
        }

        public static SKMatrix GetRotationMatrix(SKRect box, int degrees)
        {
            var matrix = SKMatrix.MakeRotationDegrees(degrees);
            matrix = matrix.PreConcat(SKMatrix.MakeScale(1, -1));
            var mappedBox = matrix.MapRect(box);
            matrix = matrix.PostConcat(SKMatrix.MakeTranslation(-mappedBox.Left, -mappedBox.Top));
            return matrix;
        }

        /**
          <summary>Gets the text-to-device space transformation matrix [PDF:1.6:5.3.3].</summary>
          <param name="topDown">Whether the y-axis orientation has to be adjusted to common top-down
          orientation rather than standard PDF coordinate system (bottom-up).</param>
        */
        public SKMatrix GetTextToDeviceMatrix(bool topDown)
        {
            /*
              NOTE: The text rendering matrix (trm) is obtained from the concatenation of the current
              transformation matrix (ctm) and the text matrix (tm).
            */
            SKMatrix matrix = GetUserToDeviceMatrix(topDown);
            matrix = matrix.PreConcat(textState.Tm);
            return matrix;
        }

        /**
          <summary>Gets the user-to-device space transformation matrix [PDF:1.6:4.2.3].</summary>
          <param name="topDown">Whether the y-axis orientation has to be adjusted to common top-down
          orientation rather than standard PDF coordinate system (bottom-up).</param>
        */
        public SKMatrix GetUserToDeviceMatrix(bool topDown)
        {
            if (topDown)
            {
                SKMatrix matrix = new SKMatrix { Values = new float[] { 1, 0, 0, 0, -1, scanner.CanvasSize.Height, 0, 0, 1 } };
                matrix = matrix.PreConcat(ctm);
                return matrix;
            }
            else
            {
                return ctm;
            }
        }


        public void Save()
        {
            if (stack == null)
            {
                stack = new Stack<GraphicsState>();
            }
            var cloned = (GraphicsState)Clone();
            stack.Push(cloned);
        }

        public void Restore()
        {
            if (stack != null && stack.Count > 0)
            {
                var poped = stack.Pop();
                poped.CopyTo(this);
            }
        }

        #endregion

        #region internal
        internal GraphicsState Clone(ContentScanner scanner)
        {
            GraphicsState state = (GraphicsState)Clone();
            state.scanner = scanner;
            return state;
        }

        internal void Initialize()
        {
            // State parameters initialization.
            blendMode = ExtGState.DefaultBlendMode;
            Ctm = GetInitialCtm();
            fillColor = colors::DeviceGrayColor.Default;
            fillColorSpace = colors::DeviceGrayColorSpace.Default;
            strokeColor = colors::DeviceGrayColor.Default;
            strokeColorSpace = colors::DeviceGrayColorSpace.Default;
            lineCap = LineCapEnum.Butt;
            lineDash = new LineDash();
            lineJoin = LineJoinEnum.Miter;
            lineWidth = 0;
            miterLimit = 10;
            font = null;
            fontSize = 0;
            rise = 0;
            scale = 1;
            charSpace = 0;
            wordSpace = 0;
            lead = 0;
            renderMode = TextRenderModeEnum.Fill;
            TextState = new TextGraphicsState();
            SMask = null;
            StrokeAlpha = null;
            FillAlpha = null;
            // Rendering context initialization.
            Scanner.RenderContext?.SetMatrix(ctm);
        }

        /**
          <summary>Gets a deep copy of the graphics state object.</summary>
        */
        public object Clone()
        {
            GraphicsState clone = new GraphicsState
            {
                scanner = scanner,
                //Text
                font = font,
                fontSize = fontSize,
                renderMode = renderMode,
                rise = rise,
                scale = scale,
                charSpace = charSpace,
                wordSpace = wordSpace,
                lead = lead,
                //Paint
                blendMode = blendMode,
                ctm = ctm,
                fillColor = fillColor,
                fillColorSpace = fillColorSpace,
                strokeColor = colors::DeviceGrayColor.Default,
                strokeColorSpace = colors::DeviceGrayColorSpace.Default,
                lineCap = lineCap,
                lineDash = lineDash,// != null ? new LineDash(lineDash.DashArray, lineDash.DashPhase),
                lineJoin = lineJoin,
                lineWidth = 0,
                miterLimit = 10,
                SMask = SMask,
                AlphaIsShape = AlphaIsShape,
                StrokeAlpha = StrokeAlpha,
                FillAlpha = FillAlpha,
                TextState = new TextGraphicsState
                {
                    Tm = textState.Tm,
                    Tlm = textState.Tlm
                }
            };
            return clone;
        }

        /**
          <summary>Copies this graphics state into the specified one.</summary>
          <param name="state">Target graphics state object.</param>
        */
        public void CopyTo(GraphicsState state)
        {
            state.Ctm = ctm;
            //Text
            state.font = font;
            state.fontSize = fontSize;
            state.renderMode = renderMode;
            state.rise = rise;
            state.scale = scale;
            state.charSpace = charSpace;
            state.wordSpace = wordSpace;
            state.lead = lead;
            //Paint
            state.blendMode = blendMode;
            state.fillColor = fillColor;
            state.fillColorSpace = fillColorSpace;
            state.strokeColor = strokeColor;
            state.strokeColorSpace = strokeColorSpace;
            state.lineCap = lineCap;
            state.lineDash = lineDash;
            state.lineJoin = lineJoin;
            state.lineWidth = lineWidth;
            state.miterLimit = miterLimit;
            state.TextState = textState;
            state.SMask = SMask;
            state.AlphaIsShape = AlphaIsShape;
            state.FillAlpha = FillAlpha;
            state.StrokeAlpha = StrokeAlpha;
        }


        #endregion
        #endregion
        #endregion
    }
}