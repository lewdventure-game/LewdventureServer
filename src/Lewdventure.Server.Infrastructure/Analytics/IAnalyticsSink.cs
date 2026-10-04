namespace Server.Infrastructure.Analytics
{
    internal interface IAnalyticsSink
    {
        public bool IsEnabled { get; }

        public bool TryEnqueue(AnalyticsRow row);
    }
}
