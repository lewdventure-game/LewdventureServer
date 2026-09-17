namespace Server.Infrastructure.Alerts
{
    internal interface IAlertPublisher
    {
        public void Publish(AlertMessage message);
    }
}
