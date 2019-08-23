/*
  Copyright 2006-2015 Stefano Chizzolini. http://www.pdfclown.org

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

using org.pdfclown;
using org.pdfclown.documents;
using org.pdfclown.documents.contents.colorSpaces;
using org.pdfclown.objects;

using System;
using SkiaSharp;
using System.IO;
using System.Text;
using BitMiracle.LibTiff.Classic;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace org.pdfclown.documents.contents.xObjects
{
    /**
      <summary>Image external object [PDF:1.6:4.8.4].</summary>
    */
    [PDF(VersionEnum.PDF10)]
    public sealed class ImageXObject : XObject
    {
        #region static
        #region interface
        #region public
        public static new ImageXObject Wrap(PdfDirectObject baseObject)
        {
            return baseObject != null ? new ImageXObject(baseObject) : null;
        }
        #endregion
        #endregion
        #endregion

        #region dynamic
        #region constructors
        public ImageXObject(Document context, PdfStream baseDataObject)
            : base(context, baseDataObject)
        {
            /*
              NOTE: It's caller responsability to adequately populate the stream
              header and body in order to instantiate a valid object; header entries like
              'Width', 'Height', 'ColorSpace', 'BitsPerComponent' MUST be defined
              appropriately.
            */
            baseDataObject.Header[PdfName.Subtype] = PdfName.Image;
        }

        private ImageXObject(PdfDirectObject baseObject) : base(baseObject)
        { }
        #endregion

        #region interface
        #region public
        /**
          <summary>Gets the number of bits per color component.</summary>
        */
        public int BitsPerComponent
        {
            get { return ((PdfInteger)BaseDataObject.Header[PdfName.BitsPerComponent]).RawValue; }
        }

        /**
          <summary>Gets the color space in which samples are specified.</summary>
        */
        public ColorSpace ColorSpace
        {
            get { return ColorSpace.Wrap(BaseDataObject.Header[PdfName.ColorSpace]); }
        }

        public override SKMatrix Matrix
        {
            get
            {
                SKSize size = Size;
                /*
                  NOTE: Image-space-to-user-space matrix is [1/w 0 0 1/h 0 0],
                  where w and h are the width and height of the image in samples [PDF:1.6:4.8.3].
                */
                return new SKMatrix
                {
                    Values = new float[] { 1f / size.Width, 0, 0, 0, -1f / size.Height, 0, 0, 0, 1 }
                };
            }
            set
            {/* NOOP. */}
        }

        /**
          <summary>Gets the size of the image (in samples).</summary>
        */
        public override SKSize Size
        {
            get
            {
                PdfDictionary header = BaseDataObject.Header;

                return new SKSize(
                  ((PdfInteger)header[PdfName.Width]).RawValue,
                  ((PdfInteger)header[PdfName.Height]).RawValue
                  );
            }
            set { throw new NotSupportedException(); }
        }


        public SKBitmap LoadImage()
        {
            if (Document.Cache.TryGetValue((PdfReference)BaseObject, out var existingBitmap))
            {
                return (SKBitmap)existingBitmap;
            }
            var stream = BaseDataObject as PdfStream;
            var buffer = stream.GetBody(false).ToByteArray();
            var image = SKBitmap.Decode(buffer);
            if (image == null)
            {
                buffer = stream.GetBody(true).ToByteArray();
            }
            if (image == null)
            {
                if (IsTiff(buffer))
                {
                    image = LoadTiff(buffer);
                }
                else
                {
                    image = LoadImage(buffer);
                }
            }
            Document.Cache[(PdfReference)BaseObject] = image;
            return image;
        }

        private SKBitmap LoadImage(byte[] buffer)
        {
            var size = Size;
            SKImageInfo info = new SKImageInfo((int)size.Width, (int)size.Height)
            {
                //AlphaType = SKAlphaType.Opaque,
                //ColorType = SKColorType.Bgra8888
            };
            var colorSpace = ColorSpace;
            var indexed = ColorSpace is IndexedColorSpace;
            //if (ColorSpace is IndexedColorSpace indexedSpace)
            //{
            //    colorSpace = indexedSpace.BaseSpace;
            //}
            //if (colorSpace is DeviceRGBColorSpace)
            //{
            //    info.ColorSpace = SKColorSpace.CreateRgb(SKColorSpaceRenderTargetGamma.Linear, SKColorSpaceGamut.AdobeRgb);
            //}
            //else if (colorSpace is ICCBasedColorSpace iccColorSpace)
            //{
            //    info.ColorSpace = SKColorSpace.CreateIcc(iccColorSpace.Profile.GetBody(true).ToByteArray());
            //}
            colorSpace = ColorSpace;
            var componentsCount = colorSpace.ComponentCount;
            var rowBytes = buffer.Length / info.Height;
            var bitPerComponent = BitsPerComponent;
            var maximum = Math.Pow(2, bitPerComponent) - 1;
            var min = 0D;
            var max = indexed ? maximum : 1D;
            // create the buffer that will hold the pixels
            var raster = new int[info.Width * info.Height];

            for (int y = 0; y < info.Height; y++)
            {
                for (int x = 0; x < info.Width; x++)
                {
                    var index = (y * info.Width + x);
                    var componentIndex = index * componentsCount;
                    var components = new List<PdfDirectObject>();
                    for (int i = 0; i < componentsCount; i++)
                    {
                        var value = componentIndex < buffer.Length ? buffer[componentIndex] : 0;
                        var interpolate = min + (value * ((max - min) / maximum));//indexed ? value : 
                        components.Add(indexed
                            ? (PdfDirectObject)new PdfInteger((int)interpolate)
                            : (PdfDirectObject)new PdfReal(interpolate));
                        componentIndex++;
                    }

                    var color = colorSpace.GetColor(components, null);
                    var skColor = colorSpace.GetColor(color);
                    raster[y * info.Width + x] = (int)(uint)skColor;
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);

            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);

            return bitmap;
        }


        //https://stackoverflow.com/a/50370515/4682355
        public SKBitmap LoadTiff(byte[] tiffStream)
        {
            // open a TIFF stored in the stream
            using (var memeoryStream = new MemoryStream(tiffStream))
            using (var tifImg = Tiff.ClientOpen("in-memory", "r", memeoryStream, new TiffStream()))
            {
                // read the dimensions
                var width = tifImg.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
                var height = tifImg.GetField(TiffTag.IMAGELENGTH)[0].ToInt();

                // create the bitmap

                var info = new SKImageInfo(width, height)
                {
                    ColorType = SKColorType.Rgba8888
                };

                // create the buffer that will hold the pixels
                var raster = new int[width * height];
                // read the image into the memory buffer
                if (!tifImg.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
                {
                    // not a valid TIF image.
                    return null;
                }

                // get a pointer to the buffer, and give it to the bitmap
                var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);

                var bitmap = new SKBitmap();
                bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);

                return bitmap;
            }
        }

        public static bool IsImage(byte[] buf)
        {
            if (buf != null)
            {
                if (IsBitmap(buf)
                    || IsGif(buf)
                    || IsTiff(buf)
                    || IsPng(buf)
                    || IsJPEG(buf))
                    return true;
            }
            return false;
        }

        private static bool IsBitmap(byte[] buf)
        {
            return Encoding.ASCII.GetString(buf, 0, 2) == "BM";
        }

        private static bool IsPng(byte[] buf)
        {
            return (buf[0] == 137 && buf[1] == 80 && buf[2] == 78 && buf[3] == (byte)71); //png
        }

        private static bool IsJPEG(byte[] buf)
        {
            return (buf[0] == 255 && buf[1] == 216 && buf[2] == 255 && buf[3] == 224) //jpeg
                    || (buf[0] == 255 && buf[1] == 216 && buf[2] == 255 && buf[3] == 225); //jpeg canon
        }

        private static bool IsGif(byte[] buf)
        {
            return Encoding.ASCII.GetString(buf, 0, 3) == "GIF";
        }

        private static bool IsTiff(byte[] buf)
        {
            return (buf[0] == 73 && buf[1] == 73 && buf[2] == 42) // TIFF
                || (buf[0] == 77 && buf[1] == 77 && buf[2] == 42); // TIFF2;
        }
        #endregion
        #endregion
        #endregion
    }
}