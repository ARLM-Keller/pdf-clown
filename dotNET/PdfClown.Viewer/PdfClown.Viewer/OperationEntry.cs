using PdfClown.Documents.Interaction.Annotations;

namespace PdfClown.Viewer
{
    public class OperationEntry
    {
        private Annotation annotation;

        public OperationType Type { get; set; }

        public Annotation Annotation
        {
            get => annotation;
            set => annotation = value;
        }

        public object Property { get; set; }

        public object OldValue { get; set; }

        public object NewValue { get; set; }


    }
}
