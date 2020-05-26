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

/* eslint no-var: error */
namespace PdfClown.Bytes.Filters
{
    internal class FaxParams
    {
        internal int K;
        internal int Columns;
        internal int Rows;
        internal bool BlackIs1;
        internal bool EndOfBlock;
        internal bool EndOfLine;
        internal bool EncodedByteAlign;

        public FaxParams(int K, int Columns, int Rows, bool BlackIs1, bool EndOfBlock)
        {
            this.K = K;
            this.Columns = Columns;
            this.Rows = Rows;
            this.BlackIs1 = BlackIs1;
            this.EndOfBlock = EndOfBlock;
        }
    }
}
