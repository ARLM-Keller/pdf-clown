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
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.Layers;
using PdfClown.Documents.Interaction.Actions;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util;

using System;
using SkiaSharp;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfClown.Documents.Interaction.Annotations
{
    /**
      <summary>Annotation [PDF:1.6:8.4].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public abstract class Annotation : PdfObjectWrapper<PdfDictionary>, ILayerable, INotifyPropertyChanged
    {
        private Page page;

        public event PropertyChangedEventHandler PropertyChanged;
        #region types
        /**
          <summary>Annotation flags [PDF:1.6:8.4.2].</summary>
        */
        [Flags]
        public enum FlagsEnum
        {
            /**
              <summary>Hide the annotation, both on screen and on print,
              if it does not belong to one of the standard annotation types
              and no annotation handler is available.</summary>
            */
            Invisible = 0x1,
            /**
              <summary>Hide the annotation, both on screen and on print
              (regardless of its annotation type or whether an annotation handler is available).</summary>
            */
            Hidden = 0x2,
            /**
              <summary>Print the annotation when the page is printed.</summary>
            */
            Print = 0x4,
            /**
              <summary>Do not scale the annotation's appearance to match the magnification of the page.</summary>
            */
            NoZoom = 0x8,
            /**
              <summary>Do not rotate the annotation's appearance to match the rotation of the page.</summary>
            */
            NoRotate = 0x10,
            /**
              <summary>Hide the annotation on the screen.</summary>
            */
            NoView = 0x20,
            /**
              <summary>Do not allow the annotation to interact with the user.</summary>
            */
            ReadOnly = 0x40,
            /**
              <summary>Do not allow the annotation to be deleted or its properties to be modified by the user.</summary>
            */
            Locked = 0x80,
            /**
              <summary>Invert the interpretation of the NoView flag.</summary>
            */
            ToggleNoView = 0x100
        }
        #endregion

        #region static
        #region interface
        #region public
        /**
          <summary>Wraps an annotation base object into an annotation object.</summary>
          <param name="baseObject">Annotation base object.</param>
          <returns>Annotation object associated to the base object.</returns>
        */
        public static Annotation Wrap(PdfDirectObject baseObject)
        {
            if (baseObject == null)
                return null;
            if (baseObject.Wrapper is Annotation annotation)
                return annotation;

            PdfName annotationType = (PdfName)((PdfDictionary)baseObject.Resolve())[PdfName.Subtype];
            if (annotationType.Equals(PdfName.Text))
                return new StickyNote(baseObject);
            else if (annotationType.Equals(PdfName.Link))
                return new Link(baseObject);
            else if (annotationType.Equals(PdfName.FreeText))
                return new FreeText(baseObject);
            else if (annotationType.Equals(PdfName.Line))
                return new Line(baseObject);
            else if (annotationType.Equals(PdfName.Square))
                return new Rectangle(baseObject);
            else if (annotationType.Equals(PdfName.Circle))
                return new Ellipse(baseObject);
            else if (annotationType.Equals(PdfName.Polygon))
                return new Polygon(baseObject);
            else if (annotationType.Equals(PdfName.PolyLine))
                return new Polyline(baseObject);
            else if (annotationType.Equals(PdfName.Highlight)
              || annotationType.Equals(PdfName.Underline)
              || annotationType.Equals(PdfName.Squiggly)
              || annotationType.Equals(PdfName.StrikeOut))
                return new TextMarkup(baseObject);
            else if (annotationType.Equals(PdfName.Stamp))
                return new Stamp(baseObject);
            else if (annotationType.Equals(PdfName.Caret))
                return new Caret(baseObject);
            else if (annotationType.Equals(PdfName.Ink))
                return new Scribble(baseObject);
            else if (annotationType.Equals(PdfName.Popup))
                return new Popup(baseObject);
            else if (annotationType.Equals(PdfName.FileAttachment))
                return new FileAttachment(baseObject);
            else if (annotationType.Equals(PdfName.Sound))
                return new Sound(baseObject);
            else if (annotationType.Equals(PdfName.Movie))
                return new Movie(baseObject);
            else if (annotationType.Equals(PdfName.Widget))
                return new Widget(baseObject);
            else if (annotationType.Equals(PdfName.Screen))
                return new Screen(baseObject);
            //TODO
            //     else if(annotationType.Equals(PdfName.PrinterMark)) return new PrinterMark(baseObject);
            //     else if(annotationType.Equals(PdfName.TrapNet)) return new TrapNet(baseObject);
            //     else if(annotationType.Equals(PdfName.Watermark)) return new Watermark(baseObject);
            //     else if(annotationType.Equals(PdfName.3DAnnotation)) return new 3DAnnotation(baseObject);
            else // Other annotation type.
                return new GenericAnnotation(baseObject);
        }

        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        protected Annotation(Page page, PdfName subtype, SKRect box, string text)
            : base(
                  page.Document,
                  new PdfDictionary(
                      new PdfName[] { PdfName.Type, PdfName.Subtype, PdfName.Border },
                      new PdfDirectObject[]
                      {
                          PdfName.Annot,
                          subtype,
                          new PdfArray(new PdfDirectObject[]{PdfInteger.Default, PdfInteger.Default, PdfInteger.Default}) // NOTE: Hide border by default.
                      }
                      )
                  )
        {
            this.page = page;
            page.Annotations.Add(this);
            Box = box;
            Text = text;
            Printable = true;
        }

        public Annotation(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public virtual string Author
        {
            get => string.Empty;
            set { }
        }

        public virtual DateTime? CreationDate
        {
            get => null;
            set { }
        }

        /**
          <summary>Gets/Sets action to be performed when the annotation is activated.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual Actions.Action Action
        {
            get => Interaction.Actions.Action.Wrap(BaseDataObject[PdfName.A]);
            set
            {
                BaseDataObject[PdfName.A] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the annotation's behavior in response to various trigger events.</summary>
        */
        [PDF(VersionEnum.PDF12)]
        public virtual AnnotationActions Actions
        {
            get => CommonAnnotationActions.Wrap(this, BaseDataObject.Get<PdfDictionary>(PdfName.AA));
            set
            {
                BaseDataObject[PdfName.AA] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the appearance specifying how the annotation is presented visually on the page.</summary>
        */
        [PDF(VersionEnum.PDF12)]
        public virtual Appearance Appearance
        {
            get => Wrap<Appearance>(BaseDataObject.Get<PdfDictionary>(PdfName.AP));
            set
            {
                BaseDataObject[PdfName.AP] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the border style.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual Border Border
        {
            get => Border.Wrap(BaseDataObject.Get<PdfDictionary>(PdfName.BS));
            set
            {
                BaseDataObject[PdfName.BS] = PdfObjectWrapper.GetBaseObject(value);
                if (value != null)
                { BaseDataObject.Remove(PdfName.Border); }
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the location of the annotation on the page in default user space units.
          </summary>
        */
        public virtual SKRect Box
        {
            get
            {
                var box = Wrap<Objects.Rectangle>(BaseDataObject[PdfName.Rect]);
                return SKRect.Create(
                  (float)box.Left,
                  (float)(GetPageHeight() - box.Top),
                  (float)box.Width,
                  (float)box.Height
                  );
            }
            set
            {
                BaseDataObject[PdfName.Rect] = new Objects.Rectangle(value.Left, GetPageHeight() - value.Top, value.Width, value.Height)
                    .BaseDataObject;
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the annotation color.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual DeviceColor Color
        {
            get => DeviceColor.Get((PdfArray)BaseDataObject[PdfName.C]);
            set
            {
                BaseDataObject[PdfName.C] = PdfObjectWrapper.GetBaseObject(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Deletes this annotation removing also its reference on the page.</summary>
        */
        public override bool Delete()
        {
            // Shallow removal (references):
            // * reference on page
            Page.Annotations.Remove(this);

            // Deep removal (indirect object).
            return base.Delete();
        }

        /**
          <summary>Gets/Sets the annotation flags.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual FlagsEnum Flags
        {
            get
            {
                PdfInteger flagsObject = (PdfInteger)BaseDataObject[PdfName.F];
                return flagsObject == null
                  ? 0
                  : (FlagsEnum)Enum.ToObject(typeof(FlagsEnum), flagsObject.RawValue);
            }
            set
            {
                BaseDataObject[PdfName.F] = PdfInteger.Get((int)value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the date and time when the annotation was most recently modified.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual DateTime? ModificationDate
        {
            get
            {
                /*
                  NOTE: Despite PDF date being the preferred format, loose formats are tolerated by the spec.
                */
                PdfDirectObject modificationDateObject = BaseDataObject[PdfName.M];
                return (DateTime?)(modificationDateObject is PdfDate ? ((PdfDate)modificationDateObject).Value : null);
            }
            set
            {
                BaseDataObject[PdfName.M] = PdfDate.Get(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the annotation name.</summary>
          <remarks>The annotation name uniquely identifies the annotation among all the annotations on its page.</remarks>
        */
        [PDF(VersionEnum.PDF14)]
        public virtual string Name
        {
            get => (string)PdfSimpleObject<Object>.GetValue(BaseDataObject[PdfName.NM]);
            set
            {
                BaseDataObject[PdfName.NM] = PdfTextString.Get(value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the associated page.</summary>
        */
        [PDF(VersionEnum.PDF13)]
        public virtual Page Page => page ?? (page = Wrap<Page>(BaseDataObject[PdfName.P]));

        /**
          <summary>Gets/Sets whether to print the annotation when the page is printed.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual bool Printable
        {
            get => (Flags & FlagsEnum.Print) == FlagsEnum.Print;
            set
            {
                Flags = EnumUtils.Mask(Flags, FlagsEnum.Print, value);
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets the annotation text.</summary>
          <remarks>Depending on the annotation type, the text may be either directly displayed
          or (in case of non-textual annotations) used as alternate description.</remarks>
        */
        public virtual string Text
        {
            get => (string)PdfSimpleObject<Object>.GetValue(BaseDataObject[PdfName.Contents]);
            set
            {
                BaseDataObject[PdfName.Contents] = PdfTextString.Get(value);
                ModificationDate = DateTime.Now;
                OnPropertyChanged();
            }
        }

        /**
          <summary>Gets/Sets whether the annotation is visible.</summary>
        */
        [PDF(VersionEnum.PDF11)]
        public virtual bool Visible
        {
            get => (Flags & FlagsEnum.Hidden) != FlagsEnum.Hidden;
            set
            {
                Flags = EnumUtils.Mask(Flags, FlagsEnum.Hidden, !value);
                OnPropertyChanged();
            }
        }

        #region ILayerable
        [PDF(VersionEnum.PDF15)]
        public virtual LayerEntity Layer
        {
            get => (LayerEntity)PropertyList.Wrap(BaseDataObject[PdfName.OC]);
            set
            {
                BaseDataObject[PdfName.OC] = value != null ? value.Membership.BaseObject : null;
                OnPropertyChanged();
            }
        }
        #endregion
        #endregion

        #region private
        protected float GetPageHeight()
        {
            Page page = Page;
            return page?.Box.Height ?? Document.GetSize().Height;
        }

        public virtual void Draw(SKCanvas canvas)
        { }

        public virtual SKRect GetBounds(SKMatrix pageMatrix)
        {
            var box = Box;
            return pageMatrix.MapRect(box);
        }

        protected void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
        #endregion
        #endregion
    }
}