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
using PdfClown.Objects;
using PdfClown.Util;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Free text annotation [PDF:1.6:8.4.5].</summary>
      <remarks>It displays text directly on the page. Unlike an ordinary text annotation, a free text
      annotation has no open or closed state; instead of being displayed in a pop-up window, the text
      is always visible.</remarks>
    */
    [PDF(VersionEnum.PDF13)]
    public sealed class StaticNote : Markup
    {
        #region types
        /**
          <summary>Callout line [PDF:1.6:8.4.5].</summary>
        */
        public class CalloutLine : PdfObjectWrapper<PdfArray>
        {
            private Page page;

            public CalloutLine(Page page, SKPoint start, SKPoint end)
                : this(page, start, null, end)
            { }

            public CalloutLine(Page page, SKPoint start, SKPoint? knee, SKPoint end)
                : base(new PdfArray())
            {
                this.page = page;
                PdfArray baseDataObject = BaseDataObject;
                {
                    double pageHeight = page.Box.Height;
                    baseDataObject.Add(PdfReal.Get(start.X));
                    baseDataObject.Add(PdfReal.Get(pageHeight - start.Y));
                    if (knee.HasValue)
                    {
                        baseDataObject.Add(PdfReal.Get(knee.Value.X));
                        baseDataObject.Add(PdfReal.Get(pageHeight - knee.Value.Y));
                    }
                    baseDataObject.Add(PdfReal.Get(end.X));
                    baseDataObject.Add(PdfReal.Get(pageHeight - end.Y));
                }
            }

            public CalloutLine(PdfDirectObject baseObject) : base(baseObject)
            { }

            public SKPoint End
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                        return new SKPoint(
                          (float)((IPdfNumber)coordinates[2]).RawValue,
                          (float)(page.Box.Height - ((IPdfNumber)coordinates[3]).RawValue)
                          );
                    else
                        return new SKPoint(
                          (float)((IPdfNumber)coordinates[4]).RawValue,
                          (float)(page.Box.Height - ((IPdfNumber)coordinates[5]).RawValue)
                          );
                }
            }

            public SKPoint? Knee
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;
                    if (coordinates.Count < 6)
                        return null;

                    return new SKPoint(
                      (float)((IPdfNumber)coordinates[2]).RawValue,
                      (float)(page.Box.Height - ((IPdfNumber)coordinates[3]).RawValue)
                      );
                }
            }

            public SKPoint Start
            {
                get
                {
                    PdfArray coordinates = BaseDataObject;

                    return new SKPoint(
                      (float)((IPdfNumber)coordinates[0]).RawValue,
                      (float)(page.Box.Height - ((IPdfNumber)coordinates[1]).RawValue)
                      );
                }
            }
        }

        /**
          <summary>Note type [PDF:1.6:8.4.5].</summary>
        */
        public enum TypeEnum
        {
            /**
              Callout.
            */
            Callout,
            /**
              Typewriter.
            */
            TypeWriter
        }
        #endregion

        #region static
        #region fields
        private static readonly JustificationEnum DefaultJustification = JustificationEnum.Left;
        private static readonly LineEndStyleEnum DefaultLineEndStyle = LineEndStyleEnum.None;
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public StaticNote(Page page, SKRect box, string text)
            : base(page, PdfName.FreeText, box, text)
        { }

        internal StaticNote(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets/Sets the border effect.</summary>
        */
        [PDF(VersionEnum.PDF16)]
        public BorderEffect BorderEffect
        {
            get => BorderEffect.Wrap(BaseDataObject.Get<PdfDictionary>(PdfName.BE));
            set
            {
                BaseDataObject[PdfName.BE] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the justification to be used in displaying the annotation's text.</summary>
        */
        public JustificationEnum Justification
        {
            get => JustificationEnumExtension.Get((PdfInteger)BaseDataObject[PdfName.Q]);
            set
            {
                BaseDataObject[PdfName.Q] = value != DefaultJustification ? value.GetCode() : null;
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the callout line attached to the free text annotation.</summary>
        */
        public CalloutLine Line
        {
            get
            {
                PdfArray calloutCalloutLine = (PdfArray)BaseDataObject[PdfName.CL];
                return calloutCalloutLine != null ? new CalloutLine(calloutCalloutLine) : null;
            }
            set
            {
                BaseDataObject[PdfName.CL] = PdfObjectWrapper.GetBaseObject(value);
                if (value != null)
                {
                    /*
                      NOTE: To ensure the callout would be properly rendered, we have to declare the
                      corresponding intent.
                    */
                    Type = TypeEnum.Callout;
                }
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the style of the ending line ending.</summary>
        */
        public LineEndStyleEnum LineEndStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[1]) : DefaultLineEndStyle;
            }
            set
            {
                EnsureLineEndStylesObject()[1] = value.GetName();
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the style of the starting line ending.</summary>
        */
        public LineEndStyleEnum LineStartStyle
        {
            get
            {
                PdfArray endstylesObject = (PdfArray)BaseDataObject[PdfName.LE];
                return endstylesObject != null ? LineEndStyleEnumExtension.Get((PdfName)endstylesObject[0]) : DefaultLineEndStyle;
            }
            set
            {
                EnsureLineEndStylesObject()[0] = value.GetName();
                OnPropertyChanged();
            }
        }

        /**
          <summary>Popups not supported.</summary>
        */
        public override Popup Popup
        {
            set => throw new NotSupportedException();
        }
        #endregion

        #region private
        private PdfArray EnsureLineEndStylesObject()
        {
            PdfArray endStylesObject = (PdfArray)BaseDataObject[PdfName.LE];
            if (endStylesObject == null)
            {
                BaseDataObject[PdfName.LE] = endStylesObject = new PdfArray(
                  new PdfDirectObject[] { DefaultLineEndStyle.GetName(), DefaultLineEndStyle.GetName() }
                  );
            }
            return endStylesObject;
        }

        private TypeEnum? Type
        {
            get => StaticNoteTypeEnumExtension.Get(TypeBase);
            set => TypeBase = value.HasValue ? value.Value.GetName() : null;
        }
        #endregion
        #endregion
        #endregion
    }

    internal static class StaticNoteTypeEnumExtension
    {
        private static readonly BiDictionary<StaticNote.TypeEnum, PdfName> codes;

        static StaticNoteTypeEnumExtension()
        {
            codes = new BiDictionary<StaticNote.TypeEnum, PdfName>
            {
                [StaticNote.TypeEnum.Callout] = PdfName.FreeTextCallout,
                [StaticNote.TypeEnum.TypeWriter] = PdfName.FreeTextTypeWriter
            };
        }

        public static StaticNote.TypeEnum? Get(PdfName name)
        {
            if (name == null)
                return null;

            StaticNote.TypeEnum? type = codes.GetKey(name);
            if (!type.HasValue)
                throw new NotSupportedException("Type unknown: " + name);

            return type.Value;
        }

        public static PdfName GetName(this StaticNote.TypeEnum type)
        { return codes[type]; }
    }
}