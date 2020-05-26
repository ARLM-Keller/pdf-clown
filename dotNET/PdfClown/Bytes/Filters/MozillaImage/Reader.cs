/* Copyright 2012 Mozilla Foundation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;

/* eslint no-var: error */
namespace PdfClown.Bytes.Filters
{
    public class Reader
    {
        internal byte[] data;
        internal int start;
        internal int end;
        internal int position;
        internal int shift;
        internal byte currentByte;

        public Reader(byte[] data, int start, int end)
        {
            this.data = data;
            this.start = start;
            this.end = end;
            this.position = start;
            this.shift = -1;
            this.currentByte = 0;
        }

        public int ReadBit()
        {
            if (this.shift < 0)
            {
                if (this.position >= this.end)
                {
                    throw new Exception("end of data while reading bit");
                }
                this.currentByte = this.data[this.position++];
                this.shift = 7;
            }
            var bit = (this.currentByte >> this.shift) & 1;
            this.shift--;
            return bit;
        }

        public int ReadBits(int numBits)
        {
            var result = 0;//TODO uint
            for (var i = numBits - 1; i >= 0; i--)
            {
                result |= this.ReadBit() << i;
            }
            return result;
        }

        public void ByteAlign()
        {
            this.shift = -1;
        }

        public int Next()
        {
            if (this.position >= this.end)
            {
                return -1;
            }
            return this.data[this.position++];
        }
    }
}
