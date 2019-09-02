
using BitMiracle.LibTiff.Classic;
using org.pdfclown.documents.contents.colorSpaces;
using org.pdfclown.objects;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace org.pdfclown.documents.contents
{
    public class ImageLoader
    {
        public static SKBitmap Load(IImageObject imageObject)
        {
            var buffer = imageObject.Data;
            var data = buffer.ToByteArray();
            var image = SKBitmap.Decode(data);
            if (image == null)
            {
                var filter = imageObject.Filter;
                if (filter != null)
                {
                    bytes.Buffer.Decode(buffer, filter, imageObject.Parameters ?? imageObject.Header);
                    data = buffer.ToByteArray();
                }
                image = SKBitmap.Decode(data);
            }
            if (image == null)
            {
                if (IsTiff(data))
                {
                    image = LoadTiff(data);
                }
                else
                {
                    var imageLoader = new ImageLoader(imageObject, data);
                    image = imageLoader.Load();
                }
            }
            return image;
        }

        private IImageObject image;
        private ColorSpace colorSpace;
        private int bitPerComponent;
        private SKSize size;
        private bool indexed;
        private int componentsCount;
        private double maximum;
        private double min;
        private double max;
        private byte[] buffer;

        public ImageLoader(IImageObject image)
        {
            var buffer = image.Data;
            var data = buffer.ToByteArray();
            var filter = image.Filter;
            if (filter != null)
            {
                bytes.Buffer.Decode(buffer, filter, image.Parameters ?? image.Header);
                data = buffer.ToByteArray();
            }
            Init(image, data);
        }

        public ImageLoader(IImageObject image, byte[] buffer)
        {
            Init(image, buffer);
        }

        private void Init(IImageObject image, byte[] buffer)
        {
            this.image = image;
            this.buffer = buffer;
            colorSpace = image.ColorSpace;
            bitPerComponent = image.BitsPerComponent;
            size = image.Size;
            indexed = colorSpace is IndexedColorSpace;
            componentsCount = colorSpace.ComponentCount;
            maximum = Math.Pow(2, bitPerComponent) - 1;
            min = 0D;
            max = indexed ? maximum : 1D;
        }

        public Color GetColor(int x, int y)
        {
            return GetColor((y * (int)size.Width + x));
        }

        public Color GetColor(int index)
        {
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
            return colorSpace.GetColor(components, null);
        }

        public SKBitmap Load()
        {
            var sMask = image.SMask;
            var sMaskLoader = (ImageLoader)null;
            if (sMask != null)
            {
                sMaskLoader = new ImageLoader(sMask);
            }
            SKImageInfo info = new SKImageInfo((int)size.Width, (int)size.Height)
            {
                //AlphaType = SKAlphaType.Opaque,
                //ColorType = SKColorType.Bgra8888
            };

            // create the buffer that will hold the pixels
            var raster = new int[info.Width * info.Height];

            for (int y = 0; y < info.Height; y++)
            {
                for (int x = 0; x < info.Width; x++)
                {
                    var index = (y * info.Width + x);
                    var color = GetColor(index);
                    if (sMaskLoader != null)
                    {

                    }
                    var skColor = colorSpace.GetColor(color);
                    raster[index] = (int)(uint)skColor;
                }
            }

            // get a pointer to the buffer, and give it to the bitmap
            var ptr = GCHandle.Alloc(raster, GCHandleType.Pinned);
            var bitmap = new SKBitmap();
            bitmap.InstallPixels(info, ptr.AddrOfPinnedObject(), info.RowBytes, (addr, ctx) => ptr.Free(), null);

            return bitmap;
        }

        //https://stackoverflow.com/a/50370515/4682355
        public static SKBitmap LoadTiff(byte[] tiffStream)
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
    }
}