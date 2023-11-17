using PdfClown.Documents;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Files;

using System;
using SkiaSharp;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample generates a series of PDF pages from the default page formats available,
      varying both in size and orientation.</summary>
    */
    public class PageFormatSample : Sample
    {
        public override void Run()
        {
            // 1. PDF file instantiation.
            var file = new File();
            Document document = file.Document;

            // 2. Populate the document!
            Populate(document);

            // 3. Serialize the PDF file!
            Serialize(file, "Page Format", "page formats", "page formats");
        }

        private void Populate(Document document)
        {
            var bodyFont = FontType1.Load(document, FontName.CourierBold);

            Pages pages = document.Pages;
            var pageFormats = Enum.GetValues<PageFormat.SizeEnum>();
            var pageOrientations = Enum.GetValues<PageFormat.OrientationEnum>();
            foreach (var pageFormat in pageFormats)
            {
                foreach (var pageOrientation in pageOrientations)
                {
                    // Add a page to the document!
                    var page = new Page(document, PageFormat.GetSize(pageFormat, pageOrientation));
                    // Instantiates the page inside the document context.
                    pages.Add(page); // Puts the page in the pages collection.

                    // Drawing the text label on the page...
                    SKSize pageSize = page.Size;
                    var composer = new PrimitiveComposer(page);
                    composer.SetFont(bodyFont, 32);
                    composer.ShowText(
                      pageFormat + " (" + pageOrientation + ")", // Text.
                      new SKPoint(
                        pageSize.Width / 2,
                        pageSize.Height / 2
                        ), // Location: page center.
                      XAlignmentEnum.Center, // Places the text on horizontal center of the location.
                      YAlignmentEnum.Middle, // Places the text on vertical middle of the location.
                      45 // Rotates the text 45 degrees counterclockwise.
                      );
                    composer.Flush();
                }
            }
        }
    }
}