# Pdf-Clown
https://sourceforge.net/projects/clown/ mirror

## Fork Task

- Pdf visualization by [SkiaSharp](https://github.com/mono/SkiaSharp).
- UI integration by [Xamarin.Forms](https://github.com/xamarin/Xamarin.Forms).

## Status

- Rendering Pdf on Xamarin.Forms 'SkiaSharp.SKCanvas'
  - Basic painting reguired just replace System.Drawing by SkiaSharp, thanks to author Stefano Chizzolini
  - New mandatory features of SkiaSharp(for Tiling, Image Mask, Gradient and Patch shaders) by [mattleibow](https://github.com/mattleibow)
  - XObject Masking by [warappa](https://github.com/warappa)

- Change Code formatting
- Rendering Annotations
- Move project to .net standard 2.1
- Several Xamarin.Forms frontends (Android, iOS, WPF)
- Performance improvements
  - Strings comparison
  - Suppress reflections invocation
  - PdfObjects Wrappers caching
  - Move buffered IO to use Memory, Span\<byte\>
- Fonts, Encryption, Functions, Shadings by integrate [Apache PdfBox Project](https://pdfbox.apache.org/) from [mirror](https://github.com/apache/pdfbox).
  - Source code translated from java to C#
  - Full Fonts processing & text rendering engine
  - LZW, CCITTFax and other fixes of Images loading engine
  - Decrypt PDF
  - Functions 0-4
  - Shaders 4,5,6
- Images and ColorSpaces by integrate [Mozilla Pdf.js](https://github.com/mozilla/pdf.js)
  - Source code translated from js to C#
  - JPX, CCITTFax, JBIG2 - decoding

## TODO

- Rendering
  - Move from SKPicture to to SKImage with rescan on each scale change, without in-memory bitmaps caching(maybe file cache or redecoding)
  - Possible GL context with SKImage
  - Decoding streaming and optimization
  - Masking won't work correctly 
- Encryption.
  - Encrypt not tested
  - Public key Certificat - requer completly rework PdfBox solution
  - Signature Fields

