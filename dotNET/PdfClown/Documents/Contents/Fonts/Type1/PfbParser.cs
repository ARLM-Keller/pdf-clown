/*
 * https://github.com/apache/pdfbox
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


using System;
using System.IO;
using Org.BouncyCastle.Asn1.Pkcs;
using PdfClown.Bytes;

namespace PdfClown.Documents.Contents.Fonts.Type1
{
    /**
	 * Parser for a pfb-file.
	 *
	 * @author Ben Litchfield
	 * @author Michael Niedermair
	 */
    public class PfbParser
    {
        /// the pdf header length.
        /// (start-marker (1 byte), ascii-/binary-marker(1 byte), size(4 byte))
        /// 3*6 == 18
        private static readonly int PFB_HEADER_LENGTH = 18;

        /// the start marker.
        private static readonly int START_MARKER = 0x80;

        /// the ascii marker.
        private static readonly int ASCII_MARKER = 0x01;

        /// the binary marker.
        private static readonly int BINARY_MARKER = 0x02;

        /// The record types in the pfb-file.
        private static readonly int[] PFB_RECORDS = { ASCII_MARKER, BINARY_MARKER, ASCII_MARKER };

        /// buffersize.
        //private static readonly int BUFFER_SIZE = 0xffff;

        /// the parsed pfb-data.
        private Memory<byte> pfbdata;

        /// the lengths of the records.
        private int[] lengths;

        // sample (pfb-file)
        // 00000000 80 01 8b 15  00 00 25 21  50 53 2d 41  64 6f 62 65  
        //          ......%!PS-Adobe

        /// Create a new object.
        /// @param filename  the file name
        /// @throws IOException if an IO-error occurs.
        public PfbParser(string filename)
        {
            using var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            ParsePfb(new ByteStream(fileStream));
        }

        /**
		 * Create a new object.
		 * @param in   The input.
		 * @throws IOException if an IO-error occurs.
		 */
        public PfbParser(IInputStream input)
        {
            ParsePfb(input);
        }

        /**
		 * Create a new object.
		 * @param bytes   The input.
		 * @throws IOException if an IO-error occurs.
		 */
        public PfbParser(Memory<byte> bytes)
        {
            ParsePfb(bytes);
        }

        /**
		 * Parse the pfb-array.
		 * @param pfb   The pfb-Array
		 * @throws IOException in an IO-error occurs.
		 */
        private void ParsePfb(Memory<byte> pfb) => ParsePfb(new ByteStream(pfb));

        private void ParsePfb(IInputStream input)
        {
            input.Seek(0);
            pfbdata = new byte[input.Length - PFB_HEADER_LENGTH];
            lengths = new int[PFB_RECORDS.Length];
            int pointer = 0;
            for (int records = 0; records < PFB_RECORDS.Length; records++)
            {
                if (input.ReadByte() != START_MARKER)
                {
                    throw new IOException("Start marker missing");
                }

                if (input.ReadByte() != PFB_RECORDS[records])
                {
                    throw new IOException("Incorrect record type");
                }

                int size = input.ReadByte();
                size += input.ReadByte() << 8;
                size += input.ReadByte() << 16;
                size += input.ReadByte() << 24;
                lengths[records] = size;
                if (pointer >= pfbdata.Length)
                {
                    throw new EndOfStreamException("attempted to read past EOF");
                }
                int got = input.Read(pfbdata.Span.Slice(pointer, size));
                if (got < 0)
                {
                    throw new EndOfStreamException();
                }
                pointer += got;
            }
        }

        /**
		 * Read the pdf input.
		 * @param in    The input.
		 * @return Returns the pdf-array.
		 * @throws IOException if an IO-error occurs.
		 */
        private Memory<byte> ReadPfbInput(ByteStream input) => input.AsMemory();

        /**
		 * Returns the lengths.
		 * @return Returns the lengths.
		 */
        public int[] Lengths => lengths;

        /**
		 * Returns the pfbdata.
		 * @return Returns the pfbdata.
		 */
        public Memory<byte> Pfbdata => pfbdata;

        /**
		 * Returns the size of the pfb-data.
		 * @return Returns the size of the pfb-data.
		 */
        public int Size => pfbdata.Length;

        /**
		 * Returns the pfb data as stream.
		 * @return Returns the pfb data as stream.
		 */
        public Bytes.ByteStream GetInputStream()
        {
            return new Bytes.ByteStream(pfbdata);
        }


        /**
		 * Returns the first segment
		 * @return first segment bytes
		 */
        public Memory<byte> GetSegment1()
        {
            return pfbdata.Slice(0, lengths[0]);
        }

        /**
		 * Returns the second segment
		 * @return second segment bytes
		 */
        public Memory<byte> GetSegment2()
        {
            return pfbdata.Slice(lengths[0], lengths[1]);
        }
    }
}