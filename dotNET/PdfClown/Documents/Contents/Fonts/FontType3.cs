/*
  Copyright 2010-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Util;
using PdfClown.Util.Math.Geom;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PdfClown.Documents.Contents.Fonts
{
    /**
      <summary>Type 3 font [PDF:1.6:5.5.4].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class FontType3 : FontSimple
    {
        private Resources resources;
        private PdfDictionary charProcs;
        private SKMatrix? fontMatrix;
        private SKRect? fontBBox;

        internal FontType3(Document context) : base(context)
        { }

        internal FontType3(PdfDirectObject baseObject) : base(baseObject)
        {
            ReadEncoding();
        }

        public override string Name
        {
            get => Dictionary.GetString(PdfName.Name);
        }

        protected override void ReadEncoding()
        {
            var encodingBase = EncodingData;
            if (encodingBase is PdfName encodingName)
            {
                encoding = Encoding.Get(encodingName);
                if (encoding == null)
                {
                    Debug.WriteLine($"warn: Unknown encoding: {encodingName}");
                }
            }
            else if (encodingBase is PdfDictionary dictionary)
            {
                encoding = new DictionaryEncoding(dictionary);
            }
            glyphList = GlyphMapping.Default;
        }

        protected override Encoding ReadEncodingFromFont()
        {
            // Type 3 fonts do not have a built-in encoding
            throw new NotSupportedException("not supported for Type 3 fonts");
        }

        protected override bool? FontSymbolic
        {
            get => false;
        }

        public override BaseFont Font
        {
            // Type 3 fonts do not use FontBox fonts
            get => throw new NotSupportedException("not supported for Type 3 fonts");
        }

        public override bool IsEmbedded
        {
            get => true;
        }

        public override SKMatrix FontMatrix
        {
            get => fontMatrix ??= Dictionary.GetArray(PdfName.FontMatrix) is PdfArray array && array.Count > 5
                            ? new SKMatrix(
                                array.GetFloat(0), array.GetFloat(1), array.GetFloat(4),
                                array.GetFloat(2), array.GetFloat(3), array.GetFloat(5),
                                0, 0, 1)
                            : base.FontMatrix;
        }

        public override bool IsDamaged
        {
            // there's no font file to load
            get => false;
        }

        public override bool IsStandard14
        {
            get => false;
        }

        /**
         * Returns the optional resources of the type3 stream.
         *
         * @return the resources bound to be used when parsing the type3 stream
         */
        public Resources Resources
        {
            get => resources ??= Wrap<Resources>(Dictionary[PdfName.Resources]);
        }

        public override SKRect BoundingBox
        {
            get => fontBBox ??= GenerateBoundingBox();
        }

        /**
         * Returns the dictionary containing all streams to be used to render the glyphs.
         * 
         * @return the dictionary containing all glyph streams.
         */
        public PdfDictionary CharProcs
        {
            get
            {
                if (charProcs == null)
                {
                    charProcs = (PdfDictionary)Dictionary.Resolve(PdfName.CharProcs);
                }
                return charProcs;
            }
        }

        public override SKPath GetPath(int code)
        {
            throw new NotSupportedException("not supported for Type 3 fonts");
        }

        public override SKPath GetPath(string name)
        {
            // Type 3 fonts do not use vector paths
            throw new NotSupportedException("not supported for Type 3 fonts");
        }

        public override SKPath GetNormalizedPath(int code)
        {
            throw new NotSupportedException("not supported for Type 3 fonts");
        }

        public override bool HasGlyph(int code)
        {
            string name = Encoding.GetName(code);
            return GetCharProc(PdfName.Get(name)) != null;
        }

        public override bool HasGlyph(string name)
        {
            return GetCharProc(PdfName.Get(name)) != null;
        }

        public override SKPoint GetDisplacement(int code)
        {
            return FontMatrix.MapVector(base.GetWidth(code), 0);
        }

        public override double GetScalingFactor(double size)
        {
            return base.GetScalingFactor(size);
        }

        public override float GetWidth(int code)
        {
            int firstChar = FirstChar ?? -1;
            int lastChar = LastChar ?? -1;
            if (Widths.Count > 0 && code >= firstChar && code <= lastChar)
            {
                return Widths.GetNFloat(code - firstChar) ?? 0F;
            }
            else
            {
                return FontDescriptor?.MissingWidth ?? GetWidthFromFont(code);
            }
        }

        public override float GetWidthFromFont(int code)
        {
            Type3CharProc charProc = GetCharProc(code);
            if (charProc == null
                || (charProc.Contents?.Count ?? 0) == 0)
            {
                return 0;
            }
            return charProc.Width ?? 0;
        }

        public override float GetHeight(int code)
        {
            var desc = FontDescriptor;
            if (desc != null)
            {
                // the following values are all more or less accurate at least all are average
                // values. Maybe we'll find another way to get those value for every single glyph
                // in the future if needed
                var bbox = desc.FontBBox;
                float retval = 0;
                if (bbox != null)
                {
                    retval = (float)bbox.Height / 2;
                }
                if (retval.CompareTo(0) == 0)
                {
                    retval = desc.CapHeight ?? 0;
                }
                if (retval.CompareTo(0) == 0)
                {
                    retval = desc.Ascent;
                }
                if (retval.CompareTo(0) == 0)
                {
                    retval = desc.XHeight ?? 0;
                    if (retval > 0)
                    {
                        retval -= desc.Descent;
                    }
                }
                return retval;
            }
            return 0;
        }

        public override int GetBytesCount(int code) => 1;

        public override void Encode(Span<byte> bytes, int unicode)
        {
            throw new NotSupportedException("Not implemented: Type3");
        }

        public override int ReadCode(IInputStream input, out ReadOnlySpan<byte> bytes)
        {
            bytes = input.ReadSpan(1);
            return ReadCode(bytes);
        }

        public override int ReadCode(ReadOnlySpan<byte> bytes)
        {
            return bytes[0];
        }

        private SKRect GenerateBoundingBox()
        {
            var rect = FontBBox?.ToRect() ?? SKRect.Empty;
            if (rect.Width == 0 || rect.Height == 0)
            {
                // Plan B: get the max bounding box of the glyphs
                PdfDictionary cp = CharProcs;
                foreach (PdfName name in cp.Keys)
                {
                    var charProc = GetCharProc(name);
                    if (charProc != null)
                    {
                        try
                        {
                            var glyphBBox = charProc.GlyphBox;
                            if (glyphBBox == null)
                            {
                                continue;
                            }
                            rect.Left = Math.Min(rect.Left, glyphBBox.Value.Left);
                            rect.Top = Math.Min(rect.Top, glyphBBox.Value.Top);
                            rect.Right = Math.Max(rect.Right, glyphBBox.Value.Right);
                            rect.Bottom = Math.Max(rect.Bottom, glyphBBox.Value.Bottom);
                        }
                        catch (Exception ex)
                        {
                            // ignore
                            Debug.WriteLine($"debug: error getting the glyph bounding box - font bounding box will be used {ex}");
                        }
                    }
                }
            }
            return rect;
        }

        /**
         * Returns the stream of the glyph for the given character code
         * 
         * @param code character code
         * @return the stream to be used to render the glyph
         */
        public Type3CharProc GetCharProc(int code)
        {
            if (Encoding == null)
                return null;
            string name = Encoding.GetName(code);
            return GetCharProc(PdfName.Get(name));
        }

        private Type3CharProc GetCharProc(PdfName name)
        {
            var baseObject = CharProcs?[name];
            return Type3CharProc.Wrap(baseObject, this);
        }

        public override SKPath DrawChar(SKCanvas context, SKPaint fill, SKPaint stroke, char textChar, int code, ReadOnlySpan<byte> codeBytes)
        {
            var proc = GetCharProc(code);
            if (proc == null)
            {
                Debug.WriteLine($"info: no Glyph for Code: {code}  Char: '{textChar}'");
                return null;
            }
            var picture = proc.Render();
            context.DrawPicture(picture, fill ?? stroke);
            return null;
        }
    }
}