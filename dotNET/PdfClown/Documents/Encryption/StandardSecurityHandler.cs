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
using PdfClown.Files;
using PdfClown.Objects;
using PdfClown.Tokens;
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
    public sealed class StandardSecurityHandler : SecurityHandler
    {


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

        private static readonly int DEFAULT_VERSION = 1;

        private StandardProtectionPolicy policy;

        /**
		 * Constructor.
		 */
        public StandardSecurityHandler()
        {
        }

        /**
		 * Constructor used for encryption.
		 *
		 * @param p The protection policy.
		 */
        public StandardSecurityHandler(StandardProtectionPolicy p)
        {
            policy = p;
            keyLength = policy.EncryptionKeyLength;
        }

        /**
		 * Computes the version number of the StandardSecurityHandler based on the encryption key
		 * length. See PDF Spec 1.6 p 93 and
		 * <a href="https://www.adobe.com/content/dam/acom/en/devnet/pdf/adobe_supplement_iso32000.pdf">PDF
		 * 1.7 Supplement ExtensionLevel: 3</a> and
		 * <a href="http://intranet.pdfa.org/wp-content/uploads/2016/08/ISO_DIS_32000-2-DIS4.pdf">PDF
		 * Spec 2.0</a>.
		 *
		 * @return The computed version number.
		 */
        private int ComputeVersionNumber()
        {
            if (keyLength == 40)
            {
                return DEFAULT_VERSION;
            }
            else if (keyLength == 128 && policy.IsPreferAES)
            {
                return 4;
            }
            else if (keyLength == 256)
            {
                return 5;
            }

            return 2;
        }

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
            if (version < 2 && !policy.Permissions.HasAnyRevision3PermissionSet)
            {
                return 2;
            }
            if (version == 5)
            {
                // note about revision 5: "Shall not be used. This value was used by a deprecated Adobe extension."
                return 6;
            }
            if (version == 4)
            {
                return 4;
            }
            if (version == 2 || version == 3 || policy.Permissions.HasAnyRevision3PermissionSet)
            {
                return 3;
            }
            return 4;
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
            if (!(decryptionMaterial is StandardDecryptionMaterial))
            {
                throw new IOException("Decryption material is not compatible with the document");
            }

            // This is only used with security version 4 and 5.
            if (encryption.Version >= 4)
            {
                SetStreamFilterName(encryption.StreamFilterName);
                SetStringFilterName(encryption.StreamFilterName);
            }
            SetDecryptMetadata(encryption.IsEncryptMetaData);
            StandardDecryptionMaterial material = (StandardDecryptionMaterial)decryptionMaterial;

            string password = material.Password ?? string.Empty;

            int dicPermissions = encryption.Permissions;
            int dicRevision = encryption.Revision;
            int dicLength = encryption.Version == 1 ? 5 : encryption.Length / 8;

            byte[] documentIDBytes = GetDocumentIDBytes(documentIDArray);

            // we need to know whether the meta data was encrypted for password calculation
            bool encryptMetadata = encryption.IsEncryptMetaData;

            byte[] userKey = encryption.UserKey;
            byte[] ownerKey = encryption.OwnerKey;
            byte[] ue = null, oe = null;

            var passwordCharset = Charset.ISO88591;
            if (dicRevision == 6 || dicRevision == 5)
            {
                passwordCharset = Charset.UTF8;
                ue = encryption.UserEncryptionKey;
                oe = encryption.OwnerEncryptionKey;
            }

            if (dicRevision == 6)
            {
                password = SaslPrep.SaslPrepQuery(password); // PDFBOX-4155
            }

            AccessPermission currentAccessPermission;

            if (IsOwnerPassword(passwordCharset.GetBytes(password), userKey, ownerKey,
                                     dicPermissions, documentIDBytes, dicRevision,
                                     dicLength, encryptMetadata))
            {
                currentAccessPermission = AccessPermission.getOwnerAccessPermission();
                CurrentAccessPermission = currentAccessPermission;

                byte[] computedPassword;
                if (dicRevision == 6 || dicRevision == 5)
                {
                    computedPassword = passwordCharset.GetBytes(password);
                }
                else
                {
                    computedPassword = GetUserPassword(passwordCharset.GetBytes(password),
                            ownerKey, dicRevision, dicLength);
                }

                encryptionKey =
                    ComputeEncryptedKey(
                        computedPassword,
                        ownerKey, userKey, oe, ue,
                        dicPermissions,
                        documentIDBytes,
                        dicRevision,
                        dicLength,
                        encryptMetadata, true);
            }
            else if (IsUserPassword(passwordCharset.GetBytes(password), userKey, ownerKey,
                               dicPermissions, documentIDBytes, dicRevision,
                               dicLength, encryptMetadata))
            {
                currentAccessPermission = new AccessPermission(dicPermissions);
                currentAccessPermission.IsReadOnly = true;
                CurrentAccessPermission = currentAccessPermission;

                encryptionKey = ComputeEncryptedKey(
                    passwordCharset.GetBytes(password),
                    ownerKey, userKey, oe, ue,
                    dicPermissions,
                    documentIDBytes,
                    dicRevision,
                    dicLength,
                    encryptMetadata, false);
            }
            else
            {
                throw new InvalidPasswordException("Cannot decrypt PDF, the password is incorrect");
            }

            if (dicRevision == 6 || dicRevision == 5)

            {
                ValidatePerms(encryption, dicPermissions, encryptMetadata);
            }

            if (encryption.Version == 4 || encryption.Version == 5)
            {
                // detect whether AES encryption is used. This assumes that the encryption algo is 
                // stored in the PDCryptFilterDictionary
                // However, crypt filters are used only when V is 4 or 5.
                var stdCryptFilterDictionary = encryption.StdCryptFilterDictionary;

                if (stdCryptFilterDictionary != null)
                {
                    PdfName cryptFilterMethod = stdCryptFilterDictionary.CryptFilterMethod;
                    IsAES = PdfName.AESV2.Equals(cryptFilterMethod) ||
                           PdfName.AESV3.Equals(cryptFilterMethod);
                }
            }
        }

        private byte[] GetDocumentIDBytes(PdfArray documentIDArray)
        {
            //some documents may not have document id, see
            //test\encryption\encrypted_doc_no_id.pdf
            byte[] documentIDBytes;
            if (documentIDArray != null && documentIDArray.Count >= 1)
            {
                PdfString id = (PdfString)documentIDArray.Resolve(0);
                documentIDBytes = id.GetBuffer();
            }
            else
            {
                documentIDBytes = new byte[0];
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
                byte[] perms = null;
                using (var cipher = new RijndaelManaged())
                {
                    cipher.Mode = CipherMode.ECB;
                    cipher.Key = encryptionKey;
                    cipher.Padding = PaddingMode.None;
                    using (var decriptor = cipher.CreateDecryptor())
                    {
                        perms = decriptor.DoFinal(encryption.Perms);
                    }
                }

                // "Verify that bytes 9-11 of the result are the characters ‘a’, ‘d’, ‘b’."
                if (perms[9] != 'a' || perms[10] != 'd' || perms[11] != 'b')
                {
                    Debug.WriteLine("warn: Verification of permissions failed (constant)");
                }

                // "Bytes 0-3 of the decrypted Perms entry, treated as a little-endian integer, 
                // are the user permissions. They should match the value in the P key."
                int permsP = perms[0] & 0xFF | (perms[1] & 0xFF) << 8 | (perms[2] & 0xFF) << 16 |
                        (perms[3] & 0xFF) << 24;

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
            if (version != 4 && version != 5)
            {
                // remove CF, StmF, and StrF entries that may be left from a previous encryption
                encryptionDictionary.RemoveV45filters();
            }
            encryptionDictionary.Revision = revision;
            encryptionDictionary.Length = keyLength;

            string ownerPassword = policy.OwnerPassword ?? string.Empty;
            string userPassword = policy.UserPassword ?? string.Empty;
            // If no owner password is set, use the user password instead.
            if (ownerPassword.Length == 0)
            {
                ownerPassword = userPassword;
            }

            int permissionInt = policy.Permissions.PermissionBytes;

            encryptionDictionary.Permissions = permissionInt;

            int length = keyLength / 8;

            if (revision == 6)
            {
                // PDFBOX-4155
                ownerPassword = SaslPrep.SaslPrepStored(ownerPassword);
                userPassword = SaslPrep.SaslPrepStored(userPassword);
                PrepareEncryptionDictRev6(ownerPassword, userPassword, encryptionDictionary, permissionInt);
            }
            else

            {
                PrepareEncryptionDictRev2345(ownerPassword, userPassword, encryptionDictionary, permissionInt,
                        document, revision, length);
            }

            document.File.Encryption = encryptionDictionary;
        }

        private void PrepareEncryptionDictRev6(string ownerPassword, string userPassword,
                PdfEncryption encryptionDictionary, int permissionInt)
        {
            try
            {
                SecureRandom rnd = new SecureRandom();
                using (var cipher = new RijndaelManaged())
                {
                    cipher.Mode = CipherMode.CBC;
                    cipher.Padding = PaddingMode.None;
                    // make a random 256-bit file encryption key
                    encryptionKey = new byte[32];
                    rnd.NextBytes(encryptionKey);

                    // Algorithm 8a: Compute U
                    byte[] userPasswordBytes = Truncate127(Charset.UTF8.GetBytes(userPassword));
                    byte[] userValidationSalt = new byte[8];
                    byte[] userKeySalt = new byte[8];
                    rnd.NextBytes(userValidationSalt);
                    rnd.NextBytes(userKeySalt);
                    byte[] hashU = ComputeHash2B(Concat(userPasswordBytes, userValidationSalt), userPasswordBytes, null);
                    byte[] u = Concat(hashU, userValidationSalt, userKeySalt);

                    // Algorithm 8b: Compute UE
                    byte[] hashUE = ComputeHash2B(Concat(userPasswordBytes, userKeySalt), userPasswordBytes, null);
                    byte[] ue = null;

                    using (var enciptor = cipher.CreateEncryptor(hashUE, new byte[16]))// "an initialization vector of zero"
                    { ue = enciptor.DoFinal(encryptionKey); }

                    // Algorithm 9a: Compute O
                    byte[] ownerPasswordBytes = Truncate127(Charset.UTF8.GetBytes(ownerPassword));
                    byte[] ownerValidationSalt = new byte[8];
                    byte[] ownerKeySalt = new byte[8];
                    rnd.NextBytes(ownerValidationSalt);
                    rnd.NextBytes(ownerKeySalt);
                    byte[] hashO = ComputeHash2B(Concat(ownerPasswordBytes, ownerValidationSalt, u), ownerPasswordBytes, u);
                    byte[] o = Concat(hashO, ownerValidationSalt, ownerKeySalt);

                    // Algorithm 9b: Compute OE
                    byte[] hashOE = ComputeHash2B(Concat(ownerPasswordBytes, ownerKeySalt, u), ownerPasswordBytes, u);
                    byte[] oe = null;
                    using (var enciptor = cipher.CreateEncryptor(hashOE, new byte[16]))// "an initialization vector of zero"
                    { oe = enciptor.DoFinal(encryptionKey); }

                    // Set keys and other required constants in encryption dictionary
                    encryptionDictionary.UserKey = u;
                    encryptionDictionary.UserEncryptionKey = ue;
                    encryptionDictionary.OwnerKey = o;
                    encryptionDictionary.OwnerEncryptionKey = oe;

                    PrepareEncryptionDictAES(encryptionDictionary, PdfName.AESV3);

                    // Algorithm 10: compute "Perms" value
                    byte[] perms = new byte[16];
                    perms[0] = (byte)permissionInt;
                    perms[1] = (byte)((uint)permissionInt >> 8);
                    perms[2] = (byte)((uint)permissionInt >> 16);
                    perms[3] = (byte)((uint)permissionInt >> 24);
                    perms[4] = (byte)0xFF;
                    perms[5] = (byte)0xFF;
                    perms[6] = (byte)0xFF;
                    perms[7] = (byte)0xFF;
                    perms[8] = (byte)'T';    // we always encrypt Metadata
                    perms[9] = (byte)'a';
                    perms[10] = (byte)'d';
                    perms[11] = (byte)'b';
                    for (int i = 12; i <= 15; i++)
                    {
                        perms[i] = (byte)rnd.NextInt();
                    }

                    byte[] permsEnc = null;
                    using (var enciptor = cipher.CreateEncryptor(encryptionKey, new byte[16])) // "an initialization vector of zero"
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

        private void PrepareEncryptionDictRev2345(string ownerPassword, string userPassword,
                PdfEncryption encryptionDictionary, int permissionInt, Document document,
                int revision, int length)
        {
            var idArray = document.File.ID;

            //check if the document has an id yet.  If it does not then generate one
            if (idArray == null || idArray.BaseDataObject.Count < 2)
            {
                DateTime Jan1st1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                using (var md = MD5.Create())
                {
                    BigInteger time = new BigInteger((DateTime.UtcNow - Jan1st1970).TotalMilliseconds.ToString());
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

            byte[] ownerBytes = ComputeOwnerPassword(
                    Charset.ISO88591.GetBytes(ownerPassword),
                    Charset.ISO88591.GetBytes(userPassword), revision, length);

            byte[] userBytes = ComputeUserPassword(
                    Charset.ISO88591.GetBytes(userPassword),
                    ownerBytes, permissionInt, id.GetBuffer(), revision, length, true);

            encryptionKey = ComputeEncryptedKey(Charset.ISO88591.GetBytes(userPassword), ownerBytes,
                        null, null, null, permissionInt, id.GetBuffer(), revision, length, true, false);

            encryptionDictionary.OwnerKey = ownerBytes;
            encryptionDictionary.UserKey = userBytes;

            if (revision == 4)
            {
                PrepareEncryptionDictAES(encryptionDictionary, PdfName.AESV2);
            }
        }

        private void PrepareEncryptionDictAES(PdfEncryption encryptionDictionary, PdfName aesVName)
        {
            var cryptFilterDictionary = new PdfCryptFilterDictionary(encryptionDictionary.File);
            cryptFilterDictionary.CryptFilterMethod = aesVName;
            cryptFilterDictionary.Length = keyLength;
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
        public bool IsOwnerPassword(byte[] ownerPassword, byte[] user, byte[] owner,
                                       int permissions, byte[] id, int encRevision, int keyLengthInBytes,
                                       bool encryptMetadata)
        {
            if (encRevision == 6 || encRevision == 5)
            {
                byte[] truncatedOwnerPassword = Truncate127(ownerPassword);

                byte[] oHash = new byte[32];
                byte[] oValidationSalt = new byte[8];
                Array.Copy(owner, 0, oHash, 0, 32);
                Array.Copy(owner, 32, oValidationSalt, 0, 8);

                byte[] hash;
                if (encRevision == 5)
                {
                    hash = ComputeSHA256(truncatedOwnerPassword, oValidationSalt, user);
                }
                else
                {
                    hash = ComputeHash2A(truncatedOwnerPassword, oValidationSalt, user);
                }

                return hash.AsSpan().SequenceEqual(oHash.AsSpan());
            }
            else

            {
                byte[] userPassword = GetUserPassword(ownerPassword, owner, encRevision, keyLengthInBytes);
                return IsUserPassword(userPassword, user, owner, permissions, id, encRevision, keyLengthInBytes,
                                       encryptMetadata);
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
		 * @ If there is an error accessing data while generating the user password.
		 */
        public byte[] GetUserPassword(byte[] ownerPassword, byte[] owner, int encRevision, int length)
        {
            using (MemoryStream result = new MemoryStream())
            {
                byte[] rc4Key = ComputeRC4key(ownerPassword, encRevision, length);

                if (encRevision == 2)
                {
                    EncryptDataRC4(rc4Key, owner, result);
                }
                else if (encRevision == 3 || encRevision == 4)
                {
                    byte[] iterationKey = new byte[rc4Key.Length];
                    byte[] otemp = new byte[owner.Length];
                    Array.Copy(owner, 0, otemp, 0, owner.Length);

                    for (int i = 19; i >= 0; i--)
                    {
                        Array.Copy(rc4Key, 0, iterationKey, 0, rc4Key.Length);
                        for (int j = 0; j < iterationKey.Length; j++)
                        {
                            iterationKey[j] = (byte)(iterationKey[j] ^ (byte)i);
                        }
                        result.Reset();
                        EncryptDataRC4(iterationKey, otemp, result);
                        otemp = result.ToArray();
                    }
                }
                return result.ToArray();
            }
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
        public byte[] ComputeEncryptedKey(byte[] password, byte[] o, byte[] u, byte[] oe, byte[] ue,
                                          int permissions, byte[] id, int encRevision, int keyLengthInBytes,
                                          bool encryptMetadata, bool isOwnerPassword)
        {
            if (encRevision == 6 || encRevision == 5)
            {
                return ComputeEncryptedKeyRev56(password, isOwnerPassword, o, u, oe, ue, encRevision);
            }
            else

            {
                return ComputeEncryptedKeyRev234(password, o, permissions, id, encryptMetadata, keyLengthInBytes, encRevision);
            }
        }

        private byte[] ComputeEncryptedKeyRev234(byte[] password, byte[] o, int permissions,
                byte[] id, bool encryptMetadata, int length, int encRevision)
        {
            //Algorithm 2, based on MD5

            //PDFReference 1.4 pg 78
            byte[] padded = TruncateOrPad(password);

            using (var md = MD5.Create())
            {
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
                if (encRevision == 4 && !encryptMetadata)
                {
                    md.Update(new byte[] { (byte)0xff, (byte)0xff, (byte)0xff, (byte)0xff });
                }
                byte[] digest = md.Digest();

                if (encRevision == 3 || encRevision == 4)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        using (var mdi = MD5.Create())
                            digest = mdi.Digest(digest, 0, length);
                    }
                }

                byte[] result = new byte[length];
                Array.Copy(digest, 0, result, 0, length);
                return result;
            }
        }

        private byte[] ComputeEncryptedKeyRev56(byte[] password, bool isOwnerPassword,
                byte[] o, byte[] u, byte[] oe, byte[] ue, int encRevision)
        {
            byte[] hash, fileKeyEnc;

            if (isOwnerPassword)
            {
                byte[] oKeySalt = new byte[8];
                Array.Copy(o, 40, oKeySalt, 0, 8);

                if (encRevision == 5)
                {
                    hash = ComputeSHA256(password, oKeySalt, u);
                }
                else
                {
                    hash = ComputeHash2A(password, oKeySalt, u);
                }

                fileKeyEnc = oe;
            }
            else
            {
                byte[] uKeySalt = new byte[8];
                Array.Copy(u, 40, uKeySalt, 0, 8);

                if (encRevision == 5)
                {
                    hash = ComputeSHA256(password, uKeySalt, null);
                }
                else
                {
                    hash = ComputeHash2A(password, uKeySalt, null);
                }

                fileKeyEnc = ue;
            }
            try
            {
                using (var cipher = new RijndaelManaged())
                {
                    //cipher.init(Cipher.DECRYPT_MODE, new SecretKeySpec(hash, "AES"), new IvParameterSpec());
                    cipher.Mode = CipherMode.CBC;
                    cipher.Padding = PaddingMode.None;
                    cipher.Key = hash;
                    cipher.IV = new byte[16];
                    using (var decriptor = cipher.CreateDecryptor(cipher.Key, cipher.IV))
                    {
                        return decriptor.DoFinal(fileKeyEnc);
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
        public byte[] ComputeUserPassword(byte[] password, byte[] owner, int permissions,
                                          byte[] id, int encRevision, int keyLengthInBytes,
                                          bool encryptMetadata)
        {
            using (MemoryStream result = new MemoryStream())
            {
                byte[] encKey = ComputeEncryptedKey(password, owner, null, null, null, permissions,
                        id, encRevision, keyLengthInBytes, encryptMetadata, true);

                if (encRevision == 2)
                {
                    EncryptDataRC4(encKey, ENCRYPT_PADDING, result);
                }
                else if (encRevision == 3 || encRevision == 4)
                {
                    using (var md = MD5.Create())
                    {
                        md.Update(ENCRYPT_PADDING);
                        md.Update(id);
                        result.Write(md.Digest());

                        byte[] iterationKey = new byte[encKey.Length];
                        for (int i = 0; i < 20; i++)
                        {
                            Array.Copy(encKey, 0, iterationKey, 0, iterationKey.Length);
                            for (int j = 0; j < iterationKey.Length; j++)
                            {
                                iterationKey[j] = (byte)(iterationKey[j] ^ i);
                            }
                            using (MemoryStream input = new MemoryStream(result.ToArray()))
                            {
                                result.Reset();
                                EncryptDataRC4(iterationKey, input, result);
                            }
                        }

                        byte[] finalResult = new byte[32];
                        Array.Copy(result.ToArray(), 0, finalResult, 0, 16);
                        Array.Copy(ENCRYPT_PADDING, 0, finalResult, 16, 16);
                        result.Reset();
                        result.Write(finalResult);
                    }
                }
                return result.ToArray();
            }
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
        public byte[] ComputeOwnerPassword(byte[] ownerPassword, byte[] userPassword,
                                           int encRevision, int length)
        {
            if (encRevision == 2 && length != 5)
            {
                throw new IOException("Expected length=5 actual=" + length);
            }

            byte[] rc4Key = ComputeRC4key(ownerPassword, encRevision, length);
            byte[] paddedUser = TruncateOrPad(userPassword);

            using (MemoryStream encrypted = new MemoryStream())
            {
                EncryptDataRC4(rc4Key, new MemoryStream(paddedUser), encrypted);

                if (encRevision == 3 || encRevision == 4)
                {
                    byte[] iterationKey = new byte[rc4Key.Length];
                    for (int i = 1; i < 20; i++)
                    {
                        Array.Copy(rc4Key, 0, iterationKey, 0, rc4Key.Length);
                        for (int j = 0; j < iterationKey.Length; j++)
                        {
                            iterationKey[j] = (byte)(iterationKey[j] ^ (byte)i);
                        }
                        using (MemoryStream input = new MemoryStream(encrypted.ToArray()))
                        {
                            encrypted.Reset();
                            EncryptDataRC4(iterationKey, input, encrypted);
                        }
                    }
                }

                return encrypted.ToArray();
            }
        }

        // steps (a) to (d) of "Algorithm 3: Computing the encryption dictionary’s O (owner password) value".
        private byte[] ComputeRC4key(byte[] ownerPassword, int encRevision, int length)
        {
            using (var md = MD5.Create())
            {
                byte[] digest = md.Digest(TruncateOrPad(ownerPassword));
                if (encRevision == 3 || encRevision == 4)
                {
                    for (int i = 0; i < 50; i++)
                    {
                        // this deviates from the spec - however, omitting the length
                        // parameter prevents the file to be opened in Adobe Reader
                        // with the owner password when the key length is 40 bit (= 5 bytes)
                        using (var mdi = MD5.Create())
                            digest = mdi.Digest(digest, 0, length);
                    }
                }
                byte[] rc4Key = new byte[length];
                Array.Copy(digest, 0, rc4Key, 0, length);
                return rc4Key;
            }
        }


        /**
		 * This will take the password and truncate or pad it as necessary.
		 *
		 * @param password The password to pad or truncate.
		 *
		 * @return The padded or truncated password.
		 */
        private byte[] TruncateOrPad(byte[] password)
        {
            byte[] padded = new byte[ENCRYPT_PADDING.Length];
            int bytesBeforePad = Math.Min(password.Length, padded.Length);
            Array.Copy(password, 0, padded, 0, bytesBeforePad);
            Array.Copy(ENCRYPT_PADDING, 0, padded, bytesBeforePad,
                              ENCRYPT_PADDING.Length - bytesBeforePad);
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
        public bool IsUserPassword(byte[] password, byte[] user, byte[] owner, int permissions,
                                      byte[] id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            switch (encRevision)
            {
                case 2:
                case 3:
                case 4:
                    return IsUserPassword234(password, user, owner, permissions, id, encRevision,
                                             keyLengthInBytes, encryptMetadata);
                case 5:
                case 6:
                    return IsUserPassword56(password, user, encRevision);
                default:
                    throw new IOException("Unknown Encryption Revision " + encRevision);
            }
        }

        private bool IsUserPassword234(byte[] password, byte[] user, byte[] owner, int permissions,
                byte[] id, int encRevision, int length, bool encryptMetadata)


        {
            byte[] passwordBytes = ComputeUserPassword(password, owner, permissions, id, encRevision,
                                                       length, encryptMetadata);
            if (encRevision == 2)
            {
                return user.AsSpan().SequenceEqual(passwordBytes.AsSpan());
            }
            else
            {
                // compare first 16 bytes only
                return user.AsSpan(0, 16).SequenceEqual(passwordBytes.AsSpan(0, 16));
            }
        }

        private bool IsUserPassword56(byte[] password, byte[] user, int encRevision)
        {
            byte[] truncatedPassword = Truncate127(password);
            byte[] uHash = new byte[32];
            byte[] uValidationSalt = new byte[8];
            Array.Copy(user, 0, uHash, 0, 32);
            Array.Copy(user, 32, uValidationSalt, 0, 8);

            byte[] hash;
            if (encRevision == 5)
            {
                hash = ComputeSHA256(truncatedPassword, uValidationSalt, null);
            }
            else
            {
                hash = ComputeHash2A(truncatedPassword, uValidationSalt, null);
            }

            return hash.AsSpan().SequenceEqual(uHash.AsSpan());
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
        public bool IsUserPassword(string password, byte[] user, byte[] owner, int permissions,
                                      byte[] id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            if (encRevision == 6 || encRevision == 5)

            {
                return IsUserPassword(Charset.UTF8.GetBytes(password), user, owner, permissions, id,
                        encRevision, keyLengthInBytes, encryptMetadata);
            }
            else

            {
                return IsUserPassword(Charset.ISO88591.GetBytes(password), user, owner, permissions, id,
                        encRevision, keyLengthInBytes, encryptMetadata);
            }
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
        public bool IsOwnerPassword(string password, byte[] user, byte[] owner, int permissions,
                                       byte[] id, int encRevision, int keyLengthInBytes, bool encryptMetadata)
        {
            return IsOwnerPassword(Charset.ISO88591.GetBytes(password), user, owner, permissions, id,
                                   encRevision, keyLengthInBytes, encryptMetadata);
        }

        // Algorithm 2.A from ISO 32000-1
        private byte[] ComputeHash2A(byte[] password, byte[] salt, byte[] u)
        {
            byte[] userKey;
            if (u == null)
            {
                userKey = new byte[0];
            }
            else if (u.Length < 48)
            {
                throw new IOException("Bad U length");
            }
            else if (u.Length > 48)
            {
                // must truncate
                userKey = new byte[48];
                Array.Copy(u, 0, userKey, 0, 48);
            }
            else
            {
                userKey = u;
            }

            byte[] truncatedPassword = Truncate127(password);
            byte[] input = Concat(truncatedPassword, salt, userKey);
            return ComputeHash2B(input, truncatedPassword, userKey);
        }

        // Algorithm 2.B from ISO 32000-2
        private static byte[] ComputeHash2B(byte[] input, byte[] password, byte[] userKey)
        {
            try
            {
                var md = (HashAlgorithm)SHA256.Create();
                byte[] k = md.Digest(input);

                byte[] e = null;
                for (int round = 0; round < 64 || ((int)e[e.Length - 1] & 0xFF) > round - 32; round++)
                {
                    byte[] k1;
                    if (userKey != null && userKey.Length >= 48)
                    {
                        k1 = new byte[64 * (password.Length + k.Length + 48)];
                    }
                    else
                    {
                        k1 = new byte[64 * (password.Length + k.Length)];
                    }

                    int pos = 0;
                    for (int i = 0; i < 64; i++)
                    {
                        Array.Copy(password, 0, k1, pos, password.Length);
                        pos += password.Length;
                        Array.Copy(k, 0, k1, pos, k.Length);
                        pos += k.Length;
                        if (userKey != null && userKey.Length >= 48)
                        {
                            Array.Copy(userKey, 0, k1, pos, 48);
                            pos += 48;
                        }
                    }

                    byte[] kFirst = new byte[16];
                    byte[] kSecond = new byte[16];
                    Array.Copy(k, 0, kFirst, 0, 16);
                    Array.Copy(k, 16, kSecond, 0, 16);

                    using (var cipher = new RijndaelManaged())
                    {
                        cipher.Mode = CipherMode.CBC;
                        cipher.Key = kFirst;
                        cipher.IV = kSecond;
                        cipher.Padding = PaddingMode.None;
                        //cipher.init(Cipher.ENCRYPT_MODE, keySpec, ivSpec);
                        using (var ecriptor = cipher.CreateEncryptor())
                            e = ecriptor.DoFinal(k1);

                        byte[] eFirst = new byte[16];
                        Array.Copy(e, 0, eFirst, 0, 16);
                        BigInteger bi = new BigInteger(1, eFirst);
                        BigInteger remainder = bi.Mod(new BigInteger("3"));
                        string nextHash = HASHES_2B[remainder.IntValue];
                        //md = MessageDigest.getInstance(nextHash);
                        md.Dispose();
                        switch (nextHash)
                        {
                            case "SHA256": md = SHA256.Create(); break;
                            case "SHA384": md = SHA256.Create(); break;
                            case "SHA512": md = SHA256.Create(); break;
                        }
                        k = md.Digest(e);
                    }
                }

                if (k.Length > 32)
                {
                    byte[] kTrunc = new byte[32];
                    Array.Copy(k, 0, kTrunc, 0, 32);
                    return kTrunc;
                }
                else
                {
                    return k;
                }
            }
            catch (Exception e)
            {
                LogIfStrongEncryptionMissing();
                throw new IOException("ComputeHash2B", e);
            }
        }

        private static byte[] ComputeSHA256(byte[] input, byte[] password, byte[] userKey)
        {
            try
            {
                using (var md = SHA256.Create())
                {
                    md.Update(input);
                    md.Update(password);
                    return userKey == null ? md.Digest() : md.Digest(userKey);
                }
            }
            catch (Exception e)
            {
                throw new IOException("ComputeSHA256", e);
            }
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] o = new byte[a.Length + b.Length];
            Array.Copy(a, 0, o, 0, a.Length);
            Array.Copy(b, 0, o, a.Length, b.Length);
            return o;
        }

        private static byte[] Concat(byte[] a, byte[] b, byte[] c)
        {
            byte[] o = new byte[a.Length + b.Length + c.Length];
            Array.Copy(a, 0, o, 0, a.Length);
            Array.Copy(b, 0, o, a.Length, b.Length);
            Array.Copy(c, 0, o, a.Length + b.Length, c.Length);
            return o;
        }

        private static byte[] Truncate127(byte[] inp)
        {
            if (inp.Length <= 127)
            {
                return inp;
            }
            byte[] trunc = new byte[127];
            Array.Copy(inp, 0, trunc, 0, 127);
            return trunc;
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

        /**
		 * {@inheritDoc}
		 */

        public override bool HasProtectionPolicy()
        {
            return policy != null;
        }
    }
}