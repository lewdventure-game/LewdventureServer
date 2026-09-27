using Server.Logging;

namespace Server.Infrastructure.Logging
{
    internal sealed class CoreLog : ICoreLog
    {
        private const string MessageTemplate = "{Message}";

        private readonly ILogger _logger;

        public CoreLog(ILogger logger)
        {
            _logger = logger;
        }

        public void Debug(string message)
        {
            _logger.LogDebug(MessageTemplate, message);
        }

        public void Information(string message)
        {
            _logger.LogInformation(MessageTemplate, message);
        }

        public void Warning(string message)
        {
            _logger.LogWarning(MessageTemplate, message);
        }

        public void Warning(Exception exception, string message)
        {
            _logger.LogWarning(exception, MessageTemplate, message);
        }

        public void Error(string message)
        {
            _logger.LogError(MessageTemplate, message);
        }

        public void Error(Exception exception, string message)
        {
            _logger.LogError(exception, MessageTemplate, message);
        }
    }
}
