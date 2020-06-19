using PdfClown.Documents.Interaction.Annotations;
using SkiaSharp;
using System;

namespace PdfClown.Viewer
{
    public class OperationEntry
    {

        private Annotation annotation;

        public PdfDocumentView Document;

        public OperationType Type { get; set; }

        public Annotation Annotation
        {
            get => annotation;
            set => annotation = value;
        }

        public object Property { get; set; }

        public object OldValue { get; set; }

        public object NewValue { get; set; }

        public OperationEntry Clone(PdfDocumentView document)
        {
            var cloned = (OperationEntry)this.MemberwiseClone();
            cloned.Document = document;
            cloned.Annotation = document.FindAnnotation(Annotation.Name)
                ?? (Annotation)Annotation.Clone(document.Document);

            if (cloned.Property is ControlPoint controlPoint)
                cloned.Property = controlPoint.Clone(cloned.Annotation);

            return cloned;
        }

        public virtual void Redo()
        {
            switch (Type)
            {
                case OperationType.AnnotationAdd:
                    Document.AddAnnotation(Annotation);
                    break;
                case OperationType.AnnotationRemove:
                    Document.RemoveAnnotation(Annotation);
                    break;
                case OperationType.AnnotationDrag:
                    Annotation.MoveTo((SKRect)NewValue);
                    break;
                case OperationType.AnnotationSize:
                    Annotation.Box = (SKRect)NewValue;
                    break;
                case OperationType.AnnotationRePage:
                    var page = Document.Pages[(int)NewValue];
                    Annotation.Page = page;
                    break;
                case OperationType.AnnotationColor:
                    Annotation.SKColor = (SKColor)NewValue;
                    break;
                case OperationType.AnnotationText:
                    Annotation.Text = (string)NewValue;
                    break;
                case OperationType.AnnotationSubject:
                    Annotation.Subject = (string)NewValue;
                    break;
                case OperationType.PointMove:
                    {
                        if (Property is ControlPoint controlPoint)
                        {
                            controlPoint.Point = (SKPoint)NewValue;
                        }
                        break;
                    }
                case OperationType.PointAdd:
                    {
                        if (Property is IndexControlPoint controlPoint)
                        {
                            if (Annotation is VertexShape vertexShape)
                            {
                                vertexShape.InsertPoint(controlPoint.Index, (SKPoint)NewValue);
                            }
                        }
                    }
                    break;
                case OperationType.PointRemove:
                    {
                        if (Property is IndexControlPoint controlPoint)
                        {
                            if (Annotation is VertexShape vertexShape)
                            {
                                vertexShape.RemovePoint(controlPoint.Index);
                            }
                        }
                    }
                    break;
            }
        }

        public virtual void Undo()
        {
            switch (Type)
            {
                case OperationType.AnnotationAdd:
                    Document.RemoveAnnotation(Annotation);
                    break;
                case OperationType.AnnotationRemove:
                    Document.AddAnnotation(Annotation);
                    break;
                case OperationType.AnnotationDrag:
                    Annotation.MoveTo((SKRect)OldValue);
                    break;
                case OperationType.AnnotationSize:
                    Annotation.Box = (SKRect)OldValue;
                    break;
                case OperationType.AnnotationRePage:
                    var page = Document.Pages[(int)OldValue];
                    Annotation.Page = page;
                    break;
                case OperationType.AnnotationColor:
                    Annotation.SKColor = (SKColor)OldValue;
                    break;
                case OperationType.AnnotationText:
                    Annotation.Text = (string)OldValue;
                    break;
                case OperationType.AnnotationSubject:
                    Annotation.Subject = (string)OldValue;
                    break;
                case OperationType.PointMove:
                    {
                        if (Property is ControlPoint controlPoint)
                        {
                            controlPoint.Point = (SKPoint)OldValue;
                        }
                        break;
                    }
                case OperationType.PointAdd:
                    {
                        if (Property is IndexControlPoint controlPoint)
                        {
                            if (Annotation is VertexShape vertexShape)
                            {
                                vertexShape.RemovePoint(controlPoint.Index);
                            }
                        }
                    }
                    break;
                case OperationType.PointRemove:
                    {
                        if (Property is IndexControlPoint controlPoint)
                        {
                            if (Annotation is VertexShape vertexShape)
                            {
                                vertexShape.InsertPoint(controlPoint.Index, (SKPoint)NewValue);
                            }
                        }
                    }
                    break;
            }
        }
    }
}
