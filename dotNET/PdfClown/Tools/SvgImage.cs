using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfClown.Tools
{

    //http://www.pshul.com/2018/01/25/xamarin-forms-using-svg-images-with-skiasharp/
    public static class SvgImage
    {
        public static readonly Dictionary<string, SkiaSharp.Extended.Svg.SKSvg> SvgCache
            = new Dictionary<string, SkiaSharp.Extended.Svg.SKSvg>(StringComparer.Ordinal);
        public static SkiaSharp.Extended.Svg.SKSvg GetCache(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;
            if (!SvgCache.TryGetValue(imageName, out var svg))
            {
                var keyName = $"{typeof(SvgImage).Assembly.GetName().Name}.Assets.{imageName}.svg";
                using (Stream stream = typeof(SvgImage).Assembly.GetManifestResourceStream(keyName))
                {
                    if (stream != null)
                    {
                        svg = new SkiaSharp.Extended.Svg.SKSvg();
                        svg.Load(stream);
                        SvgCache[imageName] = svg;
                    }
                    else
                    {
                        SvgCache[imageName] = null;
                    }
                }
            }
            return svg;
        }

        public static SKMatrix GetMatrix(SkiaSharp.Extended.Svg.SKSvg svg, SKRect bound, float indent, float rotate = 0)
        {
            bound.Inflate(-indent, -indent);

            return GetMatrix(svg, bound.Left, bound.Top, bound.Width, bound.Height, rotate);
        }

        public static SKMatrix GetMatrix(SkiaSharp.Extended.Svg.SKSvg svg, float left, float top, float widthR, float heightR, float rotate = 0)
        {
            float canvasMin = Math.Min(widthR, heightR);
            // get the size of the picture
            float svgMax = Math.Max(svg.Picture.CullRect.Width, svg.Picture.CullRect.Height);
            // get the scale to fill the screen
            float scale = canvasMin / svgMax;
            var width = svg.Picture.CullRect.Width * scale;
            var height = svg.Picture.CullRect.Height * scale;

            var matrix = SKMatrix.MakeIdentity();

            if (rotate > 0)
            {
                matrix.PreConcat(SKMatrix.MakeRotationDegrees(rotate, (left + widthR / 2), (top + heightR / 2)));
            }

            matrix.PreConcat(SKMatrix.MakeTranslation(left + (widthR - width) / 2F, top + (heightR - height) / 2F));
            matrix.PreConcat(SKMatrix.MakeScale(scale, scale));

            return matrix;
        }

        public static void DrawImage(SKCanvas canvas, string imageName, SKColor color, SKRect bounds, float indent = 2)
        {
            var svg = GetCache(imageName);
            if (svg != null)
            {
                using (var paint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(color, SKBlendMode.SrcIn) })
                {
                    var matrix = GetMatrix(svg, bounds, indent);
                    canvas.DrawPicture(svg.Picture, ref matrix, paint);
                }
            }
        }
    }

}

