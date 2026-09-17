namespace Server.Infrastructure.Alerts
{
    internal sealed class NullAlertPublisher : IAlertPublisher
    {
        public void Publish(AlertMessage message)
        {
        }
    }
}
