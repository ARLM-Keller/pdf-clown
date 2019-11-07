using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Objects;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Objects;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample demonstrates how to insert screen annotations to display media clips inside
      a PDF document.</summary>
    */
    public class VideoEmbeddingSample
      : Sample
    {
        public override void Run(
          )
        {
            // 1. Instantiate the PDF file!
            File file = new File();
            Document document = file.Document;

            // 2. Insert a new page!
            Page page = new Page(document);
            document.Pages.Add(page);

            // 3. Insert a video into the page!
            new Screen(
              page,
              SKRect.Create(10, 10, 320, 180),
              "PJ Harvey - Dress (part)",
              GetResourcePath("video" + System.IO.Path.DirectorySeparatorChar + "pj_clip.mp4"),
              "video/mp4"
              );

            // 4. Serialize the PDF file!
            Serialize(file, "Video embedding", "inserting screen annotations to display media clips inside a PDF document", "video embedding");
        }
    }
}