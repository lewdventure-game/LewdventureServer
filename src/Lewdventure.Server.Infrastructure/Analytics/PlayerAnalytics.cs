using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Analytics
{
    internal sealed class PlayerAnalytics
    {
        public const string AccountCreatedEvent = "account_created";
        public const string ExperimentAssignedEvent = "experiment_assigned";

        private readonly AnalyticsRowFactory _analyticsRowFactory;
        private readonly IAnalyticsSink _analyticsSink;

        public PlayerAnalytics(AnalyticsRowFactory analyticsRowFactory, IAnalyticsSink analyticsSink)
        {
            _analyticsRowFactory = analyticsRowFactory;
            _analyticsSink = analyticsSink;
        }

        public void TrackAccountCreated(string userId, string country, string clientVersion)
        {
            var row = _analyticsRowFactory.CreateServerEvent(AccountCreatedEvent, userId, new Dictionary<string, object>
            {
                ["client_version"] = clientVersion,
            });

            row.Country = country;
            row.AppVersion = clientVersion;
            _analyticsSink.TryEnqueue(row);
        }

        public void TrackExperimentAssigned(string userId, UserExperimentDocument assignment, bool isNewPlayer)
        {
            var row = _analyticsRowFactory.CreateServerEvent(ExperimentAssignedEvent, userId, new Dictionary<string, object>
            {
                ["experiment_id"] = assignment.ExperimentId,
                ["group_id"] = assignment.GroupId,
                ["new_player"] = isNewPlayer,
            });

            row.Country = assignment.Country;
            _analyticsSink.TryEnqueue(row);
        }
    }
}
