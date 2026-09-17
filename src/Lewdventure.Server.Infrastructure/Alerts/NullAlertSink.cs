namespace Server.Infrastructure.Alerts
{
    internal sealed class NullAlertSink : IAlertSink
    {
        private readonly ILogger<NullAlertSink> _logger;

        public NullAlertSink(ILogger<NullAlertSink> logger)
        {
            _logger = logger;
        }

        public Task<bool> SendAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[Alert] suppressed severity = {Severity} title = {Title} description = {Description}", message.Severity, message.Title, message.Description);

            return Task.FromResult(true);
        }
    }
}
