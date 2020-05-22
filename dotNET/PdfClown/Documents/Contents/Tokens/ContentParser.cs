/*
  Copyright 2011-2015 Stefano Chizzolini. http://www.pdfclown.org

  Contributors:
    * Stefano Chizzolini (original code developer, http://www.stefanochizzolini.it)
    * Stephen Cleary (bug reporter [FIX:51], https://sourceforge.net/u/stephencleary/)

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

using bytes = PdfClown.Bytes;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Objects;
using PdfClown.Tokens;
using PdfClown.Util.Parsers;

using System;
using System.Collections.Generic;
using sysIO = System.IO;

namespace PdfClown.Documents.Contents.Tokens
{
    /**
      <summary>Content stream parser [PDF:1.6:3.7.1].</summary>
    */
    public sealed class ContentParser : BaseParser
    {
        #region dynamic
        #region constructors
        internal ContentParser(bytes::IInputStream stream) : base(stream)
        { }

        public ContentParser(byte[] data) : base(data)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Parses the next content object [PDF:1.6:4.1].</summary>
        */
        public ContentObject ParseContentObject()
        {
            Operation operation = ParseOperation();
            if (operation is PaintXObject) // External object.
                return new XObject((PaintXObject)operation);
            else if (operation is PaintShading) // Shading.
                return new Shading((PaintShading)operation);
            else if (operation is BeginSubpath
              || operation is DrawRectangle) // Path.
                return ParsePath(operation);
            else if (operation is BeginText) // Text.
                return new Text(ParseContentObjects());
            else if (operation is SaveGraphicsState) // Local graphics state.
                return new LocalGraphicsState(ParseContentObjects());
            else if (operation is BeginMarkedContent) // Marked-content sequence.
                return new MarkedContent((BeginMarkedContent)operation, ParseContentObjects());
            else if (operation is BeginInlineImage) // Inline image.
                return ParseInlineImage();
            else // Single operation.
                return operation;
        }

        /**
          <summary>Parses the next content objects.</summary>
        */
        public IList<ContentObject> ParseContentObjects()
        {
            List<ContentObject> contentObjects = new List<ContentObject>();
            while (MoveNext())
            {
                ContentObject contentObject = ParseContentObject();
                // Multiple-operation graphics object end?
                if (contentObject is EndText // Text.
                  || contentObject is RestoreGraphicsState // Local graphics state.
                  || contentObject is EndMarkedContent // End marked-content sequence.
                  || contentObject is EndInlineImage) // Inline image.
                    return contentObjects;

                contentObjects.Add(contentObject);
            }
            return contentObjects;
        }

        /**
          <summary>Parses the next operation.</summary>
        */
        public Operation ParseOperation()
        {
            string @operator = null;
            List<PdfDirectObject> operands = new List<PdfDirectObject>();
            // Parsing the operation parts...
            do
            {
                switch (TokenType)
                {
                    case TokenTypeEnum.Keyword:
                        @operator = (string)Token;
                        break;
                    default:
                        operands.Add((PdfDirectObject)ParsePdfObject());
                        break;
                }
            } while (@operator == null && MoveNext());
            return Operation.Get(@operator, operands);
        }

        public override PdfDataObject ParsePdfObject()
        {
            switch (TokenType)
            {
                case TokenTypeEnum.Literal:
                    if (Token is string)
                        return new PdfByteString(Encoding.Pdf.Encode((string)Token));
                    break;
                case TokenTypeEnum.Hex:
                    return new PdfByteString((string)Token);
            }
            return base.ParsePdfObject();
        }
        #endregion

        #region private
        private InlineImage ParseInlineImage()
        {
            /*
              NOTE: Inline images use a peculiar syntax that's an exception to the usual rule
              that the data in a content stream is interpreted according to the standard PDF syntax
              for objects.
            */
            InlineImageHeader header;
            {
                List<PdfDirectObject> operands = new List<PdfDirectObject>();
                // Parsing the image entries...
                while (MoveNext() && TokenType != TokenTypeEnum.Keyword) // Not keyword (i.e. end at image data beginning (ID operator)).
                { operands.Add((PdfDirectObject)ParsePdfObject()); }
                header = new InlineImageHeader(operands);
            }

            InlineImageBody body;
            {
                // [FIX:51,74] Wrong 'EI' token handling on inline image parsing.
                bytes::IInputStream stream = Stream;
                stream.ReadByte(); // Should be the whitespace following the 'ID' token.
                bytes::Buffer data = new bytes::Buffer();
                while (true)
                {
                    int curByte = stream.ReadByte();
                    if (((char)curByte == 'E' && (char)stream.PeekByte() == 'I'))
                    {
                        stream.ReadByte();
                        break;
                    }
                    data.Append((byte)curByte);

                }
                body = new InlineImageBody(data);
            }

            return new InlineImage(header, body);
        }

        private Path ParsePath(Operation beginOperation)
        {
            /*
              NOTE: Paths do not have an explicit end operation, so we must infer it
              looking for the first non-painting operation.
            */
            IList<ContentObject> operations = new List<ContentObject>();
            {
                operations.Add(beginOperation);
                long position = Position;
                bool closeable = false;
                while (MoveNext())
                {
                    Operation operation = ParseOperation();
                    // Multiple-operation graphics object closeable?
                    if (operation is PaintPath) // Painting operation.
                    { closeable = true; }
                    else if (closeable) // Past end (first non-painting operation).
                    {
                        Seek(position); // Rolls back to the last path-related operation.

                        break;
                    }

                    operations.Add(operation);
                    position = Position;
                }
            }
            return new Path(operations);
        }
        #endregion
        #endregion
        #endregion
    }
}