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

using PdfClown.Bytes;
using colors = PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.Objects;
using xObjects = PdfClown.Documents.Contents.XObjects;
using PdfClown.Files;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Contents.Scanner;

namespace PdfClown.Documents.Contents
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
    public sealed partial class ContentScanner
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
        #endregion

        #region static
        #region fields
        private static readonly int StartIndex = -1;
        private static readonly SKPaint paintWhiteBackground = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill };

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
        private ContentWrapper contents;
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

        public ContentScanner(ContentWrapper contents)
        {
            parentLevel = null;
            objects =
                this.contents = contents;

            canvasSize =
                contextSize = contents.ContentContext.Box.Size;

            MoveStart();
        }

        /**
          <summary>Instantiates a top-level content scanner.</summary>
          <param name="contentContext">Content context containing the content objects collection to scan.</param>
        */
        public ContentScanner(IContentContext contentContext) : this(contentContext.Contents)
        { }

        /**
          <summary>Instantiates a child-level content scanner for <see cref="PdfClown.Documents.Contents.XObjects.FormXObject">external form</see>.</summary>
          <param name="formXObject">External form.</param>
          <param name="parentLevel">Parent scan level.</param>
        */
        public ContentScanner(xObjects::FormXObject formXObject, ContentScanner parentLevel)
        {
            this.parentLevel = parentLevel;
            objects =
                contents = formXObject.Contents;

            canvasSize =
                contextSize = parentLevel.contextSize;

            renderContext = parentLevel.RenderContext;
            MoveStart();
        }

        public ContentScanner(xObjects::FormXObject formXObject, SKCanvas canvas, SKSize size)
        {
            objects =
                contents = formXObject.Contents;

            canvasSize =
                contextSize = size;

            renderContext = canvas;
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
        public SKSize CanvasSize => canvasSize;

        /**
          <summary>Gets the current child scan level.</summary>
        */
        public ContentScanner ChildLevel => childLevel;

        /**
          <summary>Gets the content context associated to the content objects collection.</summary>
        */
        public IContentContext ContentContext => contents.ContentContext;

        /**
          <summary>Gets the content objects collection this scanner is inspecting.</summary>
        */
        public ContentWrapper Contents => contents;

        /**
          <summary>Gets the size of the current imageable area in user-space units.</summary>
        */
        public SKSize ContextSize => contextSize;

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
        public GraphicsObjectWrapper CurrentWrapper => GraphicsObjectWrapper.Get(this);

        /**
          <summary>Gets the current position.</summary>
        */
        public int Index => index;

        /**
         <summary>Gets the current parent object.</summary>
       */
        public CompositeObject Parent => parentLevel?.Current as CompositeObject;

        /**
          <summary>Gets the parent scan level.</summary>
        */
        public ContentScanner ParentLevel => parentLevel;

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
            if (IsRootLevel() && ClearContext)
            {
                renderContext.ClipRect(SKRect.Create(renderSize));
                renderContext.DrawRect(SKRect.Create(renderSize), paintWhiteBackground);
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
            get => renderContext;
            internal set => renderContext = value;
        }

        /**
          <summary>Gets the rendering object.</summary>
          <returns><code>null</code> in case of scanning outside a shape.</returns>
        */
        public SKPath RenderObject => renderObject;

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
        public GraphicsState State => state;

        public bool ClearContext { get; set; } = true;
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