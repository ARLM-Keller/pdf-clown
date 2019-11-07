using PdfClown.Documents;
using PdfClown.Documents.Contents.Composition;
using PdfClown.Documents.Contents.Entities;
using PdfClown.Documents.Contents.Fonts;
using PdfClown.Documents.Contents.XObjects;
using PdfClown.Documents.Interaction.Actions;
using PdfClown.Documents.Interaction.Navigation;
using PdfClown.Files;

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample demonstrates how to apply actions to a document.</summary>
      <remarks>In this case, on document-opening a go-to-page-2 action is triggered;
      then on page-2-opening a go-to-URI action is triggered.</remarks>
    */
    public class ActionSample
      : Sample
    {
        public override void Run(
          )
        {
            // 1. Opening the PDF file...
            string filePath = PromptFileChoice("Please select a PDF file");
            using (var file = new File(filePath))
            {
                Document document = file.Document;
                Page page = document.Pages[1]; // Page 2 (zero-based index).

                // 2. Applying actions...
                // 2.1. Local go-to.
                /*
                  NOTE: This statement instructs the PDF viewer to go to page 2 on document opening.
                */
                document.Actions.OnOpen = new GoToLocal(
                  document,
                  new LocalDestination(page) // Page 2 (zero-based index).
                  );

                // 2.2. Remote go-to.
                /*
                  NOTE: This statement instructs the PDF viewer to navigate to the given URI on page 2
                  opening.
                */
                page.Actions.OnOpen = new GoToURI(
                  document,
                  new Uri("http://www.sourceforge.net/projects/clown")
                  );

                // 3. Serialize the PDF file!
                Serialize(file, "Actions", "applying actions", "actions, creation, local goto, remote goto");
            }
        }
    }
}