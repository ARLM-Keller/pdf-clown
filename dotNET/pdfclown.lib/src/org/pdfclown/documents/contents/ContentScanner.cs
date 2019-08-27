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

using org.pdfclown.bytes;
using colors = org.pdfclown.documents.contents.colorSpaces;
using fonts = org.pdfclown.documents.contents.fonts;
using org.pdfclown.documents.contents.objects;
using xObjects = org.pdfclown.documents.contents.xObjects;
using org.pdfclown.files;
using org.pdfclown.objects;

using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;
using System.Text;

namespace org.pdfclown.documents.contents
{
    /**
      <summary>Content objects scanner.</summary>
      <remarks>
        <para>It wraps the <see cref="Contents">content objects collection</see> to scan its graphics state
        through a forward cursor.</para>
        <para>Scanning is performed at an arbitrary deepness, according to the content objects nesting:
        each depth level corresponds to a scan level so that at any time it's possible to seamlessly
        navigate across the levels (see <see cref="ParentLevel"/>, <see cref="ChildLevel"/>).</para>
      </remarks>
    */
    public sealed class ContentScanner
    {
        #region delegates
        /**
          <summary>Handles the scan start notification.</summary>
          <param name="scanner">Content scanner started.</param>
        */
        public delegate void OnStartEventHandler(ContentScanner scanner);
        #endregion

        #region events
        /**
          <summary>Notifies the scan start.</summary>
        */
        public event OnStartEventHandler OnStart;
        #endregion

        #region types
        /**
          <summary>Graphics state [PDF:1.6:4.3].</summary>
        */
        public sealed class GraphicsState : ICloneable
        {
            #region dynamic
            #region fields
            private IList<BlendModeEnum> blendMode;
            private double charSpace;
            private SKMatrix ctm;
            private colors::Color fillColor;
            private colors::ColorSpace fillColorSpace;
            private fonts::Font font;
            private double fontSize;
            private double lead;
            private LineCapEnum lineCap;
            private LineDash lineDash;
            private LineJoinEnum lineJoin;
            private double lineWidth;
            private double miterLimit;
            private TextRenderModeEnum renderMode;
            private double rise;
            private double scale;
            private colors::Color strokeColor;
            private colors::ColorSpace strokeColorSpace;
            private SKMatrix tlm;
            private SKMatrix tm;
            private double wordSpace;

            private ContentScanner scanner;
            #endregion

            #region constructors
            internal GraphicsState(ContentScanner scanner)
            {
                this.scanner = scanner;
                Initialize();
            }
            #endregion

            #region interface
            #region public
            /**
              <summary>Gets a deep copy of the graphics state object.</summary>
            */
            public object Clone()
            {
                GraphicsState clone;
                {
                    // Shallow copy.
                    clone = (GraphicsState)MemberwiseClone();

                    // Deep copy.
                    /* NOTE: Mutable objects are to be cloned. */
                    clone.ctm = ctm;
                    clone.tlm = tlm;
                    clone.tm = tm;
                }
                return clone;
            }

            /**
              <summary>Copies this graphics state into the specified one.</summary>
              <param name="state">Target graphics state object.</param>
            */
            public void CopyTo(GraphicsState state)
            {
                state.blendMode = blendMode;
                state.charSpace = charSpace;
                state.ctm = ctm;
                state.fillColor = fillColor;
                state.fillColorSpace = fillColorSpace;
                state.font = font;
                state.fontSize = fontSize;
                state.lead = lead;
                state.lineCap = lineCap;
                state.lineDash = lineDash;
                state.lineJoin = lineJoin;
                state.lineWidth = lineWidth;
                state.miterLimit = miterLimit;
                state.renderMode = renderMode;
                state.rise = rise;
                state.scale = scale;
                state.strokeColor = strokeColor;
                state.strokeColorSpace = strokeColorSpace;
                //TODO:temporary hack (define TextState for textual parameters!)...
                if (state.scanner.Parent is Text)
                {
                    state.tlm = tlm;
                    state.tm = tm;
                }
                else
                {
                    state.tlm = SKMatrix.MakeIdentity();
                    state.tm = SKMatrix.MakeIdentity();
                }
                state.wordSpace = wordSpace;
            }

