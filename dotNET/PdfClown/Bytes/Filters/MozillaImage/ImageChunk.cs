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
    internal class ImageChunk
    {
        internal byte[] data;
        internal int start;
        internal int end;

        public ImageChunk(byte[] data, int start, int end)
        {
            this.data = data;
            this.start = start;
            this.end = end;
        }
    }
}
