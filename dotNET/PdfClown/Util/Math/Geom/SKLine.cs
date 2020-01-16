using SkiaSharp;

namespace PdfClown.Util.Math.Geom
{
    //https://gamedev.stackexchange.com/a/111106
    public struct SKLine
    {
        public SKPoint a;
        public SKPoint b;

        public SKLine(SKPoint a, SKPoint b)
        {
            this.a = a;
            this.b = b;
        }

        public SKPoint Vector => a - b;

        public SKPoint NormalVector => SKPoint.Normalize(Vector);

        public static SKPoint? FindIntersection(SKLine a, SKLine b, bool segment)
        {
            float x1 = a.a.X;
            float y1 = a.a.Y;
            float x2 = a.b.X;
            float y2 = a.b.Y;

            float x3 = b.a.X;
            float y3 = b.a.Y;
            float x4 = b.b.X;
            float y4 = b.b.Y;

            float denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (denominator == 0)
                return null;
            if (segment)
            {
                float t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denominator;
                if (t < 0 || t > 1)
                    return null;
                float u = -((x1 - x2) * (y1 - y3) - (y1 - y2) * (x1 - x3)) / denominator;
                if (u < 0 || u > 1)
                    return null;
            }
            float xNominator = (x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4);
            float yNominator = (x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4);

            float px = xNominator / denominator;
            float py = yNominator / denominator;

            return new SKPoint(px, py);

        }

        public static SKPoint? FindIntersection(SKLine a, Quad q, bool segment)
        {
            return FindIntersection(a, q.TopLeft, q.TopRight, q.BottomRight, q.BottomLeft, segment);
        }

        public static SKPoint? FindIntersection(SKLine a, SKPoint c0, SKPoint c1, SKPoint c2, SKPoint c3, bool segment)
        {
            return FindIntersection(a, new SKLine(c0, c1), segment) ??
                FindIntersection(a, new SKLine(c1, c2), segment) ??
                FindIntersection(a, new SKLine(c2, c3), segment) ??
                FindIntersection(a, new SKLine(c3, c0), segment);
        }

    }
}
