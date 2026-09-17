namespace Server.Infrastructure.Alerts
{
    internal interface IAlertSink
    {
        public Task<bool> SendAsync(AlertMessage message, CancellationToken cancellationToken);
    }
}