            /**
              <summary>Gets/Sets the current blend mode to be used in the transparent imaging model
              [PDF:1.6:5.2.1].</summary>
              <remarks>The application should use the first blend mode in the list that it recognizes.
              </remarks>
            */
            public IList<BlendModeEnum> BlendMode
            {
                get { return blendMode; }
                set { blendMode = value; }
            }

            /**
              <summary>Gets/Sets the current character spacing [PDF:1.6:5.2.1].</summary>
            */
            public double CharSpace
            {
                get { return charSpace; }
                set { charSpace = value; }
            }

            /**
              <summary>Gets/Sets the current transformation matrix.</summary>
            */
            public SKMatrix Ctm
            {
                get { return ctm; }
                set { ctm = value; }
            }

            /**
              <summary>Gets/Sets the current color for nonstroking operations [PDF:1.6:4.5.1].</summary>
            */
            public colors::Color FillColor
            {
                get { return fillColor; }
                set { fillColor = value; }
            }

            /**
              <summary>Gets/Sets the current color space for nonstroking operations [PDF:1.6:4.5.1].</summary>
            */
            public colors::ColorSpace FillColorSpace
            {
                get { return fillColorSpace; }
                set { fillColorSpace = value; }
            }

            /**
              <summary>Gets/Sets the current font [PDF:1.6:5.2].</summary>
            */
            public fonts::Font Font
            {
                get { return font; }
                set { font = value; }
            }

            /**
              <summary>Gets/Sets the current font size [PDF:1.6:5.2].</summary>
            */
            public double FontSize
            {
                get { return fontSize; }
                set { fontSize = value; }
            }

            /**
              <summary>Gets the initial current transformation matrix.</summary>
            */
            public SKMatrix GetInitialCtm()
            {
                SKMatrix initialCtm;
                if (Scanner.RenderContext == null) // Device-independent.
                {
                    initialCtm = SKMatrix.MakeIdentity(); // Identity.
                }
                //else if (true)//alternative
                //{
                //    IContentContext contentContext = Scanner.ContentContext;
                //    var canvasSize = Scanner.CanvasSize;
                //    var originalBox = contentContext.Box;
                //    var contentBox = originalBox.RotateRect(contentContext.Rotate);

                //    using (SKPath path = new SKPath())
                //    {
                //        path.AddRect(originalBox);
                //        var rotate = SKMatrix.MakeRotationDegrees(contentContext.Rotate);
                //        path.Transform(rotate);

                //        initialCtm = new SKMatrix { Values = new float[] { 1, 0, 0, 0, -1, canvasSize.Height, 0, 0, 1 } };
                //        SKMatrix.PreConcat(ref initialCtm, rotate);
                //        SKMatrix.PreConcat(ref initialCtm, SKMatrix.MakeTranslation(originalBox.Left - path.Bounds.Left, originalBox.Top - path.Bounds.Top));
                //        SKMatrix.PreConcat(ref initialCtm, SKMatrix.MakeScale(canvasSize.Width / contentBox.Width, canvasSize.Height / contentBox.Height));
                //        SKMatrix.PreConcat(ref initialCtm, SKMatrix.MakeTranslation(-contentBox.Left, -contentBox.Top));
                //    }
                //}
                else // Device-dependent.
                {
                    IContentContext contentContext = Scanner.ContentContext;
                    SKSize canvasSize = Scanner.CanvasSize;

                    // Axes orientation.
                    RotationEnum rotation = contentContext.Rotation;
                    switch (rotation)
                    {
                        case RotationEnum.Downward:
                            initialCtm = new SKMatrix { Values = new float[] { 1, 0, 0, 0, -1, canvasSize.Height, 0, 0, 1 } };
                            break;
                        case RotationEnum.Leftward:
                            initialCtm = new SKMatrix { Values = new float[] { 0, 1, 0, 1, 0, 0, 0, 0, 1 } };
                            break;
                        case RotationEnum.Upward:
                            initialCtm = new SKMatrix { Values = new float[] { -1, 0, canvasSize.Width, 0, 1, 0, 0, 0, 1 } };
                            break;
                        case RotationEnum.Rightward:
                            initialCtm = new SKMatrix { Values = new float[] { 0, -1, canvasSize.Width, -1, 0, canvasSize.Height, 0, 0, 1 } };
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    // Scaling.
                    SKRect contentBox = contentContext.Box;
                    SKSize rotatedCanvasSize = rotation.Transform(canvasSize);
                    SKMatrix.PreConcat(ref initialCtm, SKMatrix.MakeScale(
                       rotatedCanvasSize.Width / contentBox.Width,
                       rotatedCanvasSize.Height / contentBox.Height
                       ));

                    // Origin alignment.
                    SKMatrix.PreConcat(ref initialCtm, SKMatrix.MakeTranslation(-contentBox.Left, -contentBox.Top)); //TODO: verify minimum coordinates!
                }
                return initialCtm;
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
                SKMatrix.PreConcat(ref matrix, tm);
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
                    SKMatrix.PreConcat(ref matrix, ctm);
                    return matrix;
                }
                else
                {
                    return ctm;
                }
            }

            /**
              <summary>Gets/Sets the current leading [PDF:1.6:5.2.4].</summary>
            */
            public double Lead
            {
                get { return lead; }
                set { lead = value; }
            }

            /**
              <summary>Gets/Sets the current line cap style [PDF:1.6:4.3.2].</summary>
            */
            public LineCapEnum LineCap
            {
                get { return lineCap; }
                set { lineCap = value; }
            }

            /**
              <summary>Gets/Sets the current line dash pattern [PDF:1.6:4.3.2].</summary>
            */
            public LineDash LineDash
            {
                get { return lineDash; }
                set { lineDash = value; }
            }

            /**
              <summary>Gets/Sets the current line join style [PDF:1.6:4.3.2].</summary>
            */
            public LineJoinEnum LineJoin
            {
                get { return lineJoin; }
                set { lineJoin = value; }
            }

            /**
              <summary>Gets/Sets the current line width [PDF:1.6:4.3.2].</summary>
            */
            public double LineWidth
            {
                get { return lineWidth; }
                set { lineWidth = value; }
            }

            /**
              <summary>Gets/Sets the current miter limit [PDF:1.6:4.3.2].</summary>
            */
            public double MiterLimit
            {
                get { return miterLimit; }
                set { miterLimit = value; }
            }

            /**
              <summary>Gets/Sets the current text rendering mode [PDF:1.6:5.2.5].</summary>
            */
            public TextRenderModeEnum RenderMode
            {
                get { return renderMode; }
                set { renderMode = value; }
            }

            /**
              <summary>Gets/Sets the current text rise [PDF:1.6:5.2.6].</summary>
            */
            public double Rise
            {
                get { return rise; }
                set { rise = value; }
            }

            /**
              <summary>Gets/Sets the current horizontal scaling [PDF:1.6:5.2.3], normalized to 1.</summary>
            */
            public double Scale
            {
                get { return scale; }
                set { scale = value; }
            }

            /**
              <summary>Gets the scanner associated to this state.</summary>
            */
            public ContentScanner Scanner
            {
                get { return scanner; }
            }

            /**
              <summary>Gets/Sets the current color for stroking operations [PDF:1.6:4.5.1].</summary>
            */
            public colors::Color StrokeColor
            {
                get { return strokeColor; }
                set { strokeColor = value; }
            }

            /**
              <summary>Gets/Sets the current color space for stroking operations [PDF:1.6:4.5.1].</summary>
            */
            public colors::ColorSpace StrokeColorSpace
            {
                get { return strokeColorSpace; }
                set { strokeColorSpace = value; }
            }

            /**
              <summary>Gets/Sets the current text line matrix [PDF:1.6:5.3].</summary>
            */
            public SKMatrix Tlm
            {
                get { return tlm; }
                set { tlm = value; }
            }

            /**
              <summary>Gets/Sets the current text matrix [PDF:1.6:5.3].</summary>
            */
            public SKMatrix Tm
            {
                get { return tm; }
                set { tm = value; }
            }

            /**
              <summary>Gets/Sets the current word spacing [PDF:1.6:5.2.2].</summary>
            */
            public double WordSpace
            {
                get { return wordSpace; }
                set { wordSpace = value; }
            }

            public double HorizontalScale { get; set; }

            public bool RenderModeFill
            {
                get
                {
                    return RenderMode == TextRenderModeEnum.Fill
                        || RenderMode == TextRenderModeEnum.FillStroke
                        || RenderMode == TextRenderModeEnum.FillClip
                        || RenderMode == TextRenderModeEnum.FillStrokeClip;
                }
            }

            public bool RenderModeStroke
            {
                get
                {
                    return RenderMode == TextRenderModeEnum.Stroke
                        || RenderMode == TextRenderModeEnum.FillStroke
                        || RenderMode == TextRenderModeEnum.StrokeClip
                        || RenderMode == TextRenderModeEnum.FillStrokeClip;
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
                charSpace = 0;
                ctm = GetInitialCtm();
                fillColor = colors::DeviceGrayColor.Default;
                fillColorSpace = colors::DeviceGrayColorSpace.Default;
                font = null;
                fontSize = 0;
                lead = 0;
                lineCap = LineCapEnum.Butt;
                lineDash = new LineDash();
                lineJoin = LineJoinEnum.Miter;
                lineWidth = 1;
                miterLimit = 10;
                renderMode = TextRenderModeEnum.Fill;
                rise = 0;
                scale = 1;
                strokeColor = colors::DeviceGrayColor.Default;
                strokeColorSpace = colors::DeviceGrayColorSpace.Default;
                tlm = SKMatrix.MakeIdentity();
                tm = SKMatrix.MakeIdentity();
                wordSpace = 0;

                // Rendering context initialization.
                var renderContext = Scanner.RenderContext;
                renderContext?.SetMatrix(ctm);
            }
            #endregion
            #endregion
            #endregion
        }

        public abstract class GraphicsObjectWrapper
        {
            #region static
            internal static GraphicsObjectWrapper Get(ContentScanner scanner)
            {
                ContentObject obj = scanner.Current;
                if (obj is ShowText)
                    return new TextStringWrapper(scanner);
                else if (obj is Text)
                    return new TextWrapper(scanner);
                else if (obj is XObject)
                    return new XObjectWrapper(scanner);
                else if (obj is InlineImage)
                    return new InlineImageWrapper(scanner);
                else
                    return null;
            }
            #endregion

            #region dynamic
            #region fields
            protected SKRect? box;
            #endregion

            #region interface
            #region public
            /**
              <summary>Gets the object's bounding box.</summary>
            */
            public virtual SKRect? Box
            {
                get { return box; }
            }
            #endregion
            #endregion
            #endregion
        }

        /**
          <summary>Object information.</summary>
          <remarks>
            <para>This class provides derivative (higher-level) information
            about the currently scanned object.</para>
          </remarks>
        */
        public abstract class GraphicsObjectWrapper<TDataObject> : GraphicsObjectWrapper where TDataObject : ContentObject
        {
            #region dynamic
            #region fields
            private TDataObject baseDataObject;
            #endregion

            #region constructors
            protected GraphicsObjectWrapper(TDataObject baseDataObject)
            {
                this.baseDataObject = baseDataObject;
            }
            #endregion

            #region interface
            #region public
            /**
              <summary>Gets the underlying data object.</summary>
            */
            public TDataObject BaseDataObject
            {
                get { return baseDataObject; }
            }
            #endregion
            #endregion
            #endregion
        }

