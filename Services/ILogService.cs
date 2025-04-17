// File: Services/ILogService.cs
namespace PdfFieldExtractor.Services
{
    public interface ILogService
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(string message, Exception ex);
    }
}
