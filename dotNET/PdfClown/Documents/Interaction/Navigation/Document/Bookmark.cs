/*
  Copyright 2006-2012 Stefano Chizzolini. http://www.pdfclown.org

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
using PdfClown.Documents.Interaction;
using actions = PdfClown.Documents.Interaction.Actions;
using PdfClown.Files;
using PdfClown.Objects;

using System;

namespace PdfClown.Documents.Interaction.Navigation
{
    /**
      <summary>Outline item [PDF:1.6:8.2.2].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class Bookmark : PdfObjectWrapper<PdfDictionary>, ILink
    {
        /**
          <summary>Bookmark flags [PDF:1.6:8.2.2].</summary>
        */
        [Flags]
        [PDF(VersionEnum.PDF14)]
        public enum FlagsEnum
        {
            /**
              <summary>Display the item in italic.</summary>
            */
            Italic = 0x1,
            /**
              <summary>Display the item in bold.</summary>
            */
            Bold = 0x2
        }

        public Bookmark(Document context, string title) : base(context, new PdfDictionary())
        { Title = title; }

        public Bookmark(Document context, string title, LocalDestination destination) : this(context, title)
        { Destination = destination; }

        public Bookmark(Document context, string title, Actions.Action action) : this(context, title)
        { Action = action; }

        public Bookmark(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets the child bookmarks.</summary>
        */
        public Bookmarks Bookmarks => Wrap<Bookmarks>(BaseObject);

        /**
          <summary>Gets/Sets the bookmark text color.</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public DeviceRGBColor Color
        {
            get => DeviceRGBColor.Get((PdfArray)BaseDataObject[PdfName.C]);
            set
            {
                if (value == null)
                { BaseDataObject.Remove(PdfName.C); }
                else
                {
                    CheckCompatibility("Color");
                    BaseDataObject[PdfName.C] = value.BaseObject;
                }
            }
        }

        /**
          <summary>Gets/Sets whether this bookmark's children are displayed.</summary>
        */
        public bool Expanded
        {
            get => BaseDataObject.GetInt(PdfName.Count) >= 0;
            set
            {
                if (Expanded == value)
                    return;

                /*
                  NOTE: Positive Count entry means open, negative Count entry means closed [PDF:1.6:8.2.2].
                */
                BaseDataObject[PdfName.Count] = PdfInteger.Get((value ? 1 : -1) * Math.Abs(BaseDataObject.GetInt(PdfName.Count)));
            }
        }

        /**
          <summary>Gets/Sets the bookmark flags.</summary>
        */
        [PDF(VersionEnum.PDF14)]
        public FlagsEnum Flags
        {
            get => (FlagsEnum)BaseDataObject.GetInt(PdfName.F);
            set
            {
                if (value == 0)
                { BaseDataObject.Remove(PdfName.F); }
                else
                {
                    CheckCompatibility(value);
                    BaseDataObject[PdfName.F] = PdfInteger.Get((int)value);
                }
            }
        }

        /**
          <summary>Gets the parent bookmark.</summary>
        */
        public Bookmark Parent
        {
            get
            {
                PdfReference reference = (PdfReference)BaseDataObject[PdfName.Parent];
                // Is its parent a bookmark?
                /*
                  NOTE: the Title entry can be used as a flag to distinguish bookmark
                  (outline item) dictionaries from outline (root) dictionaries.
                */
                if (((PdfDictionary)reference.DataObject).ContainsKey(PdfName.Title)) // Bookmark.
                    return Wrap<Bookmark>(reference);
                else // Outline root.
                    return null; // NO parent bookmark.
            }
        }

        /**
          <summary>Gets/Sets the text to be displayed for this bookmark.</summary>
        */
        public string Title
        {
            get => (string)((PdfTextString)BaseDataObject[PdfName.Title]).Value;
            set => BaseDataObject[PdfName.Title] = new PdfTextString(value);
        }

        public PdfObjectWrapper Target
        {
            get
            {
                if (BaseDataObject.ContainsKey(PdfName.Dest))
                    return Destination;
                else if (BaseDataObject.ContainsKey(PdfName.A))
                    return Action;
                else
                    return null;
            }
            set
            {
                if (value is Destination)
                { Destination = (Destination)value; }
                else if (value is Actions.Action)
                { Action = (actions::Action)value; }
                else
                    throw new ArgumentException("It MUST be either a Destination or an Action.");
            }
        }

        private actions::Action Action
        {
            get => Actions.Action.Wrap(BaseDataObject[PdfName.A]);
            set
            {
                if (value == null)
                { BaseDataObject.Remove(PdfName.A); }
                else
                {
                    /*
                      NOTE: This entry is not permitted in bookmarks if a 'Dest' entry already exists.
                    */
                    if (BaseDataObject.ContainsKey(PdfName.Dest))
                    { BaseDataObject.Remove(PdfName.Dest); }

                    BaseDataObject[PdfName.A] = value.BaseObject;
                }
            }
        }

        private Destination Destination
        {
            get
            {
                PdfDirectObject destinationObject = BaseDataObject[PdfName.Dest];
                return destinationObject != null
                  ? Document.ResolveName<LocalDestination>(destinationObject)
                  : null;
            }
            set
            {
                if (value == null)
                { BaseDataObject.Remove(PdfName.Dest); }
                else
                {
                    /*
                      NOTE: This entry is not permitted in bookmarks if an 'A' entry is present.
                    */
                    if (BaseDataObject.ContainsKey(PdfName.A))
                    { BaseDataObject.Remove(PdfName.A); }

                    BaseDataObject[PdfName.Dest] = value.NamedBaseObject;
                }
            }
        }
    }
}