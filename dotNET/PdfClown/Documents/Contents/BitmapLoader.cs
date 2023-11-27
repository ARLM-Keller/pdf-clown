﻿
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
    public class BitmapLoader
    {
        public static SKBitmap Load(IImageObject imageObject, GraphicsState state)
        {
            var data = imageObject.Data;
            var filter = imageObject.Filter;
            if (filter is PdfArray filterArray)
            {
                var parameterArray = imageObject.Parameters as PdfArray;
                for (int i = 0; i < filterArray.Count; i++)
                {
                    var filterItem = (PdfName)filterArray[i];
                    var parameterItem = parameterArray?[i];
                    var temp = ExtractImage(imageObject, data, filterItem, parameterItem, imageObject.Header);
                    if (temp is IByteStream tempBuffer)
                    {
                        data = tempBuffer;
                    }
                    else if (temp is SKBitmap tempImage)
                    {
                        return tempImage;
                    }
                }
            }
            else if (filter != null)
            {
                var filterItem = (PdfName)filter;
                var temp = ExtractImage(imageObject, data, filterItem, imageObject.Parameters, imageObject.Header);
                if (temp is IByteStream tempBuffer)
                {
                    data = tempBuffer;
                }
                else if (temp is SKBitmap tempImage)
                {
                    return tempImage;
                }
            }

            var loader = new BitmapLoader(imageObject, data.AsMemory(), state);
            return loader.Load();
        }

        public static object ExtractImage(IImageObject imageObject, IByteStream data, PdfName filterItem,
            PdfDirectObject parameterItem, IDictionary<PdfName, PdfDirectObject> header)
        {
            //using (var stream = new MemoryStream(data.GetBuffer()))
            //using (var skiaData = SKData.Create(stream))
            //using (var codec = SKCodec.Create(skiaData))
            //{
            //    var sizei = codec.GetScaledDimensions(0.5f);
            //    var nearest = new SKImageInfo(sizei.Width, sizei.Height);
            //    bitmap = SKBitmap.Decode(codec, nearest);
            //}


            //if (filterItem.Equals(PdfName.DCTDecode)
            //    || filterItem.Equals(PdfName.DCT))
            //{
            //    SKBitmap bitmap = SKBitmap.Decode(data.GetBuffer());

            //    if (imageObject.SMask != null)
            //    {
            //        var info = bitmap.Info;
            //        BitmapLoader smaskLoader = new BitmapLoader(imageObject.SMask, null);
            //        var smask = smaskLoader.LoadSKMask();
            //        //bitmap.InstallMaskPixels(smask); //Skia bug
            //        //vs
            //        for (int y = 0; y < info.Height; y++)
            //        {
            //            var row = y * info.Width;
            //            for (int x = 0; x < info.Width; x++)
            //            {
            //                var index = row + x;
            //                var alpha = smask.GetAddr8(x, y);
            //                if (smaskLoader.decode[0] == 1)
            //                {
            //                    alpha = (byte)(255 - alpha);
            //                }
            //                var color = bitmap.GetPixel(x, y).WithAlpha(alpha);
            //                bitmap.SetPixel(x, y, color);
            //            }
            //        }
            //        smask.FreeImage();
            //    }
            //    return bitmap;
            //}

            if (filterItem != null)
            {
                return data.Extract(filterItem, parameterItem, imageObject.Header);
            }

            return data;
        }

        private GraphicsState state;
        private IImageObject image;
        private ColorSpace colorSpace = ColorSpace.Wrap(PdfName.DeviceGray);
        private ICCBasedColorSpace iccColorSpace;
        private int bitsPerComponent;
        private PdfArray matte;
        private int width;
        private int height;
        private bool indexed;
        private int componentsCount = 1;
        private int padding;
        private float maximum;
        private float min;
        private float max;
        private float interpolateConst;
        private Memory<byte> buffer;
        private bool imageMask;
        private float[] decode;
        private IImageObject sMask;
        private BitmapLoader sMaskLoader;
        private int rowBits;
        private int rowBytes;

        public BitmapLoader(IImageObject image, GraphicsState state)
        {
            var buffer = image.Data;
            var data = buffer.AsMemory();
            var filter = image.Filter;
            if (filter != null)
            {
                buffer = buffer.Extract(filter, image.Parameters, image.Header);
                data = buffer.AsMemory();
            }
            Init(image, data, state);
        }

        public BitmapLoader(IImageObject image, Memory<byte> buffer, GraphicsState state)
        {
            Init(image, buffer, state);
        }

        public int BitsPerComponent { get => bitsPerComponent; set => bitsPerComponent = value; }
        public Memory<byte> Buffer { get => buffer; set => buffer = value; }
        public int ComponentsCount { get => componentsCount; set => componentsCount = value; }
        public int RowBytes { get => rowBytes; set => rowBytes = value; }
        public int Height { get => height; set => height = value; }
        public int Width { get => width; set => width = value; }

        private void Init(IImageObject image, Memory<byte> buffer, GraphicsState state)
        {
            this.state = state;
            this.image = image;
            this.buffer = buffer;
            imageMask = image.ImageMask;
            decode = image.Decode;
            matte = image.Matte;
            width = (int)image.Size.Width;
            height = (int)image.Size.Height;
            bitsPerComponent = image.BitsPerComponent;

            maximum = (float)Math.Pow(2, bitsPerComponent) - 1;
            min = 0F;
            max = indexed ? maximum : 1F;
            interpolateConst = (max - min) / maximum;
            sMask = image.SMask;
            if (sMask != null)
            {
                sMaskLoader = new BitmapLoader(sMask, state);
            }
            if (!imageMask)
            {
                colorSpace = image.ColorSpace;
                if (colorSpace == null)// || (image.Filter?.Equals(PdfName.DCTDecode) ?? false) || (image.Filter?.Equals(PdfName.DCT) ?? false))
                {
                    var bitPerColor = (buffer.Length * 8) / width / height;
                    var sizeComponentCount = bitPerColor / bitsPerComponent;
                    if (sizeComponentCount < 3)
                        colorSpace = DeviceGrayColorSpace.Default;
                    else if (sizeComponentCount == 3)
                        colorSpace = DeviceRGBColorSpace.Default;
                    else
                        colorSpace = DeviceCMYKColorSpace.Default;
                }

                componentsCount = colorSpace.ComponentCount;
                iccColorSpace = colorSpace as ICCBasedColorSpace;
                if (colorSpace is IndexedColorSpace indexedColorSpace)
                {
                    indexed = true;
                    iccColorSpace = indexedColorSpace.BaseSpace as ICCBasedColorSpace;
                }
            }
            if (decode == null)
            {
                decode = new float[componentsCount * 2];
                for (int i = 0; i < componentsCount * 2; i++)
                {
                    decode[i] = i % 2;
                }
            }
            // calculate row padding
            padding = 0;
            rowBits = (int)width * componentsCount * bitsPerComponent;
            rowBytes = rowBits / 8;
            if (rowBits % 8 > 0)
            {
                padding = 8 - (rowBits % 8);
                rowBytes++;
            }
        }

        public void GetColor(int y, int x, int index, Span<float> components, Span<byte> buffer)
        {
            switch (bitsPerComponent)
            {
                case 1:
                    for (int i = 0; i < componentsCount; i++)
                    {
                        var byteIndex = rowBytes * y + x / 8;
                        var byteValue = buffer[byteIndex];
                        //if (emptyBytes)
                        //    byteIndex += y;
                        var bitIndex = 7 - x % 8;
                        var value = (byteValue >> bitIndex & 1) == 0 ? 0 : 1;
                        if (decode[0] == 1)
                        {
                            value = value == 0 ? 1 : 0;
                        }
                        var interpolate = indexed ? value : min + value * interpolateConst;
                        components[i] = interpolate;
                    }
                    break;
                case 2:
                    for (int i = 0; i < componentsCount; i++)
                    {
                        var byteIndex = rowBytes * y + x / 4;
                        var byteValue = buffer[byteIndex];
                        //if (emptyBytes)
                        //    byteIndex += y;
                        var bitIndex = 6 - x % 4 * 2;
                        var value = byteValue >> bitIndex & 0b11;
                        if (decode[0] == 1)
                        {
                            value = 3 - value;
                        }
                        var interpolate = indexed ? value : min + value * interpolateConst;
                        components[i] = interpolate;
                    }
                    break;
                case 4:
                    for (int i = 0; i < componentsCount; i++)
                    {
                        var byteIndex = rowBytes * y + x / 2;
                        var byteValue = buffer[byteIndex];
                        //if (emptyBytes)
                        //    byteIndex += y;
                        var bitIndex = 4 - x % 2 * 4;
                        var value = byteValue >> bitIndex & 0b1111;
                        if (decode[0] == 1)
                        {
                            value = 3 - value;
                        }
                        var interpolate = indexed ? value : min + value * interpolateConst;
                        components[i] = interpolate;
                    }
                    break;
                case 8:
                    {
                        var componentIndex = index * componentsCount;
                        for (int i = 0; i < componentsCount; i++)
                        {
                            var value = buffer[componentIndex];
                            if (decode[0] == 1)
                            {
                                value = (byte)(255 - value);
                            }
                            var interpolate = indexed ? value : min + value * interpolateConst;
                            components[i] = interpolate;
                            componentIndex++;
                        }
                    }
                    break;
                case 16:
                    {
                        var componentIndex = index * componentsCount * 2;
                        for (int i = 0; i < componentsCount; i++)
                        {
                            var value = (buffer[componentIndex] << 8) + (buffer[componentIndex + 1] << 0);
                            if (decode[0] == 1)
                            {
                                value = ushort.MaxValue - value;
                            }
                            var interpolate = indexed ? value : min + value * interpolateConst;
                            components[i] = interpolate;
                            componentIndex += 2;
                        }
                        break;
                    }
                case 24:
                    {
                        var componentIndex = index * componentsCount * 3;
                        for (int i = 0; i < componentsCount; i++)
                        {
                            var value = buffer[componentIndex] << 16 | buffer[componentIndex + 1] << 8 | buffer[componentIndex + 1] << 0;
                            if (decode[0] == 1)
                            {
                                value = ushort.MaxValue - value;
                            }
                            var interpolate = indexed ? value : min + value * interpolateConst;
                            components[i] = interpolate;
                            componentIndex += 3;
                        }
                        break;
                    }
                case 32:
                    {
                        var componentIndex = index * componentsCount * 4;
                        for (int i = 0; i < componentsCount; i++)
                        {
                            var value = buffer[componentIndex] << 24 | buffer[componentIndex + 1] << 16 | buffer[componentIndex + 2] << 8 | buffer[componentIndex + 3] << 0;
                            if (decode[0] == 1)
                            {
                                value = ushort.MaxValue - value;
                            }
                            var interpolate = indexed ? value : min + value * interpolateConst;
                            components[i] = interpolate;
                            componentIndex += 4;
                        }
                        break;
                    }
                default:
                    break;
            }
        }

        public SKBitmap Load()
        {
            if (imageMask)
            {
                return LoadMask();
            }
            else if (componentsCount == 1 && !indexed)
            {
                return LoadGray();
            }
            else
            {
                return LoadRgbImage();
            }
        }

        public SKBitmap LoadRgbImage()
        {
            var info = new SKImageInfo(width, height)
            {
                AlphaType = SKAlphaType.Unpremul,
            };
            if (iccColorSpace != null)
            {
                //info.ColorSpace = iccColorSpace.GetSKColorSpace();
            }
            // create the buffer that will hold the pixels
            var raster = new uint[info.Width * info.Height];//var bitmap = new SKBitmap();
            Span<float> components = stackalloc float[componentsCount];
            Span<float> maskComponents = sMaskLoader != null ? stackalloc float[sMaskLoader.componentsCount] : Span<float>.Empty;
            var buffer = this.buffer.Span;
            var maskBuffer = sMaskLoader != null ? sMaskLoader.buffer.Span : Span<byte>.Empty;
            for (int y = 0; y < info.Height; y++)
            {
                var row = y * info.Width;
                for (int x = 0; x < info.Width; x++)
                {
                    var index = row + x;
                    GetColor(y, x, index, components, buffer);
                    var skColor = colorSpace.GetSKColor(components, null);
                    if (sMaskLoader != null)
                    {
                        sMaskLoader.GetColor(y, x, index, maskComponents, maskBuffer);
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
                    raster[index] = (uint)skColor;//bitmap.SetPixel(x, y, skColor);
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var handler = GCHandle.Alloc(raster, GCHandleType.Pinned);
            var ptr = handler.AddrOfPinnedObject();

            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr, info.RowBytes, (addr, ctx) => handler.Free(), null);

            return bitmap;
        }

        public SKBitmap LoadGray()
        {
            var info = new SKImageInfo(width, height)
            {
                AlphaType = SKAlphaType.Unpremul,
                ColorType = SKColorType.Gray8
            };
            // create the buffer that will hold the pixels
            var raster = new byte[info.Width * info.Height];
            Span<float> components = stackalloc float[componentsCount];
            var buffer = this.buffer.Span;
            for (int y = 0; y < info.Height; y++)
            {
                var row = y * info.Width;
                for (int x = 0; x < info.Width; x++)
                {
                    var index = (row + x);
                    GetColor(y, x, index, components, buffer);
                    var skColor = colorSpace.GetSKColor(components, null);

                    raster[index] = skColor.Red;
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);
            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);

            return bitmap;
        }

        public SKMask LoadSKMask()
        {
            //Bug https://bugs.chromium.org/p/skia/issues/detail?id=6847
            var format = bitsPerComponent == 1 ? SKMaskFormat.BW : SKMaskFormat.A8;
            var skMask = SKMask.Create(buffer.Span, SKRectI.Create(0, 0, width, height), (uint)rowBytes, format);
            return skMask;
        }

        public SKBitmap LoadMask()
        {
            var buffer = this.buffer.Span;
            var info = new SKImageInfo(width, height)
            {
                AlphaType = SKAlphaType.Unpremul,
                ColorType = SKColorType.Alpha8
            };
            var raster = new byte[width * height];

            for (int y = 0; y < height; y++)
            {
                var row = y * width;
                for (int x = 0; x < width; x++)
                {
                    var index = (row + x);
                    byte value = 0;
                    if (bitsPerComponent == 1)
                    {
                        var byteIndex = (rowBytes * y) + x / 8;
                        var byteValue = buffer[byteIndex];

                        var bitIndex = 7 - x % 8;
                        value = ((byteValue >> bitIndex) & 1) == 1 ? (byte)255 : (byte)0;
                        if (decode[0] == 1)
                        {
                            value = value == 0 ? (byte)255 : (byte)0;
                        }
                    }
                    else if (bitsPerComponent == 8)
                    {
                        value = buffer[index];
                        if (decode[0] == 1)
                        {
                            value = (byte)(255 - value);
                        }
                    }
                    else
                    { }
                    raster[index] = value;// (int)(uint)skColor.WithAlpha(value);
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