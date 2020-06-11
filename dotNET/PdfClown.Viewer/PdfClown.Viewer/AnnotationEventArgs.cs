using PdfClown.Documents.Interaction.Annotations;
using System;
using System.ComponentModel;

namespace PdfClown.Viewer
{
    public class AnnotationEventArgs : CancelEventArgs
    {
        public AnnotationEventArgs(Annotation annotation) : base(false)
        {
            this.Annotation = annotation;
        }

        public Annotation Annotation { get; }
    }
}