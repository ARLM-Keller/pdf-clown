/*
  Copyright 2009-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using PdfClown.Documents;
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Contents.Scanner;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Documents.Functions;
using PdfClown.Objects;

using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Contents
{
    /**
      <summary>Graphics state parameters [PDF:1.6:4.3.4].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class ExtGState : PdfObjectWrapper<PdfDictionary>
    {
        internal static readonly IList<BlendModeEnum> DefaultBlendMode = new BlendModeEnum[0];

        public ExtGState(Document context) : base(context, new PdfDictionary())
        { }

        public ExtGState(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets/Sets whether the current soft mask and alpha constant are to be interpreted as
          shape values instead of opacity values.</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public bool AlphaShape
        {
            get => BaseDataObject.GetBool(PdfName.AIS, false);
            set => BaseDataObject.SetBool(PdfName.AIS, value);
        }

        public void ApplyTo(GraphicsState state)
        {
            foreach (PdfName parameterName in BaseDataObject.Keys)
            {
                if (parameterName.Equals(PdfName.Font))
                {
                    state.Font = Font;
                    state.FontSize = FontSize.Value;
                }
                else if (parameterName.Equals(PdfName.CA))
                {
                    if (!AlphaShape)
                        state.StrokeAlpha = StrokeAlpha;
                }
                else if (parameterName.Equals(PdfName.ca))
                {
                    if (!AlphaShape)
                        state.FillAlpha = FillAlpha;
                }
                else if (parameterName.Equals(PdfName.AIS))
                { state.AlphaIsShape = AlphaShape; }
                else if (parameterName.Equals(PdfName.LC))
                { state.LineCap = LineCap.Value; }
                else if (parameterName.Equals(PdfName.D))
                { state.LineDash = LineDash; }
                else if (parameterName.Equals(PdfName.LJ))
                { state.LineJoin = LineJoin.Value; }
                else if (parameterName.Equals(PdfName.LW))
                { state.LineWidth = LineWidth.Value; }
                else if (parameterName.Equals(PdfName.ML))
                { state.MiterLimit = MiterLimit.Value; }
                else if (parameterName.Equals(PdfName.BM))
                { state.BlendMode = BlendMode; }
                else if (parameterName.Equals(PdfName.Type))
                { }
                else if (parameterName.Equals(PdfName.SMask))
                {
                    state.SMask = SMask;
                }
                else if (parameterName.Equals(PdfName.TK))
                {
                    state.Knockout = BaseDataObject.GetBool(PdfName.TK);
                }
                else if (parameterName.Equals(PdfName.BG))
                {
                    state.Function = BG;
                }
                else if (parameterName.Equals(PdfName.BG2))
                {
                    state.Function = BG2;
                }
                else
                {

                }
                //TODO:extend supported parameters!!!
            }
        }

        [PDF(VersionEnum.PDF14)]
        public SoftMask SMask
        {
            get => SoftMask.WrapSoftMask(BaseDataObject[PdfName.SMask]);
            set => BaseDataObject[PdfName.SMask] = value.BaseObject;
        }


        /**
          <summary>Gets/Sets the blend mode to be used in the transparent imaging model [PDF:1.7:7.2.4].
          </summary>
        */
        [PDF(VersionEnum.PDF14)]
        public IList<BlendModeEnum> BlendMode
        {
            get
            {
                PdfDirectObject blendModeObject = BaseDataObject[PdfName.BM];
                if (blendModeObject == null)
                    return DefaultBlendMode;

                IList<BlendModeEnum> blendMode = new List<BlendModeEnum>();
                if (blendModeObject is PdfName name)
                { blendMode.Add(BlendModeEnumExtension.Get(name).Value); }
                else // MUST be an array.
                {
                    foreach (PdfDirectObject alternateBlendModeObject in (PdfArray)blendModeObject)
                    { blendMode.Add(BlendModeEnumExtension.Get((PdfName)alternateBlendModeObject).Value); }
                }
                return blendMode;
            }
            set
            {
                PdfDirectObject blendModeObject;
                if (value == null || value.Count == 0)
                { blendModeObject = null; }
                else if (value.Count == 1)
                { blendModeObject = value[0].GetName(); }
                else
                {
                    var blendModeArray = new PdfArray();
                    foreach (BlendModeEnum blendMode in value)
                    { blendModeArray.Add(blendMode.GetName()); }
                    blendModeObject = blendModeArray;
                }
                BaseDataObject[PdfName.BM] = blendModeObject;
            }
        }

        /**
          <summary>Gets/Sets the nonstroking alpha constant, specifying the constant shape or constant
          opacity value to be used for nonstroking operations in the transparent imaging model
          [PDF:1.7:7.2.6].</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public float? FillAlpha
        {
            get => BaseDataObject.GetNFloat(PdfName.ca);
            set => BaseDataObject.SetFloat(PdfName.ca, value);
        }

        /**
          <summary>Gets/Sets the stroking alpha constant, specifying the constant shape or constant
          opacity value to be used for stroking operations in the transparent imaging model
          [PDF:1.7:7.2.6].</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public float? StrokeAlpha
        {
            get => BaseDataObject.GetNFloat(PdfName.CA);
            set => BaseDataObject.SetFloat(PdfName.CA, value);
        }

        [PDF(VersionEnum.PDF13)]
        public Font Font
        {
            get
            {
                var fontObject = (PdfArray)BaseDataObject[PdfName.Font];
                return Font.Wrap(fontObject?[0]);
            }
            set
            {
                var fontObject = (PdfArray)BaseDataObject[PdfName.Font];
                if (fontObject == null)
                { fontObject = new PdfArray(2) { PdfObjectWrapper.GetBaseObject(value), PdfInteger.Default }; }
                else
                { fontObject[0] = PdfObjectWrapper.GetBaseObject(value); }
                BaseDataObject[PdfName.Font] = fontObject;
            }
        }

        [PDF(VersionEnum.PDF13)]
        public float? FontSize
        {
            get
            {
                var fontObject = (PdfArray)BaseDataObject[PdfName.Font];
                return fontObject?.GetFloat(1);
            }
            set
            {
                var fontObject = (PdfArray)BaseDataObject[PdfName.Font];
                if (fontObject == null)
                { fontObject = new PdfArray(2) { null, PdfReal.Get(value) }; }
                else
                { fontObject.SetFloat(1, value); }
                BaseDataObject[PdfName.Font] = fontObject;
            }
        }

        [PDF(VersionEnum.PDF13)]
        public LineCapEnum? LineCap
        {
            get => (LineCapEnum?)BaseDataObject.GetNInt(PdfName.LC);
            set => BaseDataObject.SetInt(PdfName.LC, value.HasValue ? (int)value.Value : null);
        }

        [PDF(VersionEnum.PDF13)]
        public LineDash LineDash
        {
            get
            {
                var lineDashObject = (PdfArray)BaseDataObject[PdfName.D];
                return lineDashObject != null ? LineDash.Get((PdfArray)lineDashObject[0], (IPdfNumber)lineDashObject[1]) : null;
            }
            set
            {
                var lineDashObject = new PdfArray();
                {
                    var dashArrayObject = new PdfArray();
                    foreach (double dashArrayItem in value.DashArray)
                    { dashArrayObject.Add(PdfReal.Get(dashArrayItem)); }
                    lineDashObject.Add(dashArrayObject);
                    lineDashObject.Add(PdfReal.Get(value.DashPhase));
                }
                BaseDataObject[PdfName.D] = lineDashObject;
            }
        }

        [PDF(VersionEnum.PDF13)]
        public LineJoinEnum? LineJoin
        {
            get => (LineJoinEnum?)BaseDataObject.GetNInt(PdfName.LJ);
            set => BaseDataObject.SetInt(PdfName.LJ, value.HasValue ? (int)value.Value : null);
        }

        [PDF(VersionEnum.PDF13)]
        public float? LineWidth
        {
            get => BaseDataObject.GetNFloat(PdfName.LW);
            set => BaseDataObject.SetFloat(PdfName.LW, value);
        }

        [PDF(VersionEnum.PDF13)]
        public float? MiterLimit
        {
            get => BaseDataObject.GetNFloat(PdfName.ML);
            set => BaseDataObject.SetFloat(PdfName.ML, value);
        }

        public Function BG
        {
            get => Function.Wrap(BaseDataObject[PdfName.BG]);
            set => BaseDataObject[PdfName.BG] = value.BaseObject;
        }

        public Function BG2
        {
            get => BaseDataObject[PdfName.BG2] is PdfName ? null : Function.Wrap(BaseDataObject[PdfName.BG2]);
            set => BaseDataObject[PdfName.BG2] = value.BaseObject;
        }
    }
}