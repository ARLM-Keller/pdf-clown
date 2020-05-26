# Pdf-Clown
https://sourceforge.net/projects/clown/ mirror

## Fork Task

- Pdf visualization by [SkiaSharp](https://github.com/mono/SkiaSharp).
- UI integration by [Xamarin.Forms](https://github.com/xamarin/Xamarin.Forms).

## Status

- Rendering Pdf on Xamarin.Forms 'SkiaSharp.SKCanvas'
  - Basic painting reguired just replace System.Drawing by SkiaSharp, thanks to author Stefano Chizzolini
  - New mandatory features of SkiaSharp(for Tiling, Image Mask, Gradient and Patch shaders) by [mattleibow](https://github.com/mattleibow)
  - XObject Masking by [wrappa](https://github.com/warappa)
- Change Code formatting
- Rendering Annotations(except 3D)
- Move project to .net standard 2.0
- Several Xamarin.Forms frontends (Android, iOS, UWP and WPF)
- Performance improvements
  - Strings comparison
  - Suppress reflections invocation
  - DOM cache
- Fonts and Encryption by integrate [Apache PdfBox Project](https://pdfbox.apache.org/) from [mirror](https://github.com/apache/pdfbox).
  - Source code translated from java to C#
  - Full Fonts processing & text rendering engine
  - CCITTFax and other fixes of Images loading engine
  - Decrypt PDF
  - LZWFilter
- Images and ColorSpaces by integrate [Mozilla Pdf.js](https://github.com/mozilla/pdf.js)
  - Source code translated from js to C#
  - JPX, CCITTFax, JBIG2 - decoding
  - Function Type 0

## TODO

- Rendering
  - Move from SKPicture to to SKImage with rescan on each scale change, without in-memory bitmaps caching(maybe file cache or redecoding)
  - Possible GL context with SKImage
  - Decoding optimization
- Encryption.
  - Encrypt not tested
  - Public key Certificat - requer completly rework PdfBox solution
- Function type 4 (PdfBox or Pdf.js)
- Patch Shaders (PdfBox or Pdf.js)
- Move buffered IO to use Span\<byte\>
- Check regressions on PDF creation(fonts embeding, text postions, cache collision).
