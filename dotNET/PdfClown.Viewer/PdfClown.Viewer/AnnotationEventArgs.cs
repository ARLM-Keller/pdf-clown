using PdfClown.Documents.Interaction.Annotations;
using System;

namespace PdfClown.Viewer
{
    public class AnnotationEventArgs : EventArgs
    {
        public AnnotationEventArgs(Annotation annotation)
        {
            this.Annotation = annotation;
        }

        public Annotation Annotation { get; }
    }
}