/*
  Copyright 2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Documents.Files;
using actions = PdfClown.Documents.Interaction.Actions;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util;

using System;

namespace PdfClown.Documents.Multimedia
{
    /**
      <summary>Media clip data [PDF:1.7:9.1.3].</summary>
    */
    [PDF(VersionEnum.PDF15)]
    public sealed class MediaClipData
      : MediaClip
    {
        #region types
        /**
          <summary>Circumstance under which it is acceptable to write a temporary file in order to play
          a media clip.</summary>
        */
        public enum TempFilePermissionEnum
        {
            /**
              <summary>Never allowed.</summary>
            */
            Never,
            /**
              <summary>Allowed only if the document permissions allow content extraction.</summary>
            */
            ContentExtraction,
            /**
              <summary>Allowed only if the document permissions allow content extraction, including for
              accessibility purposes.</summary>
            */
            Accessibility,
            /**
              <summary>Always allowed.</summary>
            */
            Always
        }

        /**
          <summary>Media clip data viability.</summary>
        */
        public class Viability : PdfObjectWrapper<PdfDictionary>
        {
            public Viability(PdfDirectObject baseObject) : base(baseObject)
            { }

            /**
              <summary>Gets the absolute URL to be used as the base URL in resolving any relative URLs
              found within the media data.</summary>
            */
            public Uri BaseURL
            {
                get
                {
                    PdfString baseURLObject = (PdfString)BaseDataObject[PdfName.BU];
                    return baseURLObject != null ? new Uri(baseURLObject.StringValue) : null;
                }
                set => BaseDataObject[PdfName.BU] = (value != null ? new PdfString(value.ToString()) : null);
            }
        }
        #endregion

        #region dynamic
        #region constructors
        public MediaClipData(PdfObjectWrapper data, string mimeType) : base(data.Document, PdfName.MCD)
        {
            Data = data;
            MimeType = mimeType;
            TempFilePermission = TempFilePermissionEnum.Always;
        }

        internal MediaClipData(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        public override PdfObjectWrapper Data
        {
            get
            {
                PdfDirectObject dataObject = BaseDataObject[PdfName.D];
                if (dataObject == null)
                    return null;

                if (dataObject.Resolve() is PdfStream)
                    return FormXObject.Wrap(dataObject);
                else
                    return FileSpecification.Wrap(dataObject);
            }
            set => BaseDataObject[PdfName.D] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the MIME type of data [RFC 2045].</summary>
        */
        public string MimeType
        {
            get => BaseDataObject.GetString(PdfName.CT);
            set => BaseDataObject.SetString(PdfName.CT, value);
        }

        /**
          <summary>Gets/Sets the player rules for playing this media.</summary>
        */
        public MediaPlayers Players
        {
            get => Wrap<MediaPlayers>(BaseDataObject.Get<PdfDictionary>(PdfName.PL));
            set => BaseDataObject[PdfName.PL] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the preferred options the renderer should attempt to honor without affecting its
          viability.</summary>
        */
        public Viability Preferences
        {
            get => Wrap<Viability>(BaseDataObject.Get<PdfDictionary>(PdfName.BE));
            set => BaseDataObject[PdfName.BE] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the minimum requirements the renderer must honor in order to be considered viable.
          </summary>
        */
        public Viability Requirements
        {
            get => Wrap<Viability>(BaseDataObject.Get<PdfDictionary>(PdfName.MH));
            set => BaseDataObject[PdfName.MH] = PdfObjectWrapper.GetBaseObject(value);
        }

        /**
          <summary>Gets/Sets the circumstance under which it is acceptable to write a temporary file in order
          to play this media clip.</summary>
        */
        public TempFilePermissionEnum? TempFilePermission
        {
            get => TempFilePermissionEnumExtension.Get((PdfString)BaseDataObject.Resolve<PdfDictionary>(PdfName.P)[PdfName.TF]);
            set => BaseDataObject.Resolve<PdfDictionary>(PdfName.P)[PdfName.TF] = (value.HasValue ? value.Value.GetCode() : null);
        }
        #endregion
        #endregion
        #endregion
    }

    internal static class TempFilePermissionEnumExtension
    {
        private static readonly BiDictionary<MediaClipData.TempFilePermissionEnum, PdfString> codes;

        static TempFilePermissionEnumExtension()
        {
            codes = new BiDictionary<MediaClipData.TempFilePermissionEnum, PdfString>
            {
                [MediaClipData.TempFilePermissionEnum.Never] = new PdfString("TEMPNEVER"),
                [MediaClipData.TempFilePermissionEnum.ContentExtraction] = new PdfString("TEMPEXTRACT"),
                [MediaClipData.TempFilePermissionEnum.Accessibility] = new PdfString("TEMPACCESS"),
                [MediaClipData.TempFilePermissionEnum.Always] = new PdfString("TEMPALWAYS")
            };
        }

        public static MediaClipData.TempFilePermissionEnum? Get(PdfString code)
        {
            if (code == null)
                return null;

            MediaClipData.TempFilePermissionEnum? tempFilePermission = codes.GetKey(code);
            if (!tempFilePermission.HasValue)
                throw new NotSupportedException("Operation unknown: " + code);

            return tempFilePermission;
        }

        public static PdfString GetCode(this MediaClipData.TempFilePermissionEnum tempFilePermission)
        { return codes[tempFilePermission]; }
    }
}