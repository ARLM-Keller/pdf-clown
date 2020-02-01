# pdf-clown
https://sourceforge.net/projects/clown/ mirror

# Fork task

- Implement pdf visualization by SkiaSharp (Xamarin Forms. Android, iOS, UWP and WPF backends for start)

## Implemented 

- Rendering Pdf on Xamarin.Forms SkiaSharp canvas
- Rendering Annotations(except 3D)
- Move project to .net standard
- Performance improvements(String comparison, Remove some Reflections invocation, DOM cache)

## TODO

- Images
  - Replace freeImage library by ImageSharp https://github.com/SixLabors/ImageSharp . For specific formats(tiff fax, jbig2) port mozilla pdf viewer image loading from js to c#(possible fork ImageSharp). 
  - Some performance & memory hacks for large images
- Fonts
  - Font Type0(postscript) - find the way to get glyph paths on windows(maybe freetype, harfbuzz)
  - Glyph rendering code is ugly
- Encrypted PDF (take basic solution from PDF Sharp)
- Check regressions on PDF creation(cache collision).
