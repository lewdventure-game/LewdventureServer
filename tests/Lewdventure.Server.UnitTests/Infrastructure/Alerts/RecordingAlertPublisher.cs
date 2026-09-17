using Server.Infrastructure.Alerts;

namespace Tests.Unit.Infrastructure.Alerts
{
    internal sealed class RecordingAlertPublisher : IAlertPublisher
    {
        public List<AlertMessage> Messages { get; } = new();

        public void Publish(AlertMessage message)
        {
            Messages.Add(message);
        }
    }
}
