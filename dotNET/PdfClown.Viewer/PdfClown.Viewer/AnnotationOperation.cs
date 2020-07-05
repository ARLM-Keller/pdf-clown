using PdfClown.Documents.Interaction.Annotations;
using SkiaSharp;
using System;

namespace PdfClown.Viewer
{

    public class AnnotationOperation : EditorOperation
    {
        private Annotation annotation;

        public Annotation Annotation
        {
            get => annotation;
            set
            {
                annotation = value;
                if (annotation.Page != null)
                {
                    PageIndex = annotation.Page.Index;
                }
            }
        }

        public override EditorOperation Clone(PdfDocumentView document)
        {
            var cloned = (AnnotationOperation)base.Clone(document);

            cloned.Annotation = document.FindAnnotation(Annotation.Name)
                ?? (Annotation)Annotation.Clone(document.Document);

            if (cloned.Property is ControlPoint controlPoint)
                cloned.Property = controlPoint.Clone(cloned.Annotation);

            return cloned;
        }

        public override void EndOperation()
        {
            if (Type == OperationType.AnnotationDrag
                || Type == OperationType.AnnotationSize)
            {
                NewValue = Annotation.Box;
            }
            if (Property is ControlPoint controlPoint)
            {
                NewValue = controlPoint.Point;
            }
        }

        public override void Redo()
        {
            switch (Type)
            {
                case OperationType.AnnotationAdd:
                    Document.AddAnnotation(Annotation.Page ?? Document[PageIndex]?.Page, Annotation);
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
                    Annotation.Page = Document[(int)NewValue]?.Page;
                    break;
                case OperationType.AnnotationColor:
                    Annotation.SKColor = (SKColor)NewValue;
                    break;
                case OperationType.AnnotationText:
                    Annotation.Contents = (string)NewValue;
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

        public override void Undo()
        {
            switch (Type)
            {
                case OperationType.AnnotationAdd:
                    Document.RemoveAnnotation(Annotation);
                    break;
                case OperationType.AnnotationRemove:
                    Document.AddAnnotation(Annotation.Page ?? Document[PageIndex]?.Page, Annotation);
                    break;
                case OperationType.AnnotationDrag:
                    Annotation.MoveTo((SKRect)OldValue);
                    break;
                case OperationType.AnnotationSize:
                    Annotation.Box = (SKRect)OldValue;
                    break;
                case OperationType.AnnotationRePage:
                    Annotation.Page = Document.Pages[(int)OldValue];
                    break;
                case OperationType.AnnotationColor:
                    Annotation.SKColor = (SKColor)OldValue;
                    break;
                case OperationType.AnnotationText:
                    Annotation.Contents = (string)OldValue;
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
