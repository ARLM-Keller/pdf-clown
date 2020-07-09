using PdfClown.Documents;
using PdfClown.Documents.Contents;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using PdfClown.Tools;
using PdfClown.Util.Math;
using PdfClown.Util.Math.Geom;

using System;
using System.Collections;
using System.Collections.Generic;
using SkiaSharp;
using System.Text.RegularExpressions;

namespace PdfClown.Samples.CLI
{
    /**
      <summary>This sample demonstrates how to highlight text matching arbitrary patterns.</summary>
      <remarks>Highlighting is defined through text markup annotations.</remarks>
    */
    public class TextHighlightSample : Sample
    {
        private class TextHighlighter : TextExtractor.IIntervalFilter
        {
            private IEnumerator matchEnumerator;
            private Page page;

            public TextHighlighter(Page page, MatchCollection matches)
            {
                this.page = page;
                this.matchEnumerator = matches.GetEnumerator();
            }

            public Interval<int> Current
            {
                get
                {
                    Match current = (Match)matchEnumerator.Current;
                    return new Interval<int>(current.Index, current.Index + current.Length);
                }
            }

            object IEnumerator.Current
            {
                get { return this.Current; }
            }

            public void Dispose()
            {/* NOOP */}

            public bool MoveNext()
            { return matchEnumerator.MoveNext(); }

            public void Process(Interval<int> interval, ITextString match)
            {
                //var matrix = page.RotateMatrix;
                // Defining the highlight box of the text pattern match...
                IList<Quad> highlightQuads = new List<Quad>();
                {
                    /*
                      NOTE: A text pattern match may be split across multiple contiguous lines,
                      so we have to define a distinct highlight box for each text chunk.
                    */
                    Quad? textQuad = null;
                    foreach (TextChar textChar in match.TextChars)
                    {
                        var textCharQuad = textChar.Quad;
                        if (!textQuad.HasValue)
                        { textQuad = textCharQuad; }
                        else
                        {
                            if (textCharQuad.Top > textQuad.Value.Bottom)
                            {
                                highlightQuads.Add(textQuad.Value);
                                textQuad = textCharQuad;
                            }
                            else
                            { textQuad = Quad.Union(textQuad.Value, textCharQuad); }
                        }
                    }
                    highlightQuads.Add(textQuad.Value);
                }
                // Highlight the text pattern match!
                new TextMarkup(page, highlightQuads, null, TextMarkupType.Highlight);
            }

            public void Reset()
            { throw new NotSupportedException(); }
        }

        public override void Run()
        {
            // 1. Opening the PDF file...
            string filePath = PromptFileChoice("Please select a PDF file");
            using (var file = new File(filePath))
            {
                // Define the text pattern to look for!
                string textRegEx = PromptChoice("Please enter the pattern to look for: ");
                Regex pattern = new Regex(textRegEx, RegexOptions.IgnoreCase);

                // 2. Iterating through the document pages...
                TextExtractor textExtractor = new TextExtractor(true, true);
                foreach (Page page in file.Document.Pages)
                {
                    Console.WriteLine("\nScanning page " + page.Number + "...\n");

                    // 2.1. Extract the page text!
                    IDictionary<SKRect?, IList<ITextString>> textStrings = textExtractor.Extract(page);

                    // 2.2. Find the text pattern matches!
                    MatchCollection matches = pattern.Matches(TextExtractor.ToString(textStrings));

                    // 2.3. Highlight the text pattern matches!
                    textExtractor.Filter(
                      textStrings,
                      new TextHighlighter(page, matches)
                      );
                }

                // 3. Highlighted file serialization.
                Serialize(file);
            }
        }
    }
}