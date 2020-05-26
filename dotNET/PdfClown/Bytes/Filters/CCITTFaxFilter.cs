﻿/*
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
using PdfClown.Bytes.Filters.CCITT;
using PdfClown.Objects;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfClown.Bytes.Filters
{
    /**
     * Decodes image data that has been encoded using either Group 3 or Group 4
     * CCITT facsimile (fax) encoding, and encodes image data to Group 4.
     *
     * @author Ben Litchfield
     * @author Marcel Kammer
     * @author Paul King
     */
    public class CCITTFaxFilter : Filter
    {
        public override byte[] Decode(Bytes.Buffer data, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            // get decode parameters
            PdfDictionary decodeParms = parameters as PdfDictionary;
            var ccittFaxParams = new CCITTFaxParams(
                K: decodeParms.GetInt(PdfName.K),
                endOfLine: decodeParms.GetBool(PdfName.EndOfLine),
                encodedByteAlign: decodeParms.GetBool(PdfName.EncodedByteAlign),
                columns: decodeParms.GetInt(PdfName.Columns),
                rows: decodeParms.GetInt(PdfName.Rows),
                endOfBlock: decodeParms.GetBool(PdfName.EndOfBlock),
                blackIs1: decodeParms.GetBool(PdfName.BlackIs1)
                );
            var decoder = new CCITTFaxDecoder(data, ccittFaxParams);
            using (var output = new Bytes.Buffer())
            {
                var currentByte = 0;
                while ((currentByte = decoder.ReadNextChar()) > -1)
                {
                    output.Append(FiltersExtension.ToByte(currentByte));
                }
                return output.GetBuffer();
            }
        }
        public override byte[] Encode(Buffer data, PdfDirectObject parameters, IDictionary<PdfName, PdfDirectObject> header)
        {
            throw new NotImplementedException();
        }
    }
}