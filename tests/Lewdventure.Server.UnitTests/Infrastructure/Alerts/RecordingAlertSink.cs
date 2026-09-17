using Server.Infrastructure.Alerts;

namespace Tests.Unit.Infrastructure.Alerts
{
    internal sealed class RecordingAlertSink : IAlertSink
    {
        public List<AlertMessage> Messages { get; } = new();

        public bool ThrowOnSend { get; set; }

        public Task<bool> SendAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("sink failure");

            Messages.Add(message);

            return Task.FromResult(true);
        }
    }
}
