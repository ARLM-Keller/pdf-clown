/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using PdfClown.Bytes;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Tokens;
using PdfClown.Util;
using PdfClown.Util.IO;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace PdfClown.Documents.Encryption
{
    /**
     * The standard security handler. This security handler protects document with password.
     * @see StandardProtectionPolicy to see how to protect document with this security handler.
     * @author Ben Litchfield
     * @author Benoit Guillon
     * @author Manuel Kasper
     */
    public sealed class StandardSecurityHandler : SecurityHandler<StandardProtectionPolicy>
    {
        private const int Revision1 = 1;
        private const int Revision2 = 2;
        private const int Revision3 = 3;
        private const int Revision4 = 4;
        private const int Revision5 = 5;
        private const int Revision6 = 6;

        /** Type of security handler. */
        public static readonly string FILTER = "Standard";

        /** Protection policy class for this handler. */
        public static readonly Type PROTECTION_POLICY_CLASS = typeof(StandardProtectionPolicy);

        /** Standard padding for encryption. */
        private static readonly byte[] ENCRYPT_PADDING = new byte[]
        {
            (byte)0x28, (byte)0xBF, (byte)0x4E, (byte)0x5E, (byte)0x4E,
            (byte)0x75, (byte)0x8A, (byte)0x41, (byte)0x64, (byte)0x00,
            (byte)0x4E, (byte)0x56, (byte)0xFF, (byte)0xFA, (byte)0x01,
            (byte)0x08, (byte)0x2E, (byte)0x2E, (byte)0x00, (byte)0xB6,
            (byte)0xD0, (byte)0x68, (byte)0x3E, (byte)0x80, (byte)0x2F,
            (byte)0x0C, (byte)0xA9, (byte)0xFE, (byte)0x64, (byte)0x53,
            (byte)0x69, (byte)0x7A
        };

        // hashes used for Algorithm 2.B, depending on remainder from E modulo 3
        private static readonly string[] HASHES_2B = new string[] { "SHA256", "SHA384", "SHA512" };

        private static readonly DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /**
		 * Constructor.
		 */
        public StandardSecurityHandler() : base()
        { }

        /**
		 * Constructor used for encryption.
		 *
		 * @param p The protection policy.
		 */
        public StandardSecurityHandler(StandardProtectionPolicy policy)
            : base(policy)
        { }

        /**
		 * Computes the revision version of the StandardSecurityHandler to
		 * use regarding the version number and the permissions bits set.
		 * See PDF Spec 1.6 p98
		 * 
		 * @param version The version number.
		 *
		 * @return The computed revision number.
		 */
        private int ComputeRevisionNumber(int version)
        {
            var protectionPolicy = ProtectionPolicy;
            var permissions = protectionPolicy.Permissions;
            if (version < Revision2 && !permissions.HasAnyRevision3PermissionSet)
            {
                return Revision2;
            }
            if (version == Revision5)
            {
                // note about revision 5: "Shall not be used. This value was used by a deprecated Adobe extension."
                return Revision6;
            }
            if (version == Revision4)
            {
                return Revision4;
            }
            if (version == Revision2 || version == Revision3 || permissions.HasAnyRevision3PermissionSet)
            {
                return Revision3;
            }
            return Revision4;
        }

        /**
		 * Prepares everything to decrypt the document.
		 *
		 * Only if decryption of single objects is needed this should be called.
		 *
		 * @param encryption  encryption dictionary
		 * @param documentIDArray  document id
		 * @param decryptionMaterial Information used to decrypt the document.
		 *
		 * @throws InvalidPasswordException If the password is incorrect.
		 * @ If there is an error accessing data.
		 */

        public override void PrepareForDecryption(PdfEncryption encryption, PdfArray documentIDArray, DecryptionMaterial decryptionMaterial)
        {
            if (!(decryptionMaterial is StandardDecryptionMaterial material))
            {
                throw new IOException("Decryption material is not compatible with the document");
            }

            // This is only used with security version 4 and 5.
            if (encryption.Version >= Revision4)
            {
                StreamFilterName = encryption.StreamFilterName;
                StringFilterName = encryption.StringFilterName;
            }
            DecryptMetadata = encryption.IsEncryptMetaData;

            string password = material.Password ?? string.Empty;

            int dicPermissions = encryption.Permissions;
            int dicRevision = encryption.Revision;
            int dicLength = encryption.Version == Revision1 ? Revision5 : encryption.Length / 8;

            if (encryption.Version == Revision4 || encryption.Version == Revision5)
            {
                // detect whether AES encryption is used. This assumes that the encryption algo is 
                // stored in the PDCryptFilterDictionary
                // However, crypt filters are used only when V is 4 or 5.
                var stdCryptFilterDictionary = encryption.StdCryptFilterDictionary;
                if (stdCryptFilterDictionary != null)
                {
                    var cryptFilterMethod = stdCryptFilterDictionary.CryptFilterMethod;
                    if (PdfName.AESV2.Equals(cryptFilterMethod))
                    {
                        dicLength = 128 / 8;
                        IsAES = true;
                        if (encryption.BaseDataObject.ContainsKey(PdfName.Length))
                        {
                            // PDFBOX-5345
                            int newLength = encryption.Length / 8;
                            if (newLength < dicLength)
                            {
                                Debug.WriteLine($"warn: Using {newLength} bytes key length instead of {dicLength} in AESV2 encryption?!");
                                dicLength = newLength;
                            }
                        }
                    }
                    if (PdfName.AESV3.Equals(cryptFilterMethod))
                    {
                        dicLength = 256 / 8;
                        IsAES = true;
                        if (encryption.BaseDataObject.ContainsKey(PdfName.Length))
                        {
                            // PDFBOX-5345
                            int newLength = encryption.Length / 8;
                            if (newLength < dicLength)
                            {
                                Debug.WriteLine($"warn: Using {newLength} bytes key length instead of {dicLength} in AESV3 encryption?!");
                                dicLength = newLength;
                            }
                        }
                    }
                }
            }


            var documentIDBytes = GetDocumentIDBytes(documentIDArray);

            // we need to know whether the meta data was encrypted for password calculation
            bool encryptMetadata = encryption.IsEncryptMetaData;

            var userKey = encryption.UserKey.Span;
            var ownerKey = encryption.OwnerKey.Span;
            ReadOnlySpan<byte> ue = null, oe = null;

            var passwordCharset = Charset.ISO88591;
            if (dicRevision == Revision6 || dicRevision == Revision5)
            {
                passwordCharset = Charset.UTF8;
                ue = encryption.UserEncryptionKey.Span;
                oe = encryption.OwnerEncryptionKey.Span;
            }

            if (dicRevision == Revision6)
            {
                password = SaslPrep.SaslPrepQuery(password); // PDFBOX-4155
            }

            if (IsOwnerPassword(passwordCharset.GetBytes(password), userKey, ownerKey,
                                     dicPermissions, documentIDBytes, dicRevision,
                                     dicLength, encryptMetadata))
            {
                CurrentAccessPermission = AccessPermission.GetOwnerAccessPermission();

                ReadOnlySpan<byte> computedPassword = dicRevision == Revision6 || dicRevision == Revision5
                    ? (ReadOnlySpan<byte>)passwordCharset.GetBytes(password)
                    : GetUserPassword(passwordCharset.GetBytes(password),
                            ownerKey, dicRevision, dicLength);
                EncryptionKey = ComputeEncryptedKey(
                        computedPassword,
                        ownerKey, userKey, oe, ue,
                        dicPermissions,
                        documentIDBytes,
                        dicRevision,
                        dicLength,
                        encryptMetadata, true)
                    .ToArray();
            }
            else if (IsUserPassword(passwordCharset.GetBytes(password), userKey, ownerKey,
                               dicPermissions, documentIDBytes, dicRevision,
                               dicLength, encryptMetadata))
            {

                CurrentAccessPermission = new AccessPermission(dicPermissions)
                {
                    IsReadOnly = true
                };

                EncryptionKey = ComputeEncryptedKey(
                        passwordCharset.GetBytes(password),
                        ownerKey, userKey, oe, ue,
                        dicPermissions,
                        documentIDBytes,
                        dicRevision,
                        dicLength,
                        encryptMetadata, false)
                    .ToArray();
            }
            else
            {
                throw new InvalidPasswordException("Cannot decrypt PDF, the password is incorrect");
            }

            if (dicRevision == Revision6 || dicRevision == Revision5)
            {
                ValidatePerms(encryption, dicPermissions, encryptMetadata);
            }
        }

        private ReadOnlySpan<byte> GetDocumentIDBytes(PdfArray documentIDArray)
        {
            //some documents may not have document id, see
            //test\encryption\encrypted_doc_no_id.pdf
            ReadOnlySpan<byte> documentIDBytes;
            if (documentIDArray != null
                && documentIDArray.Count >= 1
                && documentIDArray.Resolve(0) is PdfString id)
            {
                documentIDBytes = id.RawValue.Span;
            }
            else
            {
                documentIDBytes = ReadOnlySpan<byte>.Empty;
            }
            return documentIDBytes;
        }

        // Algorithm 13: validate permissions ("Perms" field). Relaxed to accommodate buggy encoders
        // https://www.adobe.com/content/dam/Adobe/en/devnet/acrobat/pdfs/adobe_supplement_iso32000.pdf
        private void ValidatePerms(PdfEncryption encryption, int dicPermissions, bool encryptMetadata)
        {
            try
            {
                // "Decrypt the 16-byte Perms string using AES-256 in ECB mode with an 
                // initialization vector of zero and the file encryption key as the key."
                //@SuppressWarnings({ "squid:S4432"})
                Span<byte> perms = null;
                using var cipher = Aes.Create("AES");
                cipher.Mode = CipherMode.ECB;
                cipher.Key = EncryptionKey;
                cipher.Padding = PaddingMode.None;
                using (var mstream = new ByteStream(encryption.Perms))
                using (var ostream = new MemoryStream())
                using (var stream = new CryptoStream(mstream, cipher.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    stream.CopyTo(ostream);
                    perms = ostream.AsSpan();
                }

                // "Verify that bytes 9-11 of the result are the characters ‘a’, ‘d’, ‘b’."
                if (perms[9] != 'a' || perms[10] != 'd' || perms[11] != 'b')
                {
                    Debug.WriteLine("warn: Verification of permissions failed (constant)");
                }

                // "Bytes 0-3 of the decrypted Perms entry, treated as a little-endian integer, 
                // are the user permissions. They should match the value in the P key."
                // perms[0] & 0xFF | (perms[1] & 0xFF) << 8 | (perms[2] & 0xFF) << 16 | (perms[3] & 0xFF) << 24
                int permsP = ConvertUtils.ReadInt32(perms, 0, ByteOrderEnum.LittleEndian);

                if (permsP != dicPermissions)
                {
                    Debug.WriteLine($"warn: Verification of permissions failed ({$"{permsP:X8}"} != {$"{dicPermissions:X8}"})");
                }

                if (encryptMetadata && perms[8] != 'T' || !encryptMetadata && perms[8] != 'F')
                {
                    Debug.WriteLine("warn: Verification of permissions failed (EncryptMetadata)");
                }
            }
            catch (Exception e)
            {
                LogIfStrongEncryptionMissing();
                throw new IOException("ValidatePerms", e);
            }
        }

        /**
		 * Prepare document for encryption.
		 *
		 * @param document The document to encrypt.
		 *
		 * @ If there is an error accessing data.
		 */
        public override void PrepareDocumentForEncryption(Document document)
        {
            PdfEncryption encryptionDictionary = document.File.Encryption;
            if (encryptionDictionary == null)
            {
                encryptionDictionary = new PdfEncryption(document.File);
            }
            int version = ComputeVersionNumber();
            int revision = ComputeRevisionNumber(version);
            encryptionDictionary.Filter = FILTER;
            encryptionDictionary.Version = version;
            if (version != Revision4 && version != Revision5)
            {
                // remove CF, StmF, and StrF entries that may be left from a previous encryption
                encryptionDictionary.RemoveV45filters();
            }
            encryptionDictionary.Revision = revision;
            encryptionDictionary.Length = KeyLength;

            string ownerPassword = ProtectionPolicy.OwnerPassword ?? string.Empty;
            string userPassword = ProtectionPolicy.UserPassword ?? string.Empty;
            // If no owner password is set, use the user password instead.
            if (ownerPassword.Length == 0)
            {
                ownerPassword = userPassword;
            }

            int permissionInt = ProtectionPolicy.Permissions.PermissionBytes;

            encryptionDictionary.Permissions = permissionInt;

            int length = KeyLength / 8;

            if (revision == Revision6)
            {
                // PDFBOX-4155
                ownerPassword = SaslPrep.SaslPrepStored(ownerPassword);
                userPassword = SaslPrep.SaslPrepStored(userPassword);
                PrepareEncryptionDictRev6(ownerPassword, userPassword, encryptionDictionary, permissionInt);
            }
            else

            {
                PrepareEncryptionDictRev234(ownerPassword, userPassword, encryptionDictionary, permissionInt,
                        document, revision, length);
            }

            document.File.Encryption = encryptionDictionary;
        }

        private void PrepareEncryptionDictRev6(string ownerPassword, string userPassword,
                PdfEncryption encryptionDictionary, int permissionInt)
        {
            try
            {
                var rnd = new SecureRandom();
                using (var cipher = Aes.Create("AES"))
                {
                    cipher.Mode = CipherMode.CBC;
                    cipher.Padding = PaddingMode.None;
                    // make a random 256-bit file encryption key
                    EncryptionKey = new byte[32];
                    rnd.NextBytes(EncryptionKey);

                    // Algorithm 8a: Compute U
                    var userPasswordBytes = Truncate127(Charset.UTF8.GetBytes(userPassword));
                    byte[] userValidationSalt = new byte[8];
                    byte[] userKeySalt = new byte[8];
                    rnd.NextBytes(userValidationSalt);
                    rnd.NextBytes(userKeySalt);
                    var hashU = ComputeHash2B(Concat(userPasswordBytes, userValidationSalt), userPasswordBytes, null);
                    var u = Concat(hashU, userValidationSalt, userKeySalt);

                    // Algorithm 8b: Compute UE
                    var hashUE = ComputeHash2B(Concat(userPasswordBytes, userKeySalt), userPasswordBytes, null);
                    byte[] ue = null;

                    using (var enciptor = cipher.CreateEncryptor(hashUE.ToArray(), new byte[16]))// "an initialization vector of zero"
                    { ue = enciptor.DoFinal(EncryptionKey); }

                    // Algorithm 9a: Compute O
                    var ownerPasswordBytes = Truncate127(Charset.UTF8.GetBytes(ownerPassword));
                    byte[] ownerValidationSalt = new byte[8];
                    byte[] ownerKeySalt = new byte[8];
                    rnd.NextBytes(ownerValidationSalt);
                    rnd.NextBytes(ownerKeySalt);
                    var hashO = ComputeHash2B(Concat(ownerPasswordBytes, ownerValidationSalt, u), ownerPasswordBytes, u);
                    var o = Concat(hashO, ownerValidationSalt, ownerKeySalt);

                    // Algorithm 9b: Compute OE
                    var hashOE = ComputeHash2B(Concat(ownerPasswordBytes, ownerKeySalt, u), ownerPasswordBytes, u);
                    byte[] oe = null;
                    using (var enciptor = cipher.CreateEncryptor(hashOE.ToArray(), new byte[16]))// "an initialization vector of zero"
                    { oe = enciptor.DoFinal(EncryptionKey); }

                    // Set keys and other required constants in encryption dictionary
                    encryptionDictionary.UserKey = u.ToArray();
                    encryptionDictionary.UserEncryptionKey = ue;
                    encryptionDictionary.OwnerKey = o.ToArray();
                    encryptionDictionary.OwnerEncryptionKey = oe;

                    PrepareEncryptionDictAES(encryptionDictionary, PdfName.AESV3);

                    // Algorithm 10: compute "Perms" value
                    byte[] perms = new byte[16];
                    ConvertUtils.WriteInt32(perms.AsSpan(0, 4), permissionInt, ByteOrderEnum.LittleEndian);
                    Array.Fill(perms, (byte)0xFF, 4, 4);
                    perms[8] = (byte)'T';    // we always encrypt Metadata
                    perms[9] = (byte)'a';
                    perms[10] = (byte)'d';
                    perms[11] = (byte)'b';
                    rnd.NextBytes(perms, 12, 4);

                    byte[] permsEnc = null;
                    using (var enciptor = cipher.CreateEncryptor(EncryptionKey, new byte[16])) // "an initialization vector of zero"
                    { permsEnc = enciptor.DoFinal(perms); }

                    encryptionDictionary.Perms = permsEnc;
                }
            }
            catch (Exception e)
            {
                LogIfStrongEncryptionMissing();
                throw new IOException("PrepareEncryptionDictRev6", e);
            }
        }

        private void PrepareEncryptionDictRev234(string ownerPassword, string userPassword,
                PdfEncryption encryptionDictionary, int permissionInt, Document document,
                int revision, int length)
        {
            var idArray = document.File.ID;

            //check if the document has an id yet.  If it does not then generate one
            if (idArray == null || idArray.BaseDataObject.Count < 2)
            {
                using (var md = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
                {
                    var time = new BigInteger((DateTime.UtcNow - Jan1st1970).TotalMilliseconds.ToString());
                    md.Update(time.ToByteArray());
                    md.Update(Charset.ISO88591.GetBytes(ownerPassword));
                    md.Update(Charset.ISO88591.GetBytes(userPassword));
                    md.Update(Charset.ISO88591.GetBytes(document.Information.ToString()));

                    var finBlock = Charset.ISO88591.GetBytes(this.ToString());
                    PdfString idString = new PdfString(md.Digest(finBlock));

                    idArray = new FileIdentifier();
                    idArray.BaseID = idString;
                    idArray.VersionID = idString;
                    document.File.ID = idArray;
                }
            }

            PdfString id = idArray.BaseID;

            var ownerBytes = ComputeOwnerPassword(
                    Charset.ISO88591.GetBytes(ownerPassword),
                    Charset.ISO88591.GetBytes(userPassword), revision, length);

            var userBytes = ComputeUserPassword(
                    Charset.ISO88591.GetBytes(userPassword),
                    ownerBytes, permissionInt, id.RawValue.Span, revision, length, true);

            EncryptionKey = ComputeEncryptedKey(Charset.ISO88591.GetBytes(userPassword), ownerBytes,
                        null, null, null, permissionInt, id.RawValue.Span, revision, length, true, false)
                .ToArray();

            encryptionDictionary.OwnerKey = ownerBytes.ToArray();
            encryptionDictionary.UserKey = userBytes.ToArray();

            if (revision == Revision4)
            {
                PrepareEncryptionDictAES(encryptionDictionary, PdfName.AESV2);
            }
        }

        private void PrepareEncryptionDictAES(PdfEncryption encryptionDictionary, PdfName aesVName)
        {
            var cryptFilterDictionary = new PdfCryptFilterDictionary(encryptionDictionary.File);
            cryptFilterDictionary.CryptFilterMethod = aesVName;
            cryptFilterDictionary.Length = KeyLength;
            encryptionDictionary.StdCryptFilterDictionary = cryptFilterDictionary;
            encryptionDictionary.StreamFilterName = PdfName.StdCF;
            encryptionDictionary.StringFilterName = PdfName.StdCF;
            IsAES = true;
        }

        /**
		 * Check for owner password.
		 *
		 * @param ownerPassword The owner password.
		 * @param user The u entry of the encryption dictionary.
		 * @param owner The o entry of the encryption dictionary.
		 * @param permissions The set of permissions on the document.
		 * @param id The document id.
		 * @param encRevision The encryption algorithm revision.
		 * @param keyLengthInBytes The encryption key length in bytes.
		 * @param encryptMetadata The encryption metadata
		 *
		 * @return True If the ownerPassword param is the owner password.
		 *
		 * @ If there is an error accessing data.
		 */
        public bool IsOwnerPassword(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner,
                                       int permissions, ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes,
                                       bool encryptMetadata)
        {
            switch (encRevision)
            {
                case Revision2:
                case Revision3:
                case Revision4:
                    return IsOwnerPassword234(ownerPassword, user, owner, permissions, id, encRevision, keyLengthInBytes, encryptMetadata);
                case Revision6:
                case Revision5:
                    return IsOwnerPassword56(ownerPassword, user, owner, encRevision);
                default:
                    throw new IOException("Unknown Encryption Revision " + encRevision);

            }
        }

        private bool IsOwnerPassword234(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int permissions,
           ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            var userPassword = GetUserPassword234(ownerPassword, owner, encRevision, keyLengthInBytes);
            return IsUserPassword234(userPassword, user, owner, permissions, id, encRevision, keyLengthInBytes, encryptMetadata);
        }

        private bool IsOwnerPassword56(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int encRevision)
        {
            if (owner.Length < 40)
            {
                // PDFBOX-5104
                throw new IOException("Owner password is too short");
            }
            var truncatedOwnerPassword = Truncate127(ownerPassword);
            var oHash = owner.Slice(0, 32);
            var oValidationSalt = owner.Slice(32, 8);

            if (encRevision == Revision5)
            {
                return MemoryExtensions.SequenceEqual(ComputeSHA256(truncatedOwnerPassword, oValidationSalt, user), oHash);
            }
            else
            {
                return MemoryExtensions.SequenceEqual(ComputeHash2A(truncatedOwnerPassword, oValidationSalt, user), oHash);
            }
        }

        /**
     * Get the user password based on the owner password.
     *
     * @param ownerPassword The plaintext owner password.
     * @param owner The o entry of the encryption dictionary.
     * @param encRevision The encryption revision number.
     * @param length The key length.
     *
     * @return The u entry of the encryption dictionary.
     *
     * @throws IOException If there is an error accessing data while generating the user password.
     */
        public ReadOnlySpan<byte> GetUserPassword(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> owner, int encRevision,
                                       int length)
        {
            // TODO ?!?!
            if (encRevision == Revision5 || encRevision == Revision6)
            {
                return new byte[0];
            }
            else
            {
                return GetUserPassword234(ownerPassword, owner, encRevision, length);
            }
        }

        private ReadOnlySpan<byte> GetUserPassword234(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> owner, int encRevision, int length)
        {
            var result = new ByteStream(owner.Length);
            var rc4Key = ComputeRC4key(ownerPassword, encRevision, length);

            if (encRevision == Revision2)
            {
                EncryptDataRC4(rc4Key, owner, result);
            }
            else if (encRevision == Revision3 || encRevision == Revision4)
            {
                byte[] iterationKey = new byte[rc4Key.Length];
                result.Write(owner);

                for (int i = 19; i >= 0; i--)
                {
                    rc4Key.CopyTo(iterationKey);
                    for (int j = 0; j < iterationKey.Length; j++)
                    {
                        iterationKey[j] = (byte)(iterationKey[j] ^ (byte)i);
                    }

                    var otemp = result.ToArray();
                    result.SetLength(0);

                    EncryptDataRC4(iterationKey, otemp, result);
                }
            }
            return result.AsSpan();
        }


        /**
		 * Compute the encryption key.
		 *
		 * @param password The password to compute the encrypted key.
		 * @param o The O entry of the encryption dictionary.
		 * @param u The U entry of the encryption dictionary.
		 * @param oe The OE entry of the encryption dictionary.
		 * @param ue The UE entry of the encryption dictionary.
		 * @param permissions The permissions for the document.
		 * @param id The document id.
		 * @param encRevision The revision of the encryption algorithm.
		 * @param keyLengthInBytes The length of the encryption key in bytes.
		 * @param encryptMetadata The encryption metadata
		 * @param isOwnerPassword whether the password given is the owner password (for revision 6)
		 *
		 * @return The encrypted key bytes.
		 *
		 * @ If there is an error with encryption.
		 */
        public ReadOnlySpan<byte> ComputeEncryptedKey(ReadOnlySpan<byte> password, ReadOnlySpan<byte> o, ReadOnlySpan<byte> u, ReadOnlySpan<byte> oe, ReadOnlySpan<byte> ue,
                                          int permissions, ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes,
                                          bool encryptMetadata, bool isOwnerPassword)
        {
            return encRevision == Revision6 || encRevision == Revision5
                ? ComputeEncryptedKeyRev56(password, isOwnerPassword, o, u, oe, ue, encRevision)
                : ComputeEncryptedKeyRev234(password, o, permissions, id, encryptMetadata, keyLengthInBytes, encRevision);
        }

        private ReadOnlySpan<byte> ComputeEncryptedKeyRev234(ReadOnlySpan<byte> password, ReadOnlySpan<byte> o, int permissions,
                ReadOnlySpan<byte> id, bool encryptMetadata, int length, int encRevision)
        {
            //Algorithm 2, based on MD5

            //PDFReference 1.4 pg 78
            var padded = TruncateOrPad(password);

            using var md = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            md.Update(padded);

            md.Update(o);

            md.Update((byte)permissions);
            md.Update((byte)((uint)permissions >> 8));
            md.Update((byte)((uint)permissions >> 16));
            md.Update((byte)((uint)permissions >> 24));

            md.Update(id);

            //(Security handlers of revision 4 or greater) If document metadata is not being
            // encrypted, pass 4 bytes with the value 0xFFFFFFFF to the MD5 hash function.
            //see 7.6.3.3 Algorithm 2 Step f of PDF 32000-1:2008
            if (encRevision == Revision4 && !encryptMetadata)
            {
                md.Update(new byte[] { (byte)0xff, (byte)0xff, (byte)0xff, (byte)0xff });
            }
            byte[] digest = md.Digest();

            if (encRevision == Revision3 || encRevision == Revision4)
            {
                for (int i = 0; i < 50; i++)
                {
                    digest = md.Digest(digest, 0, length);
                }
            }

            return digest.AsSpan(0, length);
        }

        private ReadOnlySpan<byte> ComputeEncryptedKeyRev56(ReadOnlySpan<byte> password, bool isOwnerPassword,
                ReadOnlySpan<byte> o, ReadOnlySpan<byte> u, ReadOnlySpan<byte> oe, ReadOnlySpan<byte> ue, int encRevision)
        {
            ReadOnlySpan<byte> hash, fileKeyEnc;

            if (isOwnerPassword)
            {
                var oKeySalt = o.Slice(40, 8);

                hash = encRevision == Revision5
                    ? ComputeSHA256(password, oKeySalt, u)
                    : ComputeHash2A(password, oKeySalt, u);

                fileKeyEnc = oe;
            }
            else
            {
                var uKeySalt = u.Slice(40, 8);

                hash = encRevision == Revision5
                    ? ComputeSHA256(password, uKeySalt, null)
                    : ComputeHash2A(password, uKeySalt, null);

                fileKeyEnc = ue;
            }
            try
            {
                var bufferKey = fileKeyEnc.ToArray();
                using (var cipher = Aes.Create("AES"))
                {
                    //cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(hash, "AES"), new IvParameterSpec());
                    cipher.Mode = CipherMode.CBC;
                    cipher.Padding = PaddingMode.None;
                    cipher.Key = hash.ToArray();
                    cipher.IV = new byte[16];
                    using (var tempStream = new ByteStream(bufferKey))
                    using (var outStream = new MemoryStream())
                    using (var cryptoStream = new CryptoStream(tempStream, cipher.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        cryptoStream.CopyTo(outStream);
                        return outStream.AsSpan();
                    }
                }
            }
            catch (Exception e)
            {
                LogIfStrongEncryptionMissing();
                throw new IOException("ComputeEncryptedKeyRev56", e);
            }
        }

        /**
		 * This will compute the user password hash.
		 *
		 * @param password The plain text password.
		 * @param owner The owner password hash.
		 * @param permissions The document permissions.
		 * @param id The document id.
		 * @param encRevision The revision of the encryption.
		 * @param keyLengthInBytes The length of the encryption key in bytes.
		 * @param encryptMetadata The encryption metadata
		 *
		 * @return The user password.
		 *
		 * @ if the password could not be computed
		 */
        public ReadOnlySpan<byte> ComputeUserPassword(ReadOnlySpan<byte> password, ReadOnlySpan<byte> owner, int permissions,
                                          ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes,
                                          bool encryptMetadata)
        {
            // TODO!?!?
            if (encRevision == Revision5 || encRevision == Revision6)
            {
                return new byte[0];
            }

            using var result = new ByteStream(32);
            var encKey = ComputeEncryptedKeyRev234(password, owner, permissions,
                    id, encryptMetadata, keyLengthInBytes, encRevision);

            if (encRevision == Revision2)
            {
                EncryptDataRC4(encKey, ENCRYPT_PADDING, result);
            }
            else if (encRevision == Revision3 || encRevision == Revision4)
            {
                var md = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
                md.Update(ENCRYPT_PADDING);
                md.Update(id);
                result.Write(md.Digest());

                byte[] iterationKey = new byte[encKey.Length];
                for (int i = 0; i < 20; i++)
                {
                    encKey.CopyTo(iterationKey);
                    for (int j = 0; j < iterationKey.Length; j++)
                    {
                        iterationKey[j] = (byte)(iterationKey[j] ^ i);
                    }
                    var input = result.ToArray();
                    result.SetLength(0);
                    EncryptDataRC4(iterationKey, input, result);
                }

                byte[] finalResult = new byte[32];
                result.AsMemory().Slice(0, 16).CopyTo(finalResult);
                ENCRYPT_PADDING.AsSpan(0, 16).CopyTo(finalResult.AsSpan(16, 16));
                return finalResult;
            }
            return result.AsSpan();
        }

        /**
		 * Compute the owner entry in the encryption dictionary.
		 *
		 * @param ownerPassword The plaintext owner password.
		 * @param userPassword The plaintext user password.
		 * @param encRevision The revision number of the encryption algorithm.
		 * @param length The length of the encryption key.
		 *
		 * @return The o entry of the encryption dictionary.
		 *
		 * @ if the owner password could not be computed
		 */
        public ReadOnlySpan<byte> ComputeOwnerPassword(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> userPassword,
                                           int encRevision, int length)
        {
            if (encRevision == Revision2 && length != Revision5)
            {
                throw new IOException("Expected length=5 actual=" + length);
            }

            var rc4Key = ComputeRC4key(ownerPassword, encRevision, length);
            var paddedUser = TruncateOrPad(userPassword);

            using var output = new MemoryStream();
            EncryptDataRC4(rc4Key, paddedUser, output);

            if (encRevision == Revision3 || encRevision == Revision4)
            {
                byte[] iterationKey = new byte[rc4Key.Length];
                for (int i = 1; i < 20; i++)
                {
                    rc4Key.CopyTo(iterationKey);
                    for (int j = 0; j < iterationKey.Length; j++)
                    {
                        iterationKey[j] = (byte)(iterationKey[j] ^ (byte)i);
                    }
                    var input = output.ToArray();
                    output.Reset();

                    EncryptDataRC4(iterationKey, input, output);
                }
            }

            return output.AsSpan();
        }

        // steps (a) to (d) of "Algorithm 3: Computing the encryption dictionary’s O (owner password) value".
        private ReadOnlySpan<byte> ComputeRC4key(ReadOnlySpan<byte> ownerPassword, int encRevision, int length)
        {
            using var md = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            byte[] digest = md.Digest(TruncateOrPad(ownerPassword));
            if (encRevision == Revision3 || encRevision == Revision4)
            {
                for (int i = 0; i < 50; i++)
                {
                    // this deviates from the spec - however, omitting the length
                    // parameter prevents the file to be opened in Adobe Reader
                    // with the owner password when the key length is 40 bit (= 5 bytes)
                    digest = md.Digest(digest, 0, length);
                }
            }
            return digest.AsSpan(0, length);
        }


        /**
		 * This will take the password and truncate or pad it as necessary.
		 *
		 * @param password The password to pad or truncate.
		 *
		 * @return The padded or truncated password.
		 */
        private ReadOnlySpan<byte> TruncateOrPad(ReadOnlySpan<byte> password)
        {
            byte[] padded = new byte[ENCRYPT_PADDING.Length];
            int bytesBeforePad = Math.Min(password.Length, padded.Length);
            password.CopyTo(padded.AsSpan(0, bytesBeforePad));
            ENCRYPT_PADDING.AsSpan(0, ENCRYPT_PADDING.Length - bytesBeforePad).CopyTo(padded.AsSpan(bytesBeforePad));
            return padded;
        }

        /**
		 * Check if a plaintext password is the user password.
		 *
		 * @param password The plaintext password.
		 * @param user The u entry of the encryption dictionary.
		 * @param owner The o entry of the encryption dictionary.
		 * @param permissions The permissions set in the PDF.
		 * @param id The document id used for encryption.
		 * @param encRevision The revision of the encryption algorithm.
		 * @param keyLengthInBytes The length of the encryption key in bytes.
		 * @param encryptMetadata The encryption metadata.
		 *
		 * @return true If the plaintext password is the user password.
		 *
		 * @ If there is an error accessing data.
		 */
        public bool IsUserPassword(ReadOnlySpan<byte> password, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int permissions,
                                      ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            switch (encRevision)
            {
                case Revision2:
                case Revision3:
                case Revision4:
                    return IsUserPassword234(password, user, owner, permissions, id, encRevision,
                                             keyLengthInBytes, encryptMetadata);
                case Revision5:
                case Revision6:
                    return IsUserPassword56(password, user, encRevision);
                default:
                    throw new IOException("Unknown Encryption Revision " + encRevision);
            }
        }

        private bool IsUserPassword234(ReadOnlySpan<byte> password, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int permissions,
                ReadOnlySpan<byte> id, int encRevision, int length, bool encryptMetadata)
        {
            var passwordBytes = ComputeUserPassword(password, owner, permissions, id, encRevision,
                                                       length, encryptMetadata);
            return encRevision switch
            {
                Revision2 => user.SequenceEqual(passwordBytes),
                _ => user.Slice(0, 16).SequenceEqual(passwordBytes.Slice(0, 16))
            };
        }

        private bool IsUserPassword56(ReadOnlySpan<byte> password, ReadOnlySpan<byte> user, int encRevision)
        {
            var truncatedPassword = Truncate127(password);
            var uHash = user.Slice(0, 32);
            var uValidationSalt = user.Slice(32, 8);

            var hash = encRevision == Revision5
                ? ComputeSHA256(truncatedPassword, uValidationSalt, ReadOnlySpan<byte>.Empty)
                : ComputeHash2A(truncatedPassword, uValidationSalt, ReadOnlySpan<byte>.Empty);
            return hash.SequenceEqual(uHash);
        }

        /**
		 * Check if a plaintext password is the user password.
		 *
		 * @param password The plaintext password.
		 * @param user The u entry of the encryption dictionary.
		 * @param owner The o entry of the encryption dictionary.
		 * @param permissions The permissions set in the PDF.
		 * @param id The document id used for encryption.
		 * @param encRevision The revision of the encryption algorithm.
		 * @param keyLengthInBytes The length of the encryption key in bytes.
		 * @param encryptMetadata The encryption metadata
		 *
		 * @return true If the plaintext password is the user password.
		 *
		 * @ If there is an error accessing data.
		 */
        public bool IsUserPassword(string password, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int permissions,
                                      ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            var passwordBytes = encRevision == Revision6 || encRevision == Revision5
                ? Charset.UTF8.GetBytes(password)
                : Charset.ISO88591.GetBytes(password);
            return IsUserPassword(passwordBytes, user, owner, permissions, id,
                    encRevision, keyLengthInBytes, encryptMetadata);
        }

        /**
		 * Check for owner password.
		 *
		 * @param password The owner password.
		 * @param user The u entry of the encryption dictionary.
		 * @param owner The o entry of the encryption dictionary.
		 * @param permissions The set of permissions on the document.
		 * @param id The document id.
		 * @param encRevision The encryption algorithm revision.
		 * @param keyLengthInBytes The encryption key length in bytes.
		 * @param encryptMetadata The encryption metadata
		 *
		 * @return True If the ownerPassword param is the owner password.
		 *
		 * @ If there is an error accessing data.
		 */
        public bool IsOwnerPassword(string password, ReadOnlySpan<byte> user, ReadOnlySpan<byte> owner, int permissions,
                                       ReadOnlySpan<byte> id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            return IsOwnerPassword(Charset.ISO88591.GetBytes(password), user, owner, permissions, id,
                                   encRevision, keyLengthInBytes, encryptMetadata);
        }

        // Algorithm 2.A from ISO 32000-1
        private ReadOnlySpan<byte> ComputeHash2A(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> u)
        {
            var userKey = AdjustUserKey(u);
            var truncatedPassword = Truncate127(password);
            var input = Concat(truncatedPassword, salt, userKey);
            return ComputeHash2B(input, truncatedPassword, userKey);
        }

        // Algorithm 2.B from ISO 32000-2
        private static ReadOnlySpan<byte> ComputeHash2B(ReadOnlySpan<byte> input, ReadOnlySpan<byte> password, ReadOnlySpan<byte> userKey)
        {
            try
            {
                var md = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                Span<byte> k = md.Digest(input);

                byte[] e = null;
                for (int round = 0; round < 64 || (e[^1] & 0xFF) > round - 32; round++)
                {
                    byte[] k1 = (!userKey.IsEmpty && userKey.Length >= 48)
                        ? new byte[64 * (password.Length + k.Length + 48)]
                        : new byte[64 * (password.Length + k.Length)];

                    int pos = 0;
                    for (int i = 0; i < 64; i++)
                    {
                        password.CopyTo(k1.AsSpan(pos, password.Length));
                        pos += password.Length;
                        k.CopyTo(k1.AsSpan(pos, k.Length));
                        pos += k.Length;
                        if (userKey != null && userKey.Length >= 48)
                        {
                            userKey.CopyTo(k1.AsSpan(pos, 48));
                            pos += 48;
                        }
                    }

                    using var cipher = Aes.Create("AES");
                    cipher.Mode = CipherMode.CBC;
                    cipher.Padding = PaddingMode.None;
                    cipher.Key = k.Slice(0, 16).ToArray();
                    cipher.IV = k.Slice(16, 16).ToArray();
                    //cipher.init(Cipher.ENCRYPT_MODE, keySpec, ivSpec);
                    using (var ecriptor = cipher.CreateEncryptor())
                        e = ecriptor.DoFinal(k1);

                    var bi = new BigInteger(1, e.AsSpan(0, 16).ToArray());
                    var remainder = bi.Mod(new BigInteger("3"));
                    string nextHash = HASHES_2B[remainder.IntValue];
                    //md = MessageDigest.getInstance(nextHash);
                    md.Dispose();
                    switch (nextHash)
                    {
                        case "SHA256": md = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); break;
                        case "SHA384": md = IncrementalHash.CreateHash(HashAlgorithmName.SHA384); break;
                        case "SHA512": md = IncrementalHash.CreateHash(HashAlgorithmName.SHA512); break;
                    }
                    k = md.Digest(e);
                }
                md.Dispose();
                return k.Length > 32
                    ? k.Slice(0, 32)
                    : k;
            }
            catch (Exception e)
            {
                LogIfStrongEncryptionMissing();
                throw new IOException("ComputeHash2B", e);
            }
        }

        private static ReadOnlySpan<byte> ComputeSHA256(ReadOnlySpan<byte> input, ReadOnlySpan<byte> password, ReadOnlySpan<byte> userKey)
        {
            try
            {
                using var md = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                md.Update(input);
                md.Update(password);
                return md.Digest(AdjustUserKey(userKey));
            }
            catch (Exception e)
            {
                throw new IOException("ComputeSHA256", e);
            }
        }

        private static ReadOnlySpan<byte> AdjustUserKey(ReadOnlySpan<byte> u)
        {
            if (u.IsEmpty)
            {
                return Array.Empty<byte>();
            }
            if (u.Length < 48)
            {
                throw new IOException("Bad U length");
            }
            if (u.Length > 48)
            {
                // must truncate
                return u[..48];
            }
            return u;
        }

        private static ReadOnlySpan<byte> Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            byte[] o = new byte[a.Length + b.Length];
            a.CopyTo(o);
            b.CopyTo(o.AsSpan(a.Length));
            return o;
        }

        private static ReadOnlySpan<byte> Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c)
        {
            byte[] o = new byte[a.Length + b.Length + c.Length];
            a.CopyTo(o);
            b.CopyTo(o.AsSpan(a.Length));
            c.CopyTo(o.AsSpan(a.Length + b.Length));
            return o;
        }

        private static ReadOnlySpan<byte> Truncate127(ReadOnlySpan<byte> inp)
        {
            if (inp.Length <= 127)
            {
                return inp;
            }
            return inp.Slice(0, 127);
        }

        private static void LogIfStrongEncryptionMissing()
        {
            try
            {
                //if (Rijndael.getMaxAllowedKeyLength("AES") != int.MaxValue)
                //{
                //    Debug.WriteLine("warn: JCE unlimited strength jurisdiction policy files are not installed");
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine("debug: AES Algorithm not available " + ex);
            }
        }
    }
}