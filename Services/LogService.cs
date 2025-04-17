using Microsoft.Extensions.Logging;
using PdfFieldExtractor.Services;

namespace PdfFieldExtractor.Services
{
    public class LogService : ILogService
    {
        private readonly ILogger<LogService> _logger;

        public LogService(ILogger<LogService> logger)
        {
            _logger = logger;
        }

        public void Info(string message)
        {
            _logger.LogInformation(message);
        }

        public void Warn(string message)
        {
            _logger.LogWarning(message);
        }

        public void Error(string message)
        {
            _logger.LogError(message);
        }

        public void Error(string message, Exception ex)
        {
            _logger.LogError(ex, message);
        }
    }
}
