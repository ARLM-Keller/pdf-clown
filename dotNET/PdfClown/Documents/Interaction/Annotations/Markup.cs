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

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Markup annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It represents text-based annotations used primarily to mark up documents.</remarks>
    */
    [PDF(VersionEnum.PDF11)]
    public abstract class Markup : Annotation
    {
        #region types
        /**
          <summary>Annotation relationship [PDF:1.6:8.4.5].</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public enum ReplyTypeEnum
        {
            Thread,
            Group
        }
        #endregion

        #region dynamic
        #region constructors
        protected Markup(Page page, PdfName subtype, SKRect box, string text) : base(page, subtype, box, text)
        { CreationDate = DateTime.Now; }

        protected Markup(PdfDirectObject baseObject) : base(baseObject) { }
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
            get => (string)PdfSimpleObject<object>.GetValue(BaseDataObject[PdfName.T]);
            set
            {
                BaseDataObject[PdfName.T] = PdfTextString.Get(value);
                ModificationDate = DateTime.Now;
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the date and time when the annotation was created.</summary>
        */
        [PDF(VersionEnum.PDF15)]
        public override DateTime? CreationDate
        {
            get
            {
                PdfDirectObject creationDateObject = BaseDataObject[PdfName.CreationDate];
                return creationDateObject is PdfDate ? (DateTime?)((PdfDate)creationDateObject).Value : null;
            }
            set
            {
                BaseDataObject[PdfName.CreationDate] = PdfDate.Get(value);
                OnPropertyChanged();
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
                BaseDataObject[PdfName.IRT] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
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
                value.Markup = this;
                BaseDataObject[PdfName.Popup] = value.BaseObject;
                OnPropertyChanged();
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
                BaseDataObject[PdfName.RT] = value.GetCode();
                OnPropertyChanged();
            }
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
                BaseDataObject[PdfName.IT] = value;
                OnPropertyChanged();
            }
        }


        #endregion
        #endregion
        #endregion
    }

    internal static class ReplyTypeEnumExtension
    {
        private static readonly BiDictionary<Markup.ReplyTypeEnum, PdfName> codes;

        static ReplyTypeEnumExtension()
        {
            codes = new BiDictionary<Markup.ReplyTypeEnum, PdfName>
            {
                [Markup.ReplyTypeEnum.Thread] = PdfName.R,
                [Markup.ReplyTypeEnum.Group] = PdfName.Group
            };
        }

        public static Markup.ReplyTypeEnum? Get(PdfName name)
        {
            if (name == null)
                return Markup.ReplyTypeEnum.Thread;

            return codes.GetKey(name);
        }

        public static PdfName GetCode(this Markup.ReplyTypeEnum replyType)
        { return codes[replyType]; }
    }
}