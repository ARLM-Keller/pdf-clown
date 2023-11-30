/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Documents.Interchange.Metadata;
using PdfClown.Objects;
using PdfClown.Util;
using PdfClown.Util.Parsers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PdfClown.Documents.Contents.Fonts
{

    /**
     * A Type 3 character procedure. This is a standalone PDF content stream.
     *
     * @author John Hewson
     */
    public sealed class Type3CharProc : PdfObjectWrapper<PdfStream>, IContentContext
    {
        public static Type3CharProc Wrap(PdfDirectObject baseObject, FontType3 font)
        {
            if (baseObject == null)
                return null;
            if (baseObject.Wrapper is Type3CharProc charProc)
                return charProc;
            if (baseObject is PdfReference pdfReference
                && pdfReference.DataObject?.Wrapper is Type3CharProc referenceCharProc)
            {
                baseObject.Wrapper = referenceCharProc;
                return referenceCharProc;
            }
            return new Type3CharProc(font, baseObject);
        }

        private readonly FontType3 font;
        private SKPicture picture;
        private Stack<GraphicsState> states;

        public Type3CharProc(FontType3 font, PdfDirectObject charStream)
            : base(charStream)
        {
            this.font = font;
        }

        public FontType3 Font
        {
            get => font;
        }

        public ContentWrapper Contents => ContentWrapper.Wrap(BaseObject, this);

        public SKMatrix Matrix
        {
            get => font.FontMatrix;
        }

        public Resources Resources
        {
            get
            {
                if (BaseDataObject.Header.GetDictionary(PdfName.Resources) is PdfDictionary resourceDictionary)
                {
                    // PDFBOX-5294
                    Debug.WriteLine("warn: Using resources dictionary found in charproc entry");
                    Debug.WriteLine("warn: This should have been in the font or in the page dictionary");
                    return Wrap<Resources>(resourceDictionary);
                }
                return font.Resources;
            }
        }

        public SKRect FontBBox
        {
            get => font.BoundingBox;
        }

        /**
		 * Calculate the bounding box of this glyph. This will work only if the first operator in the
		 * stream is d1.
		 *
		 * @return the bounding box of this glyph, or null if the first operator is not d1.
		 * @throws IOException If an io error occurs while parsing the stream.
		 */
        public SKRect Box
        {
            get => GlyphBox ?? FontBBox;
        }

        public SKRect? GlyphBox
        {
            get => Contents.OfType<CharProcBBox>().FirstOrDefault()?.BBox;
        }

        /**
		 * Get the width from a type3 charproc stream.
		 *
		 * @return the glyph width.
		 * @throws IOException if the stream could not be read, or did not have d0 or d1 as first
		 * operator, or if their first argument was not a number.
		 */
        public float? Width
        {
            get => (float?)(Contents.OfType<CharProcWidth>().FirstOrDefault()?.WX ?? Contents.OfType<CharProcBBox>().FirstOrDefault()?.WX);
        }

        public RotationEnum Rotation => RotationEnum.Downward;

        public int Rotate => 0;

        public SKMatrix RotateMatrix
        {
            get => SKMatrix.Identity;
        }

        public List<ITextString> Strings { get; set; }

        public TransparencyXObject Group { get => null; }

        public AppDataCollection AppData => null;

        public DateTime? ModificationDate => null;

        public Stack<GraphicsState> GetGraphicsStateContext() => states ??= new Stack<GraphicsState>();

        public void Render(SKCanvas context, SKSize size, bool clearContext = true)
        {
            var scanner = new ContentScanner(Contents)
            {
                ClearContext = clearContext
            };
            scanner.Render(context, size);
        }

        public SKPicture Render()
        {
            if (picture != null)
                return picture;
            var box = Box;
            using (var recorder = new SKPictureRecorder())
            using (var canvas = recorder.BeginRecording(box))
            {
                Render(canvas, box.Size, false);
                return picture = recorder.EndRecording();
            }
        }

        public AppData GetAppData(PdfName appName)
        {
            throw new NotSupportedException();
        }

        public void Touch(PdfName appName)
        {
            throw new NotSupportedException();
        }

        public void Touch(PdfName appName, DateTime modificationDate)
        {
            throw new NotSupportedException();
        }

        public ContentObject ToInlineObject(PrimitiveComposer composer)
        {
            throw new NotImplementedException();
        }

        public XObjects.XObject ToXObject(Document context)
        {
            throw new NotImplementedException();
        }
    }
}
