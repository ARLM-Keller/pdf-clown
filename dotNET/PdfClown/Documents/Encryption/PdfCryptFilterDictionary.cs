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

using PdfClown.Files;
using PdfClown.Objects;
using System;

namespace PdfClown.Documents.Encryption
{

    /**
     * This class is a specialized view of the crypt filter dictionary of a PDF document.
     * It contains a low level dictionary (PdfDictionary) and provides the methods to
     * manage its fields.
     *
     */
    public class PdfCryptFilterDictionary : PdfObjectWrapper<PdfDictionary>
    {
        /**
		 * creates a new empty crypt filter dictionary.
		 */
        public PdfCryptFilterDictionary(File context) : base(context, new PdfDictionary())
        {
        }

        /**
		 * creates a new crypt filter dictionary from the low level dictionary provided.
		 * @param d the low level dictionary that will be managed by the newly created object
		 */
        public PdfCryptFilterDictionary(PdfDirectObject baseObject)// Catalog.
            : base(baseObject)
        { }

        /**
         * This will return the Length entry of the crypt filter dictionary.<br><br>
         * The length in <b>bits</b> for the crypt filter algorithm. This will return a multiple of 8.
         *
         * @return The length in bits for the encryption algorithm
         */
        /**
         * This will set the number of bits to use for the crypt filter algorithm.
         *
         * @param length The new key length.
         */
        public int Length
        {
            get => BaseDataObject.GetInt(PdfName.Length, 0);
            set => BaseDataObject.SetInt(PdfName.Length, value);
        }


        /**
		 * This will return the crypt filter method. 
		 * Allowed values are: NONE, V2, AESV2, AESV3
		 *
		 * @return the name of the crypt filter method.
		 */
        /**
		* This will set the crypt filter method. 
		* Allowed values are: NONE, V2, AESV2, AESV3
		*
		* @param cfm name of the crypt filter method.
		*
		*/
        public PdfName CryptFilterMethod
        {
            get => BaseDataObject.GetName(PdfName.CFM);
            set => BaseDataObject[PdfName.CFM] = value;
        }

        /**
        * Will get the EncryptMetaData dictionary info.
        *
        * @return true if EncryptMetaData is explicitly set (the default is true)
        */
        /**
         * Set the EncryptMetaData dictionary info.
         *
         * @param encryptMetaData true if EncryptMetaData shall be set.
         */
        public bool IsEncryptMetaData
        {
            get => BaseDataObject.GetBool(PdfName.EncryptMetadata, true);
            set => BaseDataObject.SetBool(PdfName.EncryptMetadata, value);
        }

    }
}
