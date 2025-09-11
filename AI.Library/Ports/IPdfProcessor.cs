using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI.Library.Ports
{
    public interface IPdfProcessor
    {
        Task<List<(byte[] ImageData, int PageNumber)>> ConvertPdfPagesToImagesAsync(byte[] pdfData);
        Task<string> ExtractTextAsync(byte[] pdfData);
        Task<bool> IsPdfImageBasedAsync(byte[] pdfData);
    }
}
