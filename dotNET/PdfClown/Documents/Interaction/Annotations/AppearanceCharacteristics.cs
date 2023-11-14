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
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Objects;

using System;
using System.Collections.Generic;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Appearance characteristics [PDF:1.6:8.4.5].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public sealed class AppearanceCharacteristics : PdfObjectWrapper<PdfDictionary>
    {
        /**
          <summary>Icon fit [PDF:1.6:8.6.6].</summary>
        */
        public class IconFitObject : PdfObjectWrapper<PdfDictionary>
        {
            /**
              <summary>Scaling mode [PDF:1.6:8.6.6].</summary>
            */
            public enum ScaleModeEnum
            {
                /**
                  <summary>Always scale.</summary>
                */
                Always,
                /**
                  <summary>Scale only when the icon is bigger than the annotation box.</summary>
                */
                Bigger,
                /**
                  <summary>Scale only when the icon is smaller than the annotation box.</summary>
                */
                Smaller,
                /**
                  <summary>Never scale.</summary>
                */
                Never
            };

            /**
              <summary>Scaling type [PDF:1.6:8.6.6].</summary>
            */
            public enum ScaleTypeEnum
            {
                /**
                  <summary>Scale the icon to fill the annotation box exactly,
                  without regard to its original aspect ratio.</summary>
                */
                Anamorphic,
                /**
                  <summary>Scale the icon to fit the width or height of the annotation box,
                  while maintaining the icon's original aspect ratio.</summary>
                */
                Proportional
            };

            private static readonly Dictionary<ScaleModeEnum, PdfName> ScaleModeEnumCodes;
            private static readonly Dictionary<ScaleTypeEnum, PdfName> ScaleTypeEnumCodes;

            static IconFitObject()
            {
                ScaleModeEnumCodes = new Dictionary<ScaleModeEnum, PdfName>
                {
                    [ScaleModeEnum.Always] = PdfName.A,
                    [ScaleModeEnum.Bigger] = PdfName.B,
                    [ScaleModeEnum.Smaller] = PdfName.S,
                    [ScaleModeEnum.Never] = PdfName.N
                };

                ScaleTypeEnumCodes = new Dictionary<ScaleTypeEnum, PdfName>
                {
                    [ScaleTypeEnum.Anamorphic] = PdfName.A,
                    [ScaleTypeEnum.Proportional] = PdfName.P
                };
            }

            /**
              <summary>Gets the code corresponding to the given value.</summary>
            */
            private static PdfName ToCode(ScaleModeEnum value) => ScaleModeEnumCodes[value];

            /**
              <summary>Gets the code corresponding to the given value.</summary>
            */
            private static PdfName ToCode(ScaleTypeEnum value) => ScaleTypeEnumCodes[value];

            /**
              <summary>Gets the scaling mode corresponding to the given value.</summary>
            */
            private static ScaleModeEnum ToScaleModeEnum(IPdfString value)
            {
                if (value == null)
                    return ScaleModeEnum.Always;
                foreach (KeyValuePair<ScaleModeEnum, PdfName> scaleMode in ScaleModeEnumCodes)
                {
                    if (string.Equals(scaleMode.Value.StringValue, value.StringValue, StringComparison.Ordinal))
                        return scaleMode.Key;
                }
                return ScaleModeEnum.Always;
            }

            /**
              <summary>Gets the scaling type corresponding to the given value.</summary>
            */
            private static ScaleTypeEnum ToScaleTypeEnum(IPdfString value)
            {
                if (value == null)
                    return ScaleTypeEnum.Proportional;
                foreach (KeyValuePair<ScaleTypeEnum, PdfName> scaleType in ScaleTypeEnumCodes)
                {
                    if (string.Equals(scaleType.Value.StringValue, value.StringValue, StringComparison.Ordinal))
                        return scaleType.Key;
                }
                return ScaleTypeEnum.Proportional;
            }

            public static IconFitObject Wrap(PdfDirectObject baseObjec)
            {
                return (IconFitObject)(baseObjec == null ? null : baseObjec.AlternateWrapper ??= new IconFitObject(baseObjec));
            }

            public IconFitObject(Document context) : base(context, new PdfDictionary())
            { }

            public IconFitObject(PdfDirectObject baseObject) : base(baseObject)
            { }

            /**
              <summary>Gets/Sets whether not to take into consideration the line width of the border.</summary>
            */
            public bool BorderExcluded
            {
                get => BaseDataObject.GetBool(PdfName.FB);
                set => BaseDataObject.SetBool(PdfName.FB, value);
            }

            /**
              <summary>Gets/Sets the circumstances under which the icon should be scaled inside the annotation box.</summary>
            */
            public ScaleModeEnum ScaleMode
            {
                get => ToScaleModeEnum((IPdfString)BaseDataObject[PdfName.SW]);
                set => BaseDataObject[PdfName.SW] = ToCode(value);
            }

            /**
              <summary>Gets/Sets the type of scaling to use.</summary>
            */
            public ScaleTypeEnum ScaleType
            {
                get => ToScaleTypeEnum((IPdfString)BaseDataObject[PdfName.S]);
                set => BaseDataObject[PdfName.S] = ToCode(value);
            }

            public PdfArray Alignment
            {
                get => (PdfArray)BaseDataObject.Resolve(PdfName.A);
                set => BaseDataObject[PdfName.A] = value;
            }
            /**
              <summary>Gets/Sets the horizontal alignment of the icon inside the annotation box.</summary>
            */
            public XAlignmentEnum XAlignment
            {
                get
                {
                    return (int)Math.Round((Alignment?.GetDouble(0, 0.5D) ?? 0.5D) / .5) switch
                    {
                        0 => XAlignmentEnum.Left,
                        2 => XAlignmentEnum.Right,
                        _ => XAlignmentEnum.Center,
                    };
                }
                set
                {
                    PdfArray alignmentObject = Alignment;
                    if (alignmentObject == null)
                    {
                        Alignment = alignmentObject = new PdfArray(2) { PdfReal.Get(0.5), PdfReal.Get(0.5) };
                    }

                    double objectValue;
                    switch (value)
                    {
                        case XAlignmentEnum.Left: objectValue = 0; break;
                        case XAlignmentEnum.Right: objectValue = 1; break;
                        default: objectValue = 0.5; break;
                    }
                    alignmentObject.SetDouble(0, objectValue);
                }
            }

            /**
              <summary>Gets/Sets the vertical alignment of the icon inside the annotation box.</summary>
            */
            public YAlignmentEnum YAlignment
            {
                get
                {
                    return (int)Math.Round((Alignment?.GetDouble(1, 0.5D) ?? 0.5D) / .5) switch
                    {
                        0 => YAlignmentEnum.Bottom,
                        2 => YAlignmentEnum.Top,
                        _ => YAlignmentEnum.Middle,
                    };
                }
                set
                {
                    PdfArray alignmentObject = Alignment;
                    if (alignmentObject == null)
                    {
                        Alignment = alignmentObject = new PdfArray(2) { PdfReal.Get(0.5), PdfReal.Get(0.5) };
                    }

                    double objectValue;
                    switch (value)
                    {
                        case YAlignmentEnum.Bottom: objectValue = 0; break;
                        case YAlignmentEnum.Top: objectValue = 1; break;
                        default: objectValue = 0.5; break;
                    }
                    alignmentObject.SetDouble(1, objectValue);
                }
            }
        }

        /**
          <summary>Annotation orientation [PDF:1.6:8.4.5].</summary>
        */
        public enum OrientationEnum
        {
            /**
              <summary>Upward.</summary>
            */
            Up = 0,
            /**
              <summary>Leftward.</summary>
            */
            Left = 90,
            /**
              <summary>Downward.</summary>
            */
            Down = 180,
            /**
              <summary>Rightward.</summary>
            */
            Right = 270
        };

        public AppearanceCharacteristics(Document context) : base(context, new PdfDictionary())
        { }

        public AppearanceCharacteristics(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets/Sets the widget annotation's alternate (down) caption,
          displayed when the mouse button is pressed within its active area
          (Pushbutton fields only).</summary>
        */
        public string AlternateCaption
        {
            get => BaseDataObject.GetString(PdfName.AC);
            set => BaseDataObject.SetText(PdfName.AC, value);
        }

        /**
          <summary>Gets/Sets the widget annotation's alternate (down) icon definition,
          displayed when the mouse button is pressed within its active area
          (Pushbutton fields only).</summary>
        */
        public FormXObject AlternateIcon
        {
            get => FormXObject.Wrap(BaseDataObject[PdfName.IX]);
            set => BaseDataObject[PdfName.IX] = value.BaseObject;
        }

        /**
          <summary>Gets/Sets the widget annotation's background color.</summary>
        */
        public DeviceColor BackgroundColor
        {
            get => GetColor(PdfName.BG);
            set => SetColor(PdfName.BG, value);
        }

        /**
          <summary>Gets/Sets the widget annotation's border color.</summary>
        */
        public DeviceColor BorderColor
        {
            get => GetColor(PdfName.BC);
            set => SetColor(PdfName.BC, value);
        }

        /**
          <summary>Gets/Sets the position of the caption relative to its icon (Pushbutton fields only).</summary>
        */
        public AppearanceCaptionPosition CaptionPosition
        {
            get => (AppearanceCaptionPosition)BaseDataObject.GetInt(PdfName.TP);
            set => BaseDataObject.SetInt(PdfName.TP, (int)value);
        }

        /**
          <summary>Gets/Sets the icon fit specifying how to display the widget annotation's icon
          within its annotation box (Pushbutton fields only).
          If present, the icon fit applies to all of the annotation's icons
          (normal, rollover, and alternate).</summary>
        */
        public IconFitObject IconFit
        {
            get => IconFitObject.Wrap(BaseDataObject[PdfName.IF]);
            set => BaseDataObject[PdfName.IF] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the widget annotation's normal caption,
          displayed when it is not interacting with the user (Button fields only).</summary>
        */
        public string NormalCaption
        {
            get => BaseDataObject.GetString(PdfName.CA);
            set => BaseDataObject.SetText(PdfName.CA, value);
        }

        /**
          <summary>Gets/Sets the widget annotation's normal icon definition,
          displayed when it is not interacting with the user (Pushbutton fields only).</summary>
        */
        public FormXObject NormalIcon
        {
            get => FormXObject.Wrap(BaseDataObject[PdfName.I]);
            set => BaseDataObject[PdfName.I] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the widget annotation's orientation.</summary>
        */
        public OrientationEnum Orientation
        {
            get => (OrientationEnum)BaseDataObject.GetInt(PdfName.R);
            set => BaseDataObject.SetInt(PdfName.R, (int)value);
        }

        /**
          <summary>Gets/Sets the widget annotation's rollover caption,
          displayed when the user rolls the cursor into its active area
          without pressing the mouse button (Pushbutton fields only).</summary>
        */
        public string RolloverCaption
        {
            get => BaseDataObject.GetString(PdfName.RC);
            set => BaseDataObject.SetText(PdfName.RC, value);
        }

        /**
          <summary>Gets/Sets the widget annotation's rollover icon definition,
          displayed when the user rolls the cursor into its active area
          without pressing the mouse button (Pushbutton fields only).</summary>
        */
        public FormXObject RolloverIcon
        {
            get => FormXObject.Wrap(BaseDataObject[PdfName.RI]);
            set => BaseDataObject[PdfName.RI] = PdfObjectWrapper.GetBaseObject(value);
        }

        private DeviceColor GetColor(PdfName key) => DeviceColor.Get((PdfArray)BaseDataObject.Resolve(key));

        private void SetColor(PdfName key, DeviceColor value) => BaseDataObject[key] = PdfObjectWrapper.GetBaseObject(value);
    }

    /**
      <summary>Caption position relative to its icon [PDF:1.6:8.4.5].</summary>
    */
    public enum AppearanceCaptionPosition
    {
        /**
          <summary>Caption only (no icon).</summary>
        */
        CaptionOnly = 0,
        /**
          <summary>No caption (icon only).</summary>
        */
        NoCaption = 1,
        /**
          <summary>Caption below the icon.</summary>
        */
        Below = 2,
        /**
          <summary>Caption above the icon.</summary>
        */
        Above = 3,
        /**
          <summary>Caption to the right of the icon.</summary>
        */
        Right = 4,
        /**
          <summary>Caption to the left of the icon.</summary>
        */
        Left = 5,
        /**
          <summary>Caption overlaid directly on the icon.</summary>
        */
        Overlaid = 6
    };

}