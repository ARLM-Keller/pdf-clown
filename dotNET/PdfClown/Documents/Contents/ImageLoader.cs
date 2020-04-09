﻿
using BitMiracle.LibTiff.Classic;
using FreeImageAPI;
using PdfClown.Bytes;
using PdfClown.Bytes.Filters;
using PdfClown.Documents.Contents.ColorSpaces;
using PdfClown.Documents.Contents.Scanner;
using PdfClown.Objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfClown.Documents.Contents
{
    public class ImageLoader
    {
        public static SKBitmap Load(IImageObject imageObject, GraphicsState state)
        {
            var buffer = imageObject.Data;
            var data = buffer.GetBuffer();
            var image = (SKBitmap)null;
            var filter = imageObject.Filter;
            if (filter is PdfArray filterArray)
            {
                var parameterArray = imageObject.Parameters as PdfArray;
                for (int i = 0; i < filterArray.Count; i++)
                {
                    var filterItem = (PdfName)filterArray[i];
                    var parameterItem = parameterArray?[i];
                    image = LoadImage(imageObject, data, filterItem, parameterItem);
                    if (image == null)
                    {
                        buffer = Bytes.Buffer.Extract(buffer, filterItem, parameterItem ?? imageObject.Header);
                        data = buffer.GetBuffer();
                    }
                }
            }
            else if (filter != null)
            {
                var filterItem = (PdfName)filter;
                image = LoadImage(imageObject, data, filterItem, imageObject.Parameters ?? imageObject.Header);
                if (image == null)
                {
                    buffer = Bytes.Buffer.Extract(buffer, filter, imageObject.Parameters ?? imageObject.Header);
                    data = buffer.GetBuffer();
                    image = SKBitmap.Decode(data);
                }
            }

            if (image == null)
            {
                var imageLoader = new ImageLoader(imageObject, data, state);
                image = imageLoader.Load();
            }
            return image;
        }

        public static SKBitmap LoadImage(IImageObject imageObject, byte[] data, PdfName filterItem, PdfDirectObject parameterItem)
        {
            SKBitmap image = null;
            if (filterItem.Equals(PdfName.DCTDecode)
                || filterItem.Equals(PdfName.DCT))
            {
                image = SKBitmap.Decode(data);
            }
            else if (filterItem.Equals(PdfName.CCITTFaxDecode)
                || filterItem.Equals(PdfName.CCF))
            {
                image = LoadTiff(data, parameterItem ?? imageObject.Header);
            }
            else if (filterItem.Equals(PdfName.JPXDecode))
            {
                image = LoadJPEG2000(data, parameterItem ?? imageObject.Header);
            }
            else if (filterItem.Equals(PdfName.JBIG2Decode))
            {
                image = LoadJBIG(data, parameterItem ?? imageObject.Header);
            }

            return image;
        }

        private static SKBitmap LoadJBIG(byte[] data, PdfDirectObject parameters = null)
        {

            var imageParams = ((PdfStream)parameters.Container.DataObject).Header;
            var width = imageParams.Resolve(PdfName.Width) as PdfInteger;
            var height = imageParams.Resolve(PdfName.Height) as PdfInteger;
            var bpp = imageParams.Resolve(PdfName.BitsPerComponent) as PdfInteger;
            var flag = imageParams.Resolve(PdfName.ImageMask) as PdfBoolean;

            using (var output = new MemoryStream())
            using (var input = new MemoryStream())
            {
                //
                input.Write(new byte[] { 0X97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x00, 0x00, 0x00, 0x79 }, 0, 13);
                if (parameters is PdfDictionary dict)
                {
                    var jbigGlobal = dict.Resolve(PdfName.JBIG2Globals) as PdfStream;
                    if (jbigGlobal != null)
                    {
                        var body = jbigGlobal.GetBody(false);
                        var bodyBuffer = body.GetBuffer();
                        input.Write(bodyBuffer, 0, bodyBuffer.Length);
                    }
                }
                input.Write(data, 0, data.Length);
                input.Write(new byte[] { 0X00, 0x00, 0x00, 0x03, 0x31, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 }, 0, 11);
                input.Write(new byte[] { 0X00, 0x00, 0x00, 0x04, 0x33, 0x01, 0x00, 0x00, 0x00, 0x00 }, 0, 10);
                input.Flush();
                input.Position = 0;
                FREE_IMAGE_FORMAT format = FreeImage.GetFileTypeFromStream(input);
                var bmp = FreeImage.LoadFromStream(input);

                if (bmp.IsNull)
                {
                    return null;
                }
                FreeImage.SaveToStream(bmp, output, FREE_IMAGE_FORMAT.FIF_JPEG, FREE_IMAGE_SAVE_FLAGS.JPEG_OPTIMIZE);
                FreeImage.Unload(bmp);
                output.Flush();
                output.Position = 0;
                return SKBitmap.Decode(output);
            }
        }

        public static SKBitmap LoadJPEG2000(byte[] jpegStream, PdfDirectObject parameters = null)
        {
            var imageParams = ((PdfStream)parameters.Container.DataObject).Header;
            var width = imageParams.Resolve(PdfName.Width) as PdfInteger;
            var height = imageParams.Resolve(PdfName.Height) as PdfInteger;
            var bpp = imageParams.Resolve(PdfName.BitsPerComponent) as PdfInteger;
            var flag = imageParams.Resolve(PdfName.ImageMask) as PdfBoolean;

            using (var output = new MemoryStream())
            using (var input = new MemoryStream(jpegStream))
            {

                var bmp = FreeImage.LoadFromStream(input);
                if (bmp.IsNull)
                {
                    return null;
                }
                FreeImage.SaveToStream(bmp, output, FREE_IMAGE_FORMAT.FIF_JPEG, FREE_IMAGE_SAVE_FLAGS.JPEG_OPTIMIZE);
                FreeImage.Unload(bmp);
                output.Flush();
                output.Position = 0;
                return SKBitmap.Decode(output);
            }
        }

        public static SKBitmap LoadTiff(byte[] data, PdfDirectObject parameters)
        {
            var length = data.Length;
            var imageParams = ((PdfStream)parameters.Container.DataObject).Header;
            const short TIFF_BIGENDIAN = 0x4d4d;
            const short TIFF_LITTLEENDIAN = 0x4949;
            const int ifd_length = 10;
            const int header_length = 10 + (ifd_length * 12 + 4);
            var width = imageParams.Resolve(PdfName.Width) as PdfInteger;
            var height = imageParams.Resolve(PdfName.Height) as PdfInteger;
            var bpp = imageParams.Resolve(PdfName.BitsPerComponent) as PdfInteger;
            var flag = imageParams.Resolve(PdfName.ImageMask) as PdfBoolean;
            using (MemoryStream output = new MemoryStream())
            {
                output.Write(BitConverter.GetBytes(BitConverter.IsLittleEndian ? TIFF_LITTLEENDIAN : TIFF_BIGENDIAN), 0, 2); // tiff_magic (big/little endianness)
                output.Write(BitConverter.GetBytes((uint)42), 0, 2);         // tiff_version
                output.Write(BitConverter.GetBytes((uint)8), 0, 4);          // first_ifd (Image file directory) / offset
                output.Write(BitConverter.GetBytes((uint)ifd_length), 0, 2); // ifd_length, number of tags (ifd entries)

                // Dictionary should be in order based on the TiffTag value
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.SUBFILETYPE, TiffType.LONG, 1, 0);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.IMAGEWIDTH, TiffType.LONG, 1, (uint)width.RawValue);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.IMAGELENGTH, TiffType.LONG, 1, (uint)height.RawValue);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.BITSPERSAMPLE, TiffType.SHORT, 1, (uint)bpp.RawValue);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.COMPRESSION, TiffType.SHORT, 1, (uint)Compression.CCITTFAX4); // CCITT Group 4 fax encoding.
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.PHOTOMETRIC, TiffType.SHORT, 1, flag?.BooleanValue ?? false
                    ? (uint)(int)Photometric.MINISWHITE : (uint)(int)Photometric.MINISBLACK); // WhiteIsZero
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.STRIPOFFSETS, TiffType.LONG, 1, header_length);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.SAMPLESPERPIXEL, TiffType.SHORT, 1, 1);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.ROWSPERSTRIP, TiffType.LONG, 1, (uint)height.RawValue);
                CCITTFaxFilter.WriteTiffTag(output, TiffTag.STRIPBYTECOUNTS, TiffType.LONG, 1, (uint)length);

                // Next IFD Offset
                output.Write(BitConverter.GetBytes((uint)0), 0, 4);
                output.Write(data, 0, length);
                output.Flush();
                output.Position = 0;

                return LoadTiff(output);
            }
        }

        //https://stackoverflow.com/a/50370515/4682355
        public static SKBitmap LoadTiff(MemoryStream memeoryStream)
        {
            // open a TIFF stored in the stream
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

        private GraphicsState state;
        private IImageObject image;
        private ColorSpace colorSpace;
        private ICCBasedColorSpace iccColorSpace;
        private int bitPerComponent;
        private PdfArray matte;
        private SKSize size;
        private bool indexed;
        private int componentsCount;
        private double maximum;
        private double min;
        private double max;
        private double interpolateConst;
        private byte[] buffer;
        private bool imageMask;
        private PdfArray decode;
        private IImageObject sMask;
        private ImageLoader sMaskLoader;

        public ImageLoader(IImageObject image, GraphicsState state)
        {
            var buffer = image.Data;
            var data = buffer.ToByteArray();
            var filter = image.Filter;
            if (filter != null)
            {
                buffer = Bytes.Buffer.Extract(buffer, filter, image.Parameters ?? image.Header);
                data = buffer.ToByteArray();
            }
            Init(image, data, state);
        }

        public ImageLoader(IImageObject image, byte[] buffer, GraphicsState state)
        {
            Init(image, buffer, state);
        }



        private void Init(IImageObject image, byte[] buffer, GraphicsState state)
        {
            this.state = state;
            this.image = image;
            this.buffer = buffer;
            imageMask = image.ImageMask;
            decode = image.Decode;
            matte = image.Matte;
            size = image.Size;
            bitPerComponent = image.BitsPerComponent;

            maximum = Math.Pow(2, bitPerComponent) - 1;
            min = 0D;
            max = indexed ? maximum : 1D;
            interpolateConst = (max - min) / maximum;
            sMask = image.SMask;
            if (sMask != null)
            {
                sMaskLoader = new ImageLoader(sMask, state);
            }
            if (imageMask)
            {
                return;
            }
            colorSpace = image.ColorSpace;
            componentsCount = colorSpace.ComponentCount;
            iccColorSpace = colorSpace as ICCBasedColorSpace;
            if (colorSpace is IndexedColorSpace indexedColorSpace)
            {
                indexed = true;
                iccColorSpace = indexedColorSpace.BaseSpace as ICCBasedColorSpace;
            }
        }

        public Color GetColor(int x, int y)
        {
            return GetColor((y * (int)size.Width + x));
        }

        public Color GetColor(int index)
        {
            var componentIndex = index * componentsCount;
            var components = new PdfDirectObject[componentsCount];
            for (int i = 0; i < componentsCount; i++)
            {
                var value = componentIndex < buffer.Length ? buffer[componentIndex] : 0;
                var interpolate = indexed ? value : min + (value * (interpolateConst));
                components[i] = indexed
                    ? (PdfDirectObject)new PdfInteger((int)interpolate)
                    : (PdfDirectObject)new PdfReal(interpolate);
                componentIndex++;
            }
            return colorSpace.GetColor(components, null);
        }

        public void GetColor(int index, ref double[] components)
        {
            var componentIndex = index * componentsCount;

            for (int i = 0; i < componentsCount; i++)
            {
                var value = componentIndex < buffer.Length ? buffer[componentIndex] : 0;
                var interpolate = indexed ? value : min + (value * (interpolateConst));
                components[i] = interpolate;
                componentIndex++;
            }
        }

        public SKBitmap Load()
        {
            if (imageMask)
            {
                return LoadImageMask();
            }
            var info = new SKImageInfo((int)size.Width, (int)size.Height)
            {
                AlphaType = SKAlphaType.Premul,
            };
            if (iccColorSpace != null)
            {
                //info.ColorSpace = iccColorSpace.GetSKColorSpace();
            }

            // create the buffer that will hold the pixels
            var raster = new int[info.Width * info.Height];//var bitmap = new SKBitmap();
            var components = new double[componentsCount];//TODO stackalloc
            var maskComponents = new double[componentsCount];//TODO stackalloc
            for (int y = 0; y < info.Height; y++)
            {
                var row = y * info.Width;
                for (int x = 0; x < info.Width; x++)
                {
                    var index = row + x;
                    //var color = GetColor(index);
                    GetColor(index, ref components);
                    var skColor = colorSpace.GetSKColor(components, null);
                    if (sMaskLoader != null)
                    {
                        sMaskLoader.GetColor(index, ref maskComponents);
                        //alfa
                        skColor = skColor.WithAlpha((byte)(maskComponents[0] * 255));
                        //shaping
                        //for (int i = 0; i < color.Components.Count; i++)
                        //{
                        //    var m = sMaskLoader.matte == null ? 0D : ((IPdfNumber)sMaskLoader.matte[i]).DoubleValue;
                        //    var a = ((IPdfNumber)sMaskColor.Components[sMaskColor.Components.Count == color.Components.Count ? i : 0]).DoubleValue;
                        //    var c = ((IPdfNumber)color.Components[i]).DoubleValue;
                        //    color.Components[i] = new PdfReal(m + a * (c - m));
                        //}
                    }
                    raster[index] = (int)(uint)skColor;//bitmap.SetPixel(x, y, skColor);
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);
            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);

            return bitmap;
        }

        private SKBitmap LoadImageMask()
        {
            var info = new SKImageInfo((int)size.Width, (int)size.Height)
            {
                AlphaType = SKAlphaType.Premul,
            };
            var skColor = state.FillColorSpace.GetSKColor(state.FillColor);
            var raster = new int[info.Width * info.Height];

            for (int y = 0; y < info.Height; y++)
            {
                for (int x = 0; x < info.Width; x++)
                {
                    var index = (y * info.Width + x);
                    var value = index < buffer.Length ? buffer[index] : (byte)0;
                    raster[index] = (int)(uint)skColor.WithAlpha(value);
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);
            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);
            return bitmap;
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
    }
}