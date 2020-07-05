/*
  Copyright 2013-2015 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Contents;
using PdfClown.Objects;
using PdfClown.Util;

using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Util.Math.Geom;
using PdfClown.Documents.Contents.ColorSpaces;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Markup annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It represents text-based annotations used primarily to mark up documents.</remarks>
    */
    [PDF(VersionEnum.PDF11)]
    public abstract class Markup : Annotation
    {
        #region dynamic
        #region constructors
        protected Markup(Page page, PdfName subtype, SKRect box, string text)
            : base(page, subtype, box, text)
        {
            CreationDate = DateTime.Now;
        }

        protected Markup(PdfDirectObject baseObject)
            : base(baseObject)
        { }
        #endregion

        #region interface
        #region public


        /**
          <summary>Gets/Sets the annotation editor. It is displayed as a text label in the title bar of
          the annotation's pop-up window when open and active. By convention, it identifies the user who
          added the annotation.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public override string Author
        {
            get => BaseDataObject.GetText(PdfName.T);
            set
            {
                var oldValue = Author;
                BaseDataObject.SetText(PdfName.T, value);
                ModificationDate = DateTime.Now;
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the date and time when the annotation was created.</summary>
        */
        [PDF(VersionEnum.PDF15)]
        public override DateTime? CreationDate
        {
            get => BaseDataObject.GetDate(PdfName.CreationDate);
            set
            {
                var oldValue = CreationDate;
                BaseDataObject[PdfName.CreationDate] = PdfDate.Get(value);
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the annotation that this one is in reply to. Both annotations must be on the
          same page of the document.</summary>
          <remarks>The relationship between the two annotations is specified by the
          <see cref="ReplyType"/> property.</remarks>
        */
        [PDF(VersionEnum.PDF15)]
        public virtual Annotation InReplyTo
        {
            get => Annotation.Wrap(BaseDataObject[PdfName.IRT]);
            set
            {
                var oldValue = InReplyTo;
                BaseDataObject[PdfName.IRT] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the pop-up annotation associated with this one.</summary>
          <exception cref="InvalidOperationException">If pop-up annotations can't be associated with
          this markup.</exception>
        */
        [PDF(VersionEnum.PDF13)]
        public virtual Popup Popup
        {
            get => (Popup)Annotation.Wrap(BaseDataObject[PdfName.Popup]);
            set
            {
                var oldValue = Popup;
                if (value != null)
                {
                    value.Parent = this;
                }
                BaseDataObject[PdfName.Popup] = value.BaseObject;
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the relationship between this annotation and one specified by
          <see cref="InReplyTo"/> property.</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public virtual ReplyTypeEnum ReplyType
        {
            get => ReplyTypeEnumExtension.Get((PdfName)BaseDataObject[PdfName.RT]).Value;
            set
            {
                var oldValue = ReplyType;
                BaseDataObject[PdfName.RT] = value.GetCode();
                OnPropertyChanged(oldValue, value);
            }
        }

        [PDF(VersionEnum.PDF15)]
        public string RichContents
        {
            get => BaseDataObject.GetText(PdfName.RC);
            set => BaseDataObject.SetText(PdfName.RC, value);
        }

        [PDF(VersionEnum.PDF16)]
        public MarkupIntent? Intent
        {
            get => MarkupIntentExtension.Get(TypeBase);
            set => TypeBase = MarkupIntentExtension.GetCode(value.Value);
        }

        public string DefaultStyle
        {
            get => BaseDataObject.GetText(PdfName.DS);
            set => BaseDataObject.SetText(PdfName.DS, value);
        }

        /**
          <summary>Gets/Sets the color with which to fill the interior of following markups: Line Ending, Circle, Square.</summary>
        */
        public DeviceColor InteriorColor
        {
            get => DeviceColor.Get((PdfArray)BaseDataObject[PdfName.IC]);
            set => BaseDataObject[PdfName.IC] = (PdfArray)value?.BaseDataObject;
        }

        public SkiaSharp.SKColor? InteriorSKColor
        {
            get => InteriorColor == null ? (SKColor?)null : DeviceColorSpace.CalcSKColor(InteriorColor, Alpha);
            set
            {
                var oldValue = InteriorSKColor;
                InteriorColor = DeviceRGBColor.Get(value);
                OnPropertyChanged(oldValue, value);
            }
        }

        /**
          <summary>Gets/Sets the border effect.</summary>
        */
        [PDF(VersionEnum.PDF15)]
        public BorderEffect BorderEffect
        {
            get => Wrap<BorderEffect>(BaseDataObject.Get<PdfDictionary>(PdfName.BE));
            set
            {
                var oldValue = BorderEffect;
                BaseDataObject[PdfName.BE] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged(oldValue, value);
            }
        }

        protected virtual void RefreshAppearance()
        {

        }

        protected FormXObject ResetAppearance()
        {
            return ResetAppearance(Rect.ToRect());
        }

        protected FormXObject ResetAppearance(SKRect box)
        {
            FormXObject normalAppearance;
            AppearanceStates normalAppearances = Appearance.Normal;
            normalAppearance = normalAppearances[null];
            if (normalAppearance != null)
            {
                normalAppearance.Box = box;
                normalAppearance.BaseDataObject.Body.Clear();
                normalAppearance.ClearContents();
            }
            else
            {
                normalAppearances[null] =
                      normalAppearance = new FormXObject(Document, box);
            }

            return normalAppearance;
        }

        public override SKRect GetBounds(SKMatrix pageMatrix)
        {
            return base.GetBounds(pageMatrix);
        }
        #endregion

        #region protected
        protected PdfName TypeBase
        {
            get => (PdfName)BaseDataObject[PdfName.IT];
            set
            {
                var oldvalue = TypeBase;
                BaseDataObject[PdfName.IT] = value;
                OnPropertyChanged(oldvalue, value);
            }
        }


        #endregion
        #endregion
        #endregion
    }

    /**
      <summary>Annotation relationship [PDF:1.6:8.4.5].</summary>
    */
    [PDF(VersionEnum.PDF16)]
    public enum ReplyTypeEnum
    {
        Thread,
        Group
    }
    public enum MarkupIntent
    {
        FreeText,
        FreeTextCallout,
        FreeTextTypeWriter,

        LineArrow,
        LineDimension,

        PolygonCloud,
        PolygonDimension,
        PolyLineDimension
    }
    internal static class MarkupIntentExtension
    {
        private static readonly BiDictionary<MarkupIntent, PdfName> codes;

        static MarkupIntentExtension()
        {
            codes = new BiDictionary<MarkupIntent, PdfName>
            {
                [MarkupIntent.FreeText] = PdfName.FreeText,
                [MarkupIntent.FreeTextCallout] = PdfName.FreeTextCallout,
                [MarkupIntent.FreeTextTypeWriter] = PdfName.FreeTextTypeWriter,

                [MarkupIntent.LineArrow] = PdfName.LineArrow,
                [MarkupIntent.LineDimension] = PdfName.LineDimension,

                [MarkupIntent.PolygonCloud] = PdfName.PolygonCloud,
                [MarkupIntent.PolygonDimension] = PdfName.PolygonDimension,
                [MarkupIntent.PolyLineDimension] = PdfName.PolyLineDimension
            };
        }

        public static MarkupIntent? Get(PdfName name)
        {
            if (name == null)
                return null;

            return codes.GetKey(name);
        }

        public static PdfName GetCode(this MarkupIntent? intent)
        {
            return intent == null ? null : codes[intent.Value];
        }
    }

    internal static class ReplyTypeEnumExtension
    {
        private static readonly BiDictionary<ReplyTypeEnum, PdfName> codes;

        static ReplyTypeEnumExtension()
        {
            codes = new BiDictionary<ReplyTypeEnum, PdfName>
            {
                [ReplyTypeEnum.Thread] = PdfName.R,
                [ReplyTypeEnum.Group] = PdfName.Group
            };
        }

        public static ReplyTypeEnum? Get(PdfName name)
        {
            if (name == null)
                return ReplyTypeEnum.Thread;

            return codes.GetKey(name);
        }

        public static PdfName GetCode(this ReplyTypeEnum replyType)
        {
            return codes[replyType];
        }
    }
}