using System;

namespace PdfClown.Viewer
{
    public class OperationEventArgs : EventArgs
    {
        public OperationEventArgs(object result)
        {
            this.Result = result;
        }

        public object Result { get; set; }
    }
}