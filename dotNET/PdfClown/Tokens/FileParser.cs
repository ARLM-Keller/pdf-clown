/*
  Copyright 2011-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using Org.BouncyCastle.Pkcs;
using PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Documents.Encryption;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Util;
using PdfClown.Util.Parsers;

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfClown.Tokens
{
    /**
      <summary>PDF file parser [PDF:1.7:3.2,3.4].</summary>
    */
    public sealed class FileParser : BaseParser
    {
        public struct Reference
        {
            public readonly int GenerationNumber;
            public readonly int ObjectNumber;

            internal Reference(int objectNumber, int generationNumber)
            {
                this.ObjectNumber = objectNumber;
                this.GenerationNumber = generationNumber;
            }
        }

       // private static readonly int EOFMarkerChunkSize = 1024; // [PDF:1.6:H.3.18].

        private Files.File file;
        private PdfEncryption encryption;
        private Stream keyStoreInputStream;
        private string password;
        private string keyAlias;
        private ISecurityHandler securityHandler;
        private AccessPermission accessPermission;

        public string KeyAlias { get => keyAlias; set => keyAlias = value; }

        internal FileParser(IInputStream stream, Files.File file, string password = null, Stream keyStoreInputStream = null)
            : base(stream)
        {
            this.file = file;
            this.password = password;
            this.keyStoreInputStream = keyStoreInputStream;
        }

        public override bool MoveNext()
        {
            bool moved = base.MoveNext();
            if (moved)
            {
                switch (TokenType)
                {
                    case TokenTypeEnum.Integer:
                        {
                            /*
                              NOTE: We need to verify whether indirect reference pattern is applicable:
                              ref :=  { int int 'R' }
                            */
                            IInputStream stream = Stream;
                            long baseOffset = stream.Position; // Backs up the recovery position.

                            // 1. Object number.
                            int objectNumber = (int)Token;
                            // 2. Generation number.
                            base.MoveNext();
                            if (TokenType == TokenTypeEnum.Integer)
                            {
                                int generationNumber = (int)Token;
                                // 3. Reference keyword.
                                base.MoveNext();
                                if (TokenType == TokenTypeEnum.Keyword)
                                {
                                    var span = CharsToken;
                                    if (span.Equals(Keyword.Reference, StringComparison.Ordinal))
                                    {
                                        TokenType = TokenTypeEnum.Reference;
                                        Token = new Reference(objectNumber, generationNumber);
                                    }
                                    else if (span.Equals(Keyword.BeginIndirectObject, StringComparison.Ordinal))
                                    {
                                        TokenType = TokenTypeEnum.InderectObject;
                                        Token = new Reference(objectNumber, generationNumber);
                                    }
                                }
                            }
                            if (Token is not Reference)
                            {
                                // Rollback!
                                stream.Seek(baseOffset);
                                Token = objectNumber;
                                TokenType = TokenTypeEnum.Integer;
                            }
                        }
                        break;
                }
            }
            return moved;
        }

        public override PdfDataObject ParsePdfObject()
        {
            switch (TokenType)
            {
                case TokenTypeEnum.Reference:
                    if (Token is Reference reference)
                    {
                        return new PdfReference(reference.ObjectNumber, reference.GenerationNumber, file);
                    }
                    break;
            }

            PdfDataObject pdfObject = base.ParsePdfObject();
            if (pdfObject is PdfDictionary streamHeader)
            {
                IInputStream stream = Stream;
                int oldOffset = (int)stream.Position;
                MoveNext();
                // Is this dictionary the header of a stream object [PDF:1.6:3.2.7]?
                if (TokenType == TokenTypeEnum.Keyword
                    && CharsToken.Equals(Keyword.BeginStream, StringComparison.Ordinal))
                {
                    // Keep track of current position!
                    /*
                      NOTE: Indirect reference resolution is an outbound call which affects the stream pointer position,
                      so we need to recover our current position after it returns.
                    */
                    long position = stream.Position;
                    // Get the stream length!
                    int length = streamHeader.GetInt(PdfName.Length, 0);
                    // Move to the stream data beginning!
                    stream.Seek(position);
                    SkipEOL();
                    ValidateLength(streamHeader, stream, ref position, ref length);
                    if (length < 0)
                        length = 0;
                    // Copy the stream data to the instance!
                    byte[] data = new byte[length];
                    stream.Read(data);
                    var bytes = new ByteStream(data);
                    position = stream.Position;
                    MoveNext(); // Postcondition (last token should be 'endstream' keyword).
                    var streamType = streamHeader[PdfName.Type];
                    if (PdfName.ObjStm.Equals(streamType)) // Object stream [PDF:1.6:3.4.6].
                        return new ObjectStream(streamHeader, bytes);
                    else if (PdfName.XRef.Equals(streamType)) // Cross-reference stream [PDF:1.6:3.4.7].
                        return new XRefStream(streamHeader, bytes);
                    else // Generic stream.
                        return new PdfStream(streamHeader, bytes);
                }
                else // Stand-alone dictionary.
                {
                    // Restores postcondition (last token should be the dictionary end).
                    stream.Seek(oldOffset);
                }
            }
            return pdfObject;
        }

        private void ValidateLength(PdfDictionary streamHeader, IInputStream stream, ref long position, ref int length)
        {
            position = stream.Position;
            if (length <= 0)
            {
                length = RepairStreamLength(streamHeader, stream, position);
            }
            else
            {
                stream.Skip(length);
                MoveNext(); // Postcondition (last token should be 'endstream' keyword).
                if (TokenType != TokenTypeEnum.Keyword
                    || !CharsToken.Equals(Keyword.EndStream, StringComparison.Ordinal))
                {
                    stream.Seek(position);

                    length = RepairStreamLength(streamHeader, stream, position);
                }
            }
            stream.Seek(position);

        }

        private int RepairStreamLength(PdfDictionary streamHeader, IInputStream stream, long position)
        {
            int length;
            System.Diagnostics.Debug.Write($"warning: Repair Stream Object missing {PdfName.Length} header parameter");
            if (SkipKey(Keyword.EndStream))
            {
                length = (int)(stream.Position - position);
                streamHeader[PdfName.Length] = PdfInteger.Get(length);
            }
            else
            {
                throw new Exception($"Pdf Stream Object missing {Keyword.EndStream} Keyword");
            }

            return length;
        }

        /**
          <summary>Parses the specified PDF indirect object [PDF:1.6:3.2.9].</summary>
          <param name="xrefEntry">Cross-reference entry of the indirect object to parse.</param>
        */
        public PdfDataObject ParsePdfObject(XRefEntry xrefEntry)
        {
            // Go to the beginning of the indirect object!
            Seek(xrefEntry.Offset);

            // Skip the indirect-object header!
            MoveNext();
            // Empty indirect object?
            if (IsInderectObjectEnd())
                return null;

            MoveNext();
            // Empty indirect object?
            if (IsInderectObjectEnd())
                return null;

            // Get the indirect data object!
            var dataObject = ParsePdfObject();

            if (securityHandler != null)
            {
                securityHandler.Decrypt(dataObject, xrefEntry.Number, xrefEntry.Generation);
            }
            return dataObject;
        }

        private bool IsInderectObjectEnd()
        {
            return TokenType == TokenTypeEnum.Keyword
                            && CharsToken.Equals(Keyword.EndIndirectObject, StringComparison.Ordinal);
        }

        /**
          <summary>Retrieves the PDF version of the file [PDF:1.6:3.4.1].</summary>
        */
        public string RetrieveVersion()
        {
            IInputStream stream = Stream;
            stream.Seek(0);
            string header = stream.ReadString(10);
            if (!header.StartsWith(Keyword.BOF))
                throw new PostScriptParseException("PDF header not found.", this);

            return header.Substring(Keyword.BOF.Length, 3);
        }

        /**
          <summary>Retrieves the starting position of the last xref-table section [PDF:1.6:3.4.4].</summary>
        */
        public long RetrieveXRefOffset()
        {
            // [FIX:69] 'startxref' keyword not found (file was corrupted by alien data in the tail).
            IInputStream stream = Stream;
            var streamLength = stream.Length;

            long position = SeekRevers(streamLength, Keyword.StartXRef);
            if (position < 0)
                throw new PostScriptParseException("'" + Keyword.StartXRef + "' keyword not found.", this);

            // Go past the 'startxref' keyword!
            stream.Seek(position); MoveNext();

            // Get the xref offset!
            MoveNext();
            if (TokenType != TokenTypeEnum.Integer)
                throw new PostScriptParseException("'" + Keyword.StartXRef + "' value invalid.", this);
            long xrefPosition = (int)Token;

            stream.Seek(xrefPosition);
            MoveNext();
            //Repair 
            if (xrefPosition > streamLength
                || (TokenType == TokenTypeEnum.Keyword && !CharsToken.Equals(Keyword.XRef, StringComparison.Ordinal))
                || (TokenType != TokenTypeEnum.InderectObject && TokenType != TokenTypeEnum.Keyword))
            {
                xrefPosition = SeekRevers(streamLength, "\n" + Keyword.XRef);
                if (xrefPosition >= 0)
                    xrefPosition++;

            }
            return xrefPosition;
        }

        private long SeekRevers(long startPosition, string keyWord)
        {
            Seek(startPosition);
            return SkipKeyRevers(keyWord) ? Position : -1;
            //string text = null;
            //long streamLength = stream.Length;
            //long position = startPosition;
            //int chunkSize = (int)Math.Min(streamLength, EOFMarkerChunkSize);
            //int index = -1;

            //while (index < 0 && position > 0)
            //{
            //    /*
            //      NOTE: This condition prevents the keyword from being split by the chunk boundary.
            //    */
            //    if (position < streamLength)
            //    { position += keyWord.Length; }
            //    position -= chunkSize;
            //    if (position < 0)
            //    { position = 0; }
            //    stream.Seek(position);

            //    text = stream.ReadString(chunkSize);
            //    index = text.LastIndexOf(keyWord, StringComparison.Ordinal);
            //}
            //return index < 0 ? -1 : position + index;
        }

        /**
         * Prepare for decryption.
         * 
         * @throws InvalidPasswordException If the password is incorrect.
         * @throws IOException if something went wrong
         */
        public void PrepareDecryption()
        {
            if (encryption != null)
            {
                return;
            }
            encryption = file.Encryption;
            if (encryption == null)
            {
                return;
            }

            try
            {
                DecryptionMaterial decryptionMaterial;
                if (keyStoreInputStream != null)
                {
                    var builder = new Pkcs12StoreBuilder();
                    var ks = builder.Build();
                    ks.Load(keyStoreInputStream, password.ToCharArray());// KeyStore.getInstance("PKCS12");
                    decryptionMaterial = new PublicKeyDecryptionMaterial(ks, keyAlias, password);
                }
                else
                {
                    decryptionMaterial = new StandardDecryptionMaterial(password);
                }

                securityHandler = encryption.SecurityHandler;
                securityHandler.PrepareForDecryption(encryption, file.ID.BaseDataObject, decryptionMaterial);
                accessPermission = securityHandler.CurrentAccessPermission;
            }
            catch (IOException e)
            {
                throw e;
            }
            catch (Org.BouncyCastle.Security.GeneralSecurityException e)
            {
                throw new IOException($"Error ({e.GetType().Name}) while creating security handler for decryption", e);
            }
            finally
            {
                if (keyStoreInputStream != null)
                {
                    keyStoreInputStream.Dispose();
                }
            }
        }
    }
}