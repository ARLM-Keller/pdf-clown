using PdfClown.Bytes;
using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Contents.Entities;
using PdfClown.Documents.Contents.XObjects;
using files = PdfClown.Files;
using PdfClown.Objects;

using System;
using System.IO;
using System.Linq;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample demonstrates how to replace images appearing in a PDF document's pages
      through their resource names.</summary>
    */
    public class ImageSubstitutionSample
      : Sample
    {
        public override void Run(
          )
        {
            // 1. Opening the PDF file...
            string filePath = PromptFileChoice("Please select a PDF file");
            using (var file = new files::File(filePath))
            {
                Document document = file.Document;

                // 2. Replace the images!
                ReplaceImages(document);

                // 3. Serialize the PDF file!
                Serialize(file, "Image substitution", "substituting a document's images", "image replacement");
            }
        }

        private void ReplaceImages(
          Document document
          )
        {
            // Get the image used to replace existing ones!
            Image image = Image.Get(GetResourcePath("images" + Path.DirectorySeparatorChar + "gnu.jpg")); // Image is an abstract entity, as it still has to be included into the pdf document.
                                                                                                          // Add the image to the document!
            XObject imageXObject = image.ToXObject(document); // XObject (i.e. external object) is, in PDF spec jargon, a reusable object.
                                                              // Looking for images to replace...
            foreach (Page page in document.Pages)
            {
                Resources resources = page.Resources;
                XObjectResources xObjects = resources.XObjects;
                if (xObjects == null)
                    continue;

                foreach (PdfName xObjectKey in xObjects.Keys.ToList())
                {
                    XObject xObject = xObjects[xObjectKey];
                    // Is the page's resource an image?
                    if (xObject is ImageXObject)
                    {
                        Console.WriteLine("Substituting " + xObjectKey + " image xobject.");
                        xObjects[xObjectKey] = imageXObject;
                    }
                }
            }
        }
    }
}