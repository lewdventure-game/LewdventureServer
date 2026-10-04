using System.Globalization;
using System.Text.Json;

namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsRowFactory
    {
        public const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly TimeProvider _timeProvider;

        public AnalyticsRowFactory(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public AnalyticsRow CreateServerEvent(string eventType, string userId, Dictionary<string, object> properties)
        {
            var now = FormatTime(_timeProvider.GetUtcNow().UtcDateTime);

            return new AnalyticsRow
            {
                InsertId = Guid.NewGuid().ToString("N"),
                EventType = eventType,
                EventTime = now,
                ServerTime = now,
                UserId = userId,
                Platform = AnalyticsRow.ServerSource,
                Source = AnalyticsRow.ServerSource,
                EventProperties = JsonSerializer.Serialize(properties, _jsonOptions),
            };
        }

        public string FormatTime(DateTime time)
        {
            return time.ToString(TimeFormat, CultureInfo.InvariantCulture);
        }
    }
}