        /**
          <summary>Inline image information.</summary>
        */
        public sealed class InlineImageWrapper : GraphicsObjectWrapper<InlineImage>
        {
            internal InlineImageWrapper(ContentScanner scanner) : base((InlineImage)scanner.Current)
            {
                SKMatrix ctm = scanner.State.Ctm;
                this.box = SKRect.Create(
                  ctm.TransX,
                  scanner.ContextSize.Height - ctm.TransY,
                  ctm.ScaleX,
                  Math.Abs(ctm.ScaleY)
                  );
            }

            /**
              <summary>Gets the inline image.</summary>
            */
            public InlineImage InlineImage
            { get { return BaseDataObject; } }
        }

        /**
          <summary>Text information.</summary>
        */
        public sealed class TextWrapper : GraphicsObjectWrapper<Text>
        {
            private List<TextStringWrapper> textStrings;

            internal TextWrapper(ContentScanner scanner) : base((Text)scanner.Current)
            {
                textStrings = new List<TextStringWrapper>();
                Extract(scanner.ChildLevel);
            }

            public override SKRect? Box
            {
                get
                {
                    if (box == null)
                    {
                        foreach (TextStringWrapper textString in textStrings)
                        {
                            if (!box.HasValue)
                            { box = textString.Box; }
                            else
                            { box = SKRect.Union(box.Value, textString.Box.Value); }
                        }
                    }
                    return box;
                }
            }

            public string Text
            {
                get
                {
                    var textBuilder = new StringBuilder();
                    foreach (TextStringWrapper textString in textStrings)
                    {
                        textBuilder.Append(textString.Text);
                    }
                    return textBuilder.ToString();
                }
            }

            /**
              <summary>Gets the text strings.</summary>
            */
            public List<TextStringWrapper> TextStrings
            { get { return textStrings; } }

            public override string ToString()
            { return Text; }

            private void Extract(ContentScanner level)
            {
                if (level == null)
                    return;
                level.MoveStart();
                while (level.MoveNext())
                {
                    ContentObject content = level.Current;
                    if (content is ShowText)
                    { textStrings.Add((TextStringWrapper)level.CurrentWrapper); }
                    else if (content is ContainerObject)
                    { Extract(level.ChildLevel); }
                }
            }
        }

        /**
          <summary>Text string information.</summary>
        */
        public sealed class TextStringWrapper : GraphicsObjectWrapper<ShowText>, ITextString
        {
            private class ShowTextScanner : ShowText.IScanner
            {
                TextStringWrapper wrapper;

                internal ShowTextScanner(TextStringWrapper wrapper)
                { this.wrapper = wrapper; }

                public void ScanChar(char textChar, SKRect textCharBox)
                {
                    wrapper.textChars.Add(
                      new TextChar(
                        textChar,
                        textCharBox,
                        wrapper.style,
                        false
                        )
                      );
                }
            }

            private TextStyle style;
            private List<TextChar> textChars;

            internal TextStringWrapper(ContentScanner scanner) : base((ShowText)scanner.Current)
            {
                textChars = new List<TextChar>();
                {
                    GraphicsState state = scanner.State;
                    style = new TextStyle(
                      state.Font,
                      state.FontSize * state.Tm.ScaleY,
                      state.RenderMode,
                      state.StrokeColor,
                      state.StrokeColorSpace,
                      state.FillColor,
                      state.FillColorSpace,
                      state.Scale * state.Tm.ScaleX,
                      state.Tm.ScaleY
                      );
                    BaseDataObject.Scan(
                      state,
                      new ShowTextScanner(this)
                      );
                }
            }

            public override SKRect? Box
            {
                get
                {
                    if (box == null)
                    {
                        foreach (TextChar textChar in textChars)
                        {
                            if (!box.HasValue)
                            { box = textChar.Box; }
                            else
                            { box = SKRect.Union(box.Value, textChar.Box); }
                        }
                    }
                    return box;
                }
            }

