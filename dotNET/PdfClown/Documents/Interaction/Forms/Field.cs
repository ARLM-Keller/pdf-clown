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
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Contents.Tokens;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace PdfClown.Documents.Interaction.Forms
{
    /**
      <summary>Interactive form field [PDF:1.6:8.6.2].</summary>
    */
    [PDF(VersionEnum.PDF12)]
    public abstract class Field : PdfObjectWrapper<PdfDictionary>
    {
        private SetFont setFontOperation;

        /*
NOTE: Inheritable attributes are NOT early-collected, as they are NOT part
of the explicit representation of a field -- they are retrieved everytime clients call.
*/
        /**
          <summary>Field flags [PDF:1.6:8.6.2].</summary>
        */
        [Flags]
        public enum FlagsEnum
        {
            /**
              <summary>The user may not change the value of the field.</summary>
            */
            ReadOnly = 0x1,
            /**
              <summary>The field must have a value at the time it is exported by a submit-form action.</summary>
            */
            Required = 0x2,
            /**
              <summary>The field must not be exported by a submit-form action.</summary>
            */
            NoExport = 0x4,
            /**
              <summary>(Text fields only) The field can contain multiple lines of text.</summary>
            */
            Multiline = 0x1000,
            /**
              <summary>(Text fields only) The field is intended for entering a secure password
              that should not be echoed visibly to the screen.</summary>
            */
            Password = 0x2000,
            /**
              <summary>(Radio buttons only) Exactly one radio button must be selected at all times.</summary>
            */
            NoToggleToOff = 0x4000,
            /**
              <summary>(Button fields only) The field is a set of radio buttons (otherwise, a check box).</summary>
              <remarks>This flag is meaningful only if the Pushbutton flag isn't selected.</remarks>
            */
            Radio = 0x8000,
            /**
              <summary>(Button fields only) The field is a pushbutton that does not retain a permanent value.</summary>
            */
            Pushbutton = 0x10000,
            /**
              <summary>(Choice fields only) The field is a combo box (otherwise, a list box).</summary>
            */
            Combo = 0x20000,
            /**
              <summary>(Choice fields only) The combo box includes an editable text box as well as a dropdown list
              (otherwise, it includes only a drop-down list).</summary>
            */
            Edit = 0x40000,
            /**
              <summary>(Choice fields only) The field's option items should be sorted alphabetically.</summary>
            */
            Sort = 0x80000,
            /**
              <summary>(Text fields only) Text entered in the field represents the pathname of a file
              whose contents are to be submitted as the value of the field.</summary>
            */
            FileSelect = 0x100000,
            /**
              <summary>(Choice fields only) More than one of the field's option items may be selected simultaneously.</summary>
            */
            MultiSelect = 0x200000,
            /**
              <summary>(Choice and text fields only) Text entered in the field is not spell-checked.</summary>
            */
            DoNotSpellCheck = 0x400000,
            /**
              <summary>(Text fields only) The field does not scroll to accommodate more text
              than fits within its annotation rectangle.</summary>
              <remarks>Once the field is full, no further text is accepted.</remarks>
            */
            DoNotScroll = 0x800000,
            /**
              <summary>(Text fields only) The field is automatically divided into as many equally spaced positions,
              or combs, as the value of MaxLen, and the text is laid out into those combs.</summary>
            */
            Comb = 0x1000000,
            /**
              <summary>(Text fields only) The value of the field should be represented as a rich text string.</summary>
            */
            RichText = 0x2000000,
            /**
              <summary>(Button fields only) A group of radio buttons within a radio button field that use
              the same value for the on state will turn on and off in unison
              (otherwise, the buttons are mutually exclusive).</summary>
            */
            RadiosInUnison = 0x2000000,
            /**
              <summary>(Choice fields only) The new value is committed as soon as a selection is made with the pointing device.</summary>
            */
            CommitOnSelChange = 0x4000000
        };

        /**
          <summary>Wraps a field reference into a field object.</summary>
          <param name="reference">Reference to a field object.</param>
          <returns>Field object associated to the reference.</returns>
        */
        public static Field Wrap(PdfReference reference)
        {
            if (reference == null)
                return null;
            if (reference.Wrapper is Field field)
                return field;
            if (reference.DataObject?.Wrapper is Field referenceField)
            {
                reference.Wrapper = referenceField;
                return referenceField;
            }
            PdfDictionary dataObject = (PdfDictionary)reference.DataObject;
            PdfName fieldType = (PdfName)GetInheritableAttribute(dataObject, PdfName.FT);
            PdfInteger fieldFlags = (PdfInteger)GetInheritableAttribute(dataObject, PdfName.Ff);
            FlagsEnum fieldFlagsValue = (FlagsEnum)(fieldFlags?.IntValue ?? 0);
            if (fieldType.Equals(PdfName.Btn)) // Button.
            {
                if ((fieldFlagsValue & FlagsEnum.Pushbutton) == FlagsEnum.Pushbutton) // Pushbutton.
                    return new PushButton(reference);
                else if ((fieldFlagsValue & FlagsEnum.Radio) == FlagsEnum.Radio) // Radio.
                    return new RadioButton(reference);
                else // Check box.
                    return new CheckBox(reference);
            }
            else if (fieldType.Equals(PdfName.Tx)) // Text.
                return new TextField(reference);
            else if (fieldType.Equals(PdfName.Ch)) // Choice.
            {
                if ((fieldFlagsValue & FlagsEnum.Combo) > 0) // Combo box.
                    return new ComboBox(reference);
                else // List box.
                    return new ListBox(reference);
            }
            else if (fieldType.Equals(PdfName.Sig)) // Signature.
                return new SignatureField(reference);
            else // Unknown.
                throw new NotSupportedException("Unknown field type: " + fieldType);
        }

        private static PdfDirectObject GetInheritableAttribute(PdfDictionary dictionary, PdfName key)
        {
            /*
              NOTE: It moves upwards until it finds the inherited attribute.
            */
            do
            {
                PdfDirectObject entry = dictionary[key];
                if (entry != null)
                    return entry;

                dictionary = (PdfDictionary)dictionary.Resolve(PdfName.Parent);
            } while (dictionary != null);
            // Default.
            if (key.Equals(PdfName.Ff))
                return PdfInteger.Default;
            else
                return null;
        }

        /**
          <summary>Creates a new field within the given document context.</summary>
        */
        protected Field(PdfName fieldType, string name, Widget widget) : this(widget.BaseObject)
        {
            widget.Field = this;
            PdfDictionary baseDataObject = BaseDataObject;
            baseDataObject[PdfName.FT] = fieldType;
            baseDataObject[PdfName.T] = new PdfTextString(name);
        }

        public Field(PdfDirectObject baseObject) : base(baseObject)
        { }

        /**
          <summary>Gets/Sets the field's behavior in response to trigger events.</summary>
        */
        public FieldActions Actions
        {
            get
            {
                PdfDirectObject actionsObject = BaseDataObject[PdfName.AA];
                return actionsObject != null ? new FieldActions(actionsObject) : null;
            }
            set => BaseDataObject[PdfName.AA] = value.BaseObject;
        }

        /**
          <summary>Gets the default value to which this field reverts when a <see cref="ResetForm">reset
          -form</see> action} is executed.</summary>
        */
        public object DefaultValue
        {
            get
            {
                PdfDataObject defaultValueObject = PdfObject.Resolve(GetInheritableAttribute(PdfName.DV));
                return defaultValueObject != null
                  ? defaultValueObject.GetType().InvokeMember(
                    "Value",
                    BindingFlags.GetProperty,
                    null,
                    defaultValueObject,
                    null)
                  : null;
            }
        }

        /**
          <summary>Gets/Sets whether the field is exported by a submit-form action.</summary>
        */
        public bool Exportable
        {
            get => (Flags & FlagsEnum.NoExport) != FlagsEnum.NoExport;
            set => Flags = EnumUtils.Mask(Flags, FlagsEnum.NoExport, !value);
        }

        /**
          <summary>Gets/Sets the field flags.</summary>
        */
        public FlagsEnum Flags
        {
            get
            {
                PdfInteger flagsObject = (PdfInteger)PdfObject.Resolve(GetInheritableAttribute(PdfName.Ff));
                return (FlagsEnum)Enum.ToObject(
                  typeof(FlagsEnum),
                  (flagsObject == null ? 0 : flagsObject.RawValue)
                  );
            }
            set => BaseDataObject[PdfName.Ff] = PdfInteger.Get((int)value);
        }

        //public Field Parent
        //{
        //    get => Wrap((PdfReference)BaseDataObject[PdfName.Parent]);
        //    set => BaseDataObject[PdfName.Parent] = value?.BaseObject;
        //}

        /**
          <summary>Gets the fully-qualified field name.</summary>
        */
        public string FullName
        {
            get
            {
                var buffer = new StringBuilder();
                {
                    var partialNameStack = new Stack<string>();
                    {
                        PdfDictionary parent = BaseDataObject;
                        while (parent != null)
                        {
                            partialNameStack.Push((string)((PdfTextString)parent[PdfName.T]).Value);
                            parent = (PdfDictionary)parent.Resolve(PdfName.Parent);
                        }
                    }
                    while (partialNameStack.Count > 0)
                    {
                        if (buffer.Length > 0)
                        { buffer.Append('.'); }

                        buffer.Append(partialNameStack.Pop());
                    }
                }
                return buffer.ToString();
            }
        }

        /**
          <summary>Gets/Sets the partial field name.</summary>
        */
        public string Name
        {
            // NOTE: Despite the field name is not a canonical 'inheritable' attribute, sometimes it's not expressed at leaf level.
            get => ((IPdfString)GetInheritableAttribute(PdfName.T)).StringValue;
            set => BaseDataObject.SetText(PdfName.T, value);
        }

        /**
          <summary>Gets/Sets whether the user may not change the value of the field.</summary>
        */
        public bool ReadOnly
        {
            get => (Flags & FlagsEnum.ReadOnly) == FlagsEnum.ReadOnly;
            set => Flags = EnumUtils.Mask(Flags, FlagsEnum.ReadOnly, value);
        }

        /**
          <summary>Gets/Sets whether the field must have a value at the time it is exported by a
          submit-form action.</summary>
        */
        public bool Required
        {
            get => (Flags & FlagsEnum.Required) == FlagsEnum.Required;
            set => Flags = EnumUtils.Mask(Flags, FlagsEnum.Required, value);
        }

        /**
          <summary>Gets/Sets the field value.</summary>
        */
        public abstract object Value
        {
            get;
            set;
        }

        /**
          <summary>Gets the widget annotations that are associated with this field.</summary>
        */
        public FieldWidgets Widgets
        {
            get
            {
                /*
                  NOTE: Terminal fields MUST be associated at least to one widget annotation.
                  If there is only one associated widget annotation and its contents
                  have been merged into the field dictionary, 'Kids' entry MUST be omitted.
                */
                PdfDirectObject widgetsObject = BaseDataObject[PdfName.Kids];
                return FieldWidgets.Wrap(
                  widgetsObject != null
                    ? widgetsObject // Annotation array.
                    : BaseObject, // Merged annotation.
                  this);
            }
        }

        protected PdfString DAString
        {
            get => (PdfString)GetInheritableAttribute(PdfName.DA);
            set => BaseDataObject[PdfName.DA] = value;
        }

        protected SetFont DAOperation
        {
            get
            {
                if (setFontOperation != null)
                    return setFontOperation;
                if (DAString == null)
                    return null;
                var parser = new ContentParser(DAString.RawValue);
                foreach (ContentObject content in parser.ParseContentObjects())
                {
                    if (content is SetFont setFont)
                    {
                        return setFontOperation = setFont;
                    }
                }
                return null;
            }
            set
            {
                setFontOperation = value;
                if (setFontOperation != null)
                {
                    var buffer = new ByteStream(64);
                    value.WriteTo(buffer, Document);
                    DAString = new PdfString(buffer.AsMemory());
                }
            }
        }

        protected PdfDirectObject GetInheritableAttribute(PdfName key) => GetInheritableAttribute(BaseDataObject, key);
    }
}