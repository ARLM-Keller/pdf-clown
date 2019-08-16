using System.IO;
using System.Threading.Tasks;

namespace PDFClown.Viewer.Test
{
    public interface IOpenFileService
    {
        Task<(Stream Stream, string FileName)> OpenFileDialog();
    }
}