            /**
              <summary>Gets the text style.</summary>
            */
            public TextStyle Style
            { get { return style; } }

            public String Text
            {
                get
                {
                    StringBuilder textBuilder = new StringBuilder();
                    foreach (TextChar textChar in textChars)
                    { textBuilder.Append(textChar); }
                    return textBuilder.ToString();
                }
            }

            public List<TextChar> TextChars
            { get { return textChars; } }

            public override string ToString()
            { return Text; }
        }

        /**
          <summary>External object information.</summary>
        */
        public sealed class XObjectWrapper : GraphicsObjectWrapper<XObject>
        {
            private PdfName name;
            private xObjects::XObject xObject;

            internal XObjectWrapper(ContentScanner scanner) : base((XObject)scanner.Current)
            {
                SKMatrix ctm = scanner.State.Ctm;
                this.box = SKRect.Create(
                  ctm.TransX,
                  scanner.ContextSize.Height - ctm.TransY,
                  ctm.ScaleX,
                  Math.Abs(ctm.ScaleY)
                  );
                this.name = BaseDataObject.Name;
                this.xObject = BaseDataObject.GetResource(scanner.ContentContext);
            }

            /**
              <summary>Gets the corresponding resource key.</summary>
            */
            public PdfName Name
            { get { return name; } }

            /**
              <summary>Gets the external object.</summary>
            */
            public xObjects::XObject XObject
            { get { return xObject; } }
        }
        #endregion

        #region static
        #region fields
        private static readonly int StartIndex = -1;
        #endregion
        #endregion

        #region dynamic
        #region fields
        /**
          Child level.
        */
        private ContentScanner childLevel;
        /**
          Content objects collection.
        */
        private Contents contents;
        /**
          Current object index at this level.
        */
        private int index = 0;
        /**
          Object collection at this level.
        */
        private IList<ContentObject> objects;
        /**
          Parent level.
        */
        private ContentScanner parentLevel;
        /**
          Current graphics state.
        */
        private GraphicsState state;

        /**
          Rendering context.
        */
        private SKCanvas renderContext;
        /**
          Rendering object.
        */
        private SKPath renderObject;

        /**
          <summary>Size of the graphics canvas.</summary>
          <remarks>According to the current processing (whether it is device-independent scanning or
          device-based rendering), it may be expressed, respectively, in user-space units or in
          device-space units.</remarks>
        */
        private SKSize canvasSize;
        /**
          <summary>Device-independent size of the graphics canvas.</summary>
        */
        private SKSize contextSize;
        #endregion

        #region constructors
        /**
          <summary>Instantiates a top-level content scanner.</summary>
          <param name="contents">Content objects collection to scan.</param>
        */

        public ContentScanner(Contents contents)
        {
            parentLevel = null;
            objects = this.contents = contents;

            canvasSize = contextSize = contents.ContentContext.Box.Size;

            MoveStart();
        }

        /**
          <summary>Instantiates a top-level content scanner.</summary>
          <param name="contentContext">Content context containing the content objects collection to scan.</param>
        */
        public ContentScanner(IContentContext contentContext) : this(contentContext.Contents)
        { }

        /**
          <summary>Instantiates a child-level content scanner for <see cref="org.pdfclown.documents.contents.xObjects.FormXObject">external form</see>.</summary>
          <param name="formXObject">External form.</param>
          <param name="parentLevel">Parent scan level.</param>
        */
        public ContentScanner(xObjects::FormXObject formXObject, ContentScanner parentLevel)
        {
            this.parentLevel = parentLevel;
            this.objects = this.contents = formXObject.Contents;

            canvasSize = contextSize = parentLevel.contextSize;

            OnStart += delegate (ContentScanner scanner)
            {
                // Adjust the initial graphics state to the external form context!
                var ctm = scanner.State.Ctm;
                SKMatrix.PreConcat(ref ctm, formXObject.Matrix);
                scanner.State.Ctm = ctm;
                /*
                  TODO: On rendering, clip according to the form dictionary's BBox entry!
                */
            };
            MoveStart();
        }

