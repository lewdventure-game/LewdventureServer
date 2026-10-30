namespace Server.Infrastructure.Analytics
{
    internal sealed class DialogueAnalytics
    {
        public const string DialogueCompletedEvent = "dialogue_completed";

        private readonly AnalyticsRowFactory _analyticsRowFactory;
        private readonly IAnalyticsSink _analyticsSink;

        public DialogueAnalytics(AnalyticsRowFactory analyticsRowFactory, IAnalyticsSink analyticsSink)
        {
            _analyticsRowFactory = analyticsRowFactory;
            _analyticsSink = analyticsSink;
        }

        public void TrackCompleted(string userId, string launchKey, int dialogueId, IReadOnlyList<int> picks)
        {
            _analyticsSink.TryEnqueue(_analyticsRowFactory.CreateServerEvent(DialogueCompletedEvent, userId, new Dictionary<string, object>
            {
                ["launch_key"] = launchKey,
                ["dialogue_id"] = dialogueId,
                ["picks"] = string.Join(";", picks),
            }));
        }
    }
}
