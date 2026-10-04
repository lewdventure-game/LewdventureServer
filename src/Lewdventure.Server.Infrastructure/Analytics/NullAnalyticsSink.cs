namespace Server.Infrastructure.Analytics
{
    internal sealed class NullAnalyticsSink : IAnalyticsSink
    {
        public bool IsEnabled => false;

        public bool TryEnqueue(AnalyticsRow row)
        {
            return true;
        }
    }
}