        /**
          <summary>Instantiates a child-level content scanner.</summary>
          <param name="parentLevel">Parent scan level.</param>
        */
        private ContentScanner(ContentScanner parentLevel)
        {
            this.parentLevel = parentLevel;
            this.contents = parentLevel.contents;
            this.objects = ((CompositeObject)parentLevel.Current).Objects;

            canvasSize = contextSize = parentLevel.contextSize;

            MoveStart();
        }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the size of the current imageable area.</summary>
          <remarks>It can be either the user-space area (dry scanning) or the device-space area (wet
          scanning).</remarks>
        */
        public SKSize CanvasSize
        {
            get { return canvasSize; }
        }

        /**
          <summary>Gets the current child scan level.</summary>
        */
        public ContentScanner ChildLevel
        {
            get { return childLevel; }
        }

        /**
          <summary>Gets the content context associated to the content objects collection.</summary>
        */
        public IContentContext ContentContext
        {
            get { return contents.ContentContext; }
        }

        /**
          <summary>Gets the content objects collection this scanner is inspecting.</summary>
        */
        public Contents Contents
        {
            get { return contents; }
        }

        /**
          <summary>Gets the size of the current imageable area in user-space units.</summary>
        */
        public SKSize ContextSize
        {
            get { return contextSize; }
        }

        /**
          <summary>Gets/Sets the current content object.</summary>
        */
        public ContentObject Current
        {
            get
            {
                if (index < 0 || index >= objects.Count)
                    return null;

                return objects[index];
            }
            set
            {
                objects[index] = value;
                Refresh();
            }
        }

        /**
          <summary>Gets the current content object's information.</summary>
        */
        public GraphicsObjectWrapper CurrentWrapper
        {
            get { return GraphicsObjectWrapper.Get(this); }
        }

        /**
          <summary>Gets the current position.</summary>
        */
        public int Index
        {
            get { return index; }
        }

        /**
          <summary>Inserts a content object at the current position.</summary>
        */
        public void Insert(ContentObject obj)
        {
            if (index == -1)
            { index = 0; }

            objects.Insert(index, obj);
            Refresh();
        }

        /**
          <summary>Inserts content objects at the current position.</summary>
          <remarks>After the insertion is complete, the lastly-inserted content object is at the current position.</remarks>
        */
        public void Insert<T>(ICollection<T> objects) where T : ContentObject
        {
            int index = 0;
            int count = objects.Count;
            foreach (ContentObject obj in objects)
            {
                Insert(obj);

                if (++index < count)
                { MoveNext(); }
            }
        }

        /**
          <summary>Gets whether this level is the root of the hierarchy.</summary>
        */
        public bool IsRootLevel()
        { return parentLevel == null; }

        /**
          <summary>Moves to the object at the given position.</summary>
          <param name="index">New position.</param>
          <returns>Whether the object was successfully reached.</returns>
        */
        public bool Move(int index)
        {
            if (this.index > index)
            { MoveStart(); }

            while (this.index < index
              && MoveNext()) ;

            return Current != null;
        }

        /**
          <summary>Moves after the last object.</summary>
        */
        public void MoveEnd()
        { MoveLast(); MoveNext(); }

        /**
          <summary>Moves to the first object.</summary>
          <returns>Whether the first object was successfully reached.</returns>
        */
        public bool MoveFirst()
        { MoveStart(); return MoveNext(); }

        /**
          <summary>Moves to the last object.</summary>
          <returns>Whether the last object was successfully reached.</returns>
        */
        public bool MoveLast()
        {
            int lastIndex = objects.Count - 1;
            while (index < lastIndex)
            { MoveNext(); }

            return Current != null;
        }

        /**
          <summary>Moves to the next object.</summary>
          <returns>Whether the next object was successfully reached.</returns>
        */
        public bool MoveNext()
        {
            // Scanning the current graphics state...
            ContentObject currentObject = Current;
            if (currentObject != null)
            { currentObject.Scan(state); }

            // Moving to the next object...
            if (index < objects.Count)
            { index++; Refresh(); }

            return Current != null;
        }

        /**
          <summary>Moves before the first object.</summary>
        */
        public void MoveStart()
        {
            index = StartIndex;
            if (state == null)
            {
                if (parentLevel == null)
                { state = new GraphicsState(this); }
                else
                { state = parentLevel.state.Clone(this); }
            }
            else
            {
                if (parentLevel == null)
                { state.Initialize(); }
                else
                { parentLevel.state.CopyTo(state); }
            }

            NotifyStart();

            Refresh();
        }

        /**
          <summary>Gets the current parent object.</summary>
        */
        public CompositeObject Parent
        {
            get { return (parentLevel == null ? null : parentLevel.Current as CompositeObject); }
        }

        /**
          <summary>Gets the parent scan level.</summary>
        */
        public ContentScanner ParentLevel
        {
            get { return parentLevel; }
        }

        /**
          <summary>Removes the content object at the current position.</summary>
          <returns>Removed object.</returns>
        */
        public ContentObject Remove()
        {
            ContentObject removedObject = Current; objects.RemoveAt(index);
            Refresh();

            return removedObject;
        }

        /**
          <summary>Renders the contents into the specified context.</summary>
          <param name="renderContext">Rendering context.</param>
          <param name="renderSize">Rendering canvas size.</param>
        */
        public void Render(SKCanvas renderContext, SKSize renderSize)
        {
            Render(renderContext, renderSize, null);
        }

        /**
          <summary>Renders the contents into the specified object.</summary>
          <param name="renderContext">Rendering context.</param>
          <param name="renderSize">Rendering canvas size.</param>
          <param name="renderObject">Rendering object.</param>
        */
        public void Render(SKCanvas renderContext, SKSize renderSize, SKPath renderObject)
        {
            if (IsRootLevel() && renderContext != null)
            {
                renderContext.ClipRect(SKRect.Create(renderSize));
                renderContext.Clear(SKColors.White);
            }

            try
            {
                this.renderContext = renderContext;
                this.canvasSize = renderSize;
                this.renderObject = renderObject;

                // Scan this level for rendering!
                MoveStart();
                while (MoveNext()) ;
            }
            finally
            {
                this.renderContext = null;
                this.canvasSize = contextSize;
                this.renderObject = null;
            }
        }

        /**
          <summary>Gets the rendering context.</summary>
          <returns><code>null</code> in case of dry scanning.</returns>
        */
        public SKCanvas RenderContext
        {
            get { return renderContext; }
            internal set { renderContext = value; }
        }

        /**
          <summary>Gets the rendering object.</summary>
          <returns><code>null</code> in case of scanning outside a shape.</returns>
        */
        public SKPath RenderObject
        {
            get { return renderObject; }
        }

        /**
          <summary>Gets the root scan level.</summary>
        */
        public ContentScanner RootLevel
        {
            get
            {
                ContentScanner level = this;
                while (true)
                {
                    ContentScanner parentLevel = level.ParentLevel;
                    if (parentLevel == null)
                        return level;

                    level = parentLevel;
                }
            }
        }

        /**
          <summary>Gets the current graphics state applied to the current content object.</summary>
        */
        public GraphicsState State
        {
            get { return state; }
        }
        #endregion

        #region protected
#pragma warning disable 0628
        /**
          <summary>Notifies the scan start to listeners.</summary>
        */
        protected void NotifyStart()
        {
            if (OnStart != null)
            { OnStart(this); }
        }
#pragma warning restore 0628
        #endregion

        #region private
        /**
          <summary>Synchronizes the scanner state.</summary>
        */
        private void Refresh()
        {
            if (Current is CompositeObject)
            { childLevel = new ContentScanner(this); }
            else
            { childLevel = null; }
        }
        #endregion
        #endregion
        #endregion
    }
}