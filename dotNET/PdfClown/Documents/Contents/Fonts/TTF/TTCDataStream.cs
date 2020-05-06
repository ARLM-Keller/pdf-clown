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

namespace PdfClown.Documents.Contents.Fonts.TTF
{

    using System.IO;


    /**
     * A wrapper for a TTF stream inside a TTC file, does not close the underlying shared stream.
     *
     * @author John Hewson
     */
    public class TTCDataStream : TTFDataStream
    {
        private readonly TTFDataStream stream;

        public TTCDataStream(TTFDataStream stream)
        {
            this.stream = stream;
        }

        public override int Read()
        {
            return stream.Read();
        }

        public override long ReadLong()
        {
            return stream.ReadLong();
        }

        public override ulong ReadUnsignedLong()
        {
            return stream.ReadUnsignedLong();
        }

        public override ushort ReadUnsignedShort()
        {
            return stream.ReadUnsignedShort();
        }

        public override short ReadSignedShort()
        {
            return stream.ReadSignedShort();
        }

        public override void Dispose()
        {
            // don't close the underlying stream, as it is shared by all fonts from the same TTC
            // TrueTypeCollection.Dispose() must be called instead
        }

        public override void Seek(long pos)
        {
            stream.Seek(pos);
        }

        public override int Read(byte[] b, int off, int len)
        {
            return stream.Read(b, off, len);
        }

        public override long CurrentPosition
        {
            get => stream.CurrentPosition;
        }

        public override Bytes.Buffer OriginalData
        {
            get => stream.OriginalData;
        }

        public override long OriginalDataSize
        {
            get => stream.OriginalDataSize;
        }
    }
}
