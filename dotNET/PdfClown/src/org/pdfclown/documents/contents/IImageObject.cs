using org.pdfclown.documents.contents.colorSpaces;
using org.pdfclown.objects;
using SkiaSharp;

namespace org.pdfclown.documents.contents
{
    public interface IImageObject
    {
        int BitsPerComponent { get; }
        ColorSpace ColorSpace { get; }
        PdfDirectObject Filter { get; }
        PdfDirectObject Parameters { get; }
        bytes.IBuffer Data { get; }
        SKSize Size { get; }
        IImageObject SMask { get; }
        PdfDirectObject Header { get; }

        PdfArray Matte { get; }

        SKBitmap LoadImage();
    }
}