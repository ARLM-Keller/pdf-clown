using PdfClown.Objects;
using PdfClown.Util;

namespace PdfClown.Documents.Interaction.Annotations
{
    public enum MarkupIntent
    {
        Text,

        FreeText,
        FreeTextCallout,
        FreeTextTypeWriter,

        Line,
        LineArrow,
        LineDimension,

        Polygon,
        PolygonCloud,
        PolygonDimension,

        PolyLine,
        PolyLineDimension
    }

    internal static class MarkupIntentExtension
    {
        private static readonly BiDictionary<MarkupIntent, PdfName> codes;

        static MarkupIntentExtension()
        {
            codes = new BiDictionary<MarkupIntent, PdfName>
            {
                [MarkupIntent.Text] = PdfName.Text,
                [MarkupIntent.FreeText] = PdfName.FreeText,
                [MarkupIntent.FreeTextCallout] = PdfName.FreeTextCallout,
                [MarkupIntent.FreeTextTypeWriter] = PdfName.FreeTextTypeWriter,

                [MarkupIntent.Line] = PdfName.Line,
                [MarkupIntent.LineArrow] = PdfName.LineArrow,
                [MarkupIntent.LineDimension] = PdfName.LineDimension,

                [MarkupIntent.Polygon] = PdfName.Polygon,
                [MarkupIntent.PolygonCloud] = PdfName.PolygonCloud,
                [MarkupIntent.PolygonDimension] = PdfName.PolygonDimension,
                [MarkupIntent.PolyLine] = PdfName.PolyLine,
                [MarkupIntent.PolyLineDimension] = PdfName.PolyLineDimension
            };
        }

        public static MarkupIntent? Get(PdfName name)
        {
            if (name == null)
                return null;

            return codes.GetKey(name);
        }

        public static PdfName GetCode(this MarkupIntent? intent)
        {
            return intent == null ? null : codes[intent.Value];
        }
    }
}
