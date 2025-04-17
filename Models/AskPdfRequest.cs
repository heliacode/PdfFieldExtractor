using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace PdfFieldExtractor.Models
{
    public class AskPdfRequest
    {
        [FromForm]
        public IFormFile? File { get; set; }

        [FromForm]
        public string? Query { get; set; }
    }
}
