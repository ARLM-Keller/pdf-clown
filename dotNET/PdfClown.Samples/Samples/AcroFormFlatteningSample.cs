using PdfClown.Documents;
using PdfClown.Tools;
using PdfClown.Files;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample demonstrates how to flatten the AcroForm fields of a PDF document.</summary>
    */
    public class AcroFormFlatteningSample
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

                // 2. Flatten the form!
                FormFlattener formFlattener = new FormFlattener();
                formFlattener.Flatten(document);

                // 3. Serialize the PDF file!
                Serialize(file);
            }
        }
    }
}