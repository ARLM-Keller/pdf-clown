using PdfClown.Documents;
using PdfClown.Documents.Interaction.Annotations;
using PdfClown.Files;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdfClown.Viewer
{
    public class PdfDocumentView : IDisposable
    {
        public static PdfDocumentView LoadFrom(string filePath)
        {
            var document = new PdfDocumentView();
            document.Load(filePath);
            return document;
        }

        public static PdfDocumentView LoadFrom(System.IO.Stream fileStream)
        {
            var document = new PdfDocumentView();
            document.Load(fileStream);
            return document;
        }

        private readonly List<PdfPageView> pageViews = new List<PdfPageView>();
        private readonly Dictionary<int, PdfPageView> pagesIndex = new Dictionary<int, PdfPageView>();
        private readonly float indent = 10;

        public Files.File File { get; private set; }

        public Document Document => File.Document;

        public Pages Pages { get; private set; }

        public List<PdfPageView> PageViews => pageViews;

        public string FilePath { get; private set; }

        public string TempFilePath { get; set; }

        public SKSize Size { get; private set; }

        public PdfPageView this[int index]
        {
            get => pagesIndex.TryGetValue(index, out var page) ? page : null;
        }

        public event EventHandler<AnnotationEventArgs> AnnotationAdded;

        public event EventHandler<AnnotationEventArgs> AnnotationRemoved;

        public void LoadPages()
        {
            float totalWidth, totalHeight;
            Pages = Document.Pages;

            totalWidth = 0F;
            totalHeight = 0F;
            pageViews.Clear();
            pagesIndex.Clear();
            foreach (var page in Pages)
            {
                totalHeight += indent;
                var box = page.RotatedBox;
                var dpi = 1F;
                var imageSize = new SKSize(box.Width * dpi, box.Height * dpi);
                var pageView = new PdfPageView()
                {
                    Index = page.Index,
                    Page = page,
                    Size = imageSize
                };
                pageView.Matrix = pageView.Matrix.PreConcat(SKMatrix.CreateTranslation(indent, totalHeight));
                pagesIndex[pageView.Index] = pageView;
                pageViews.Add(pageView);
                if (imageSize.Width > totalWidth)
                    totalWidth = imageSize.Width;

                totalHeight += imageSize.Height;
            }
            Size = new SKSize(totalWidth + indent * 2, totalHeight);
            foreach (var pageView in pageViews)
            {
                if ((pageView.Size.Width + indent * 2) < Size.Width)
                {
                    pageView.Matrix.TransX += (Size.Width - pageView.Size.Width + indent * 2) / 2;
                }
            }
        }

        public PdfPageView GetPageView(Documents.Page page)
        {
            foreach (var pageView in pageViews)
            {
                if (pageView.Page == page)
                {
                    return pageView;
                }
            }
            return null;
        }

        private void ClearPages()
        {
            foreach (var pageView in pageViews)
            {
                pageView.Dispose();
            }
            pageViews.Clear();
            pagesIndex.Clear();
        }

        public void Dispose()
        {
            if (File != null)
            {
                ClearPages();
                File.Dispose();
                File = null;

                try { System.IO.File.Delete(TempFilePath); }
                catch { }
            }
        }

        public void Load(string filePath)
        {
            FilePath = filePath;
            TempFilePath = GetTempPath(filePath);
            System.IO.File.Copy(filePath, TempFilePath, true);
            var fileStream = new FileStream(TempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            Load(fileStream);
        }

        private static string GetTempPath(string filePath)
        {
            int? index = null;
            var tempPath = "";
            do
            {
                tempPath = $"{filePath}~{index}";
                index = (index ?? 0) + 1;
            }
            while (System.IO.File.Exists(tempPath));
            return tempPath;
        }

        public void Load(Stream stream)
        {
            if (string.IsNullOrEmpty(FilePath)
                && stream is FileStream fileStream)
            {
                FilePath = fileStream.Name;
                TempFilePath = GetTempPath(FilePath);
                System.IO.File.Copy(FilePath, TempFilePath, true);
                fileStream.Close();
                stream = new FileStream(TempFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            File = new Files.File(stream);
            LoadPages();
        }

        public void Save(SerializationModeEnum mode = SerializationModeEnum.Standard)
        {
            File.Save(FilePath, mode);
        }

        public Annotation FindAnnotation(string name, int? pageIndex = null)
        {
            var annotation = (Annotation)null;
            if (pageIndex == null)
            {
                foreach (var pageView in PageViews)
                {
                    annotation = pageView.Page.Annotations[name];
                    if (annotation != null)
                        return annotation;
                }
            }
            else
            {
                var pageView = this[(int)pageIndex];
                return pageView?.Page.Annotations[name];
            }
            return null;
        }

        public void AddAnnotation(Annotation annotation)
        {
            AddAnnotation(annotation.Page, annotation);
        }

        public void AddAnnotation(Page page, Annotation annotation)
        {
            if (page != null)
            {
                annotation.Page = page;
                if (!page.Annotations.Contains(annotation))
                {
                    page.Annotations.Add(annotation);
                    AnnotationAdded?.Invoke(this, new AnnotationEventArgs(annotation));
                }

                foreach (var item in annotation.Replies)
                {
                    if (item is Markup markup)
                    {
                        AddAnnotation(page, item);
                    }
                }
            }
            if (annotation is Popup popup
                && popup.Parent != null)
            {
                AddAnnotation(popup.Parent);
            }
        }

        public List<Annotation> RemoveAnnotation(Annotation annotation)
        {
            var list = new List<Annotation>();

            if (annotation.Page != null)
            {
                foreach (var item in annotation.Page.Annotations.ToList())
                {
                    if (item is Markup markup
                        && markup.ReplyTo == annotation)//&& markup.ReplyType == Markup.ReplyTypeEnum.Thread
                    {
                        annotation.Replies.Add(item);
                        foreach (var deleted in RemoveAnnotation(markup))
                        {
                            list.Add(deleted);
                        }
                    }
                }
            }
            annotation.Remove();
            AnnotationRemoved?.Invoke(this, new AnnotationEventArgs(annotation));
            if (annotation is Popup popup)
            {
                foreach (var deleted in RemoveAnnotation(popup.Parent))
                {
                    list.Add(deleted);
                }
            }
            list.Add(annotation);

            return list;
        }
    }
}
