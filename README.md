# Pdf-Clown
https://sourceforge.net/projects/clown/ mirror

# Fork Task

- Implement pdf visualization by SkiaSharp (Xamarin Forms. Android, iOS, UWP and WPF backends for start)

## Status

- Rendering Pdf on Xamarin.Forms SkiaSharp canvas
- Rendering Annotations(except 3D)
- Move project to .net standard
- Performance improvements(String comparison, Remove some Reflections invocation, DOM cache)
- XObject Alfa Masking by wrappa
- Integrate [Apache PdfBox Project] (https://pdfbox.apache.org/) from [mirror](https://github.com/apache/pdfbox). 
  - Translat from java to C#
  - Full Fonts processing & rendering engine
  - CCITTFax and other fixes of Images loading engine
  - Reguired fix regressions (Compositor and TextScan)
  
## TODO

- Images
  - For specific formats(tiff fax, jbig2) port mozilla pdf viewer image loading from js to c#(possible fork ImageSharp). 
  - Some performance & memory hacks for large images

- Encrypted PDF (take basic solution from PDF Sharp)
- Move buffered IO to use Spans
- Check regressions on PDF creation(cache collision).
