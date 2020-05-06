# Pdf-Clown
https://sourceforge.net/projects/clown/ mirror

# Fork Task

- Pdf visualization by [SkiaSharp](https://github.com/mono/SkiaSharp).
- UI integration by [Xamarin.Forms](https://github.com/xamarin/Xamarin.Forms).

## Status

- Rendering Pdf on Xamarin.Forms 'SkiaSharp.SKCanvas'
  - Basic painting reguire just replace System.Drawing by SkiaSharp, thanks to author Stefano Chizzolini
  - Tiling Pattern by [mattleibow](https://github.com/mattleibow) and [Gillibald](https://github.com/Gillibald)
  - XObject alfa mask by [wrappa](https://github.com/warappa)
- Rendering Annotations(except 3D)
- Several Xamarin.Forms frontends (Android, iOS, UWP and WPF )
- Move project to .net standard
- Performance improvements
  - Strings comparison
  - Remove reflections invocation
  - DOM cache
- Fonts by integrate [Apache PdfBox Project](https://pdfbox.apache.org/) from [mirror](https://github.com/apache/pdfbox).
  - Translated from java to C#
  - Full Fonts processing & text rendering engine
  - CCITTFax and other fixes of Images loading engine

## TODO

- Images
  - Add support for custome bits ber component ranges
  - For specific formats(tiff, fax, jbig2) port [mozilla pdf viewer](https://github.com/mozilla/pdf.js) image loading engine(from js to c#).
  - Some performance & scale & memory improvements for large images
- CMYK Color Space - add ICC Profile for monitors
- Encrypted PDF (take basic solution from PDF Sharp)
- Move buffered IO to use Spans
- Check regressions on PDF creation(fonts embeding, text postions, cache collision).
