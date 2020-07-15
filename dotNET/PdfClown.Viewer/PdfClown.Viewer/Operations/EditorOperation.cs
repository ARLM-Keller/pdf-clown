namespace PdfClown.Viewer
{
    public abstract class EditorOperation
    {
        public PdfDocumentView Document { get; set; }

        public OperationType Type { get; set; }

        public int PageIndex { get; set; }

        public object Property { get; set; }

        public object OldValue { get; set; }

        public object NewValue { get; set; }

        public virtual object EndOperation()
        {
            Document?.OnEndOperation(null);
            return null;
        }

        public abstract void Redo();

        public abstract void Undo();

        public virtual EditorOperation Clone(PdfDocumentView document)
        {
            var cloned = (EditorOperation)this.MemberwiseClone();
            cloned.Document = document;
            return cloned;
        }
    }
}
