using System.Text.Json;
using Microsoft.Extensions.Options;
using Server.Infrastructure.Analytics;

namespace Server.Api.Endpoints
{
    internal sealed class AnalyticsEventConverter
    {
        public const int MaxEventTypeLength = 64;
        public const int MaxTextLength = 64;

        private const string EmptyObject = "{}";
        private readonly TimeSpan _maxPast = TimeSpan.FromDays(30);
        private readonly TimeSpan _maxFuture = TimeSpan.FromHours(1);

        private readonly AnalyticsRowFactory _analyticsRowFactory;
        private readonly AnalyticsOptions _options;

        public AnalyticsEventConverter(AnalyticsRowFactory analyticsRowFactory, IOptions<AnalyticsOptions> options)
        {
            _analyticsRowFactory = analyticsRowFactory;
            _options = options.Value;
        }

        public bool TryConvert(AnalyticsEventRequest request, string userId, string country, DateTime now, out AnalyticsRow? row, out string error)
        {
            row = null;

            if (IsValidEventType(request.EventType) == false)
            {
                error = $"event_type '{Shorten(request.EventType)}' must match [a-z][a-z0-9_.]{{0,63}}.";

                return false;
            }

            if (TryReadObject(request.EventProperties, out var eventProperties) == false || TryReadObject(request.UserProperties, out var userProperties) == false)
            {
                error = $"{request.EventType}: event_properties and user_properties must be JSON objects up to {_options.MaxPropertiesBytes} bytes.";

                return false;
            }

            row = new AnalyticsRow
            {
                InsertId = string.IsNullOrWhiteSpace(request.InsertId) ? Guid.NewGuid().ToString("N") : Shorten(request.InsertId.Trim()),
                EventType = request.EventType,
                EventTime = _analyticsRowFactory.FormatTime(ReadTime(request.Time, now)),
                ServerTime = _analyticsRowFactory.FormatTime(now),
                UserId = userId,
                DeviceId = Shorten(request.DeviceId ?? string.Empty),
                SessionId = request.SessionId,
                Platform = Shorten(request.Platform ?? string.Empty),
                AppVersion = Shorten(request.AppVersion ?? string.Empty),
                Country = country,
                Source = AnalyticsRow.ClientSource,
                EventProperties = eventProperties,
                UserProperties = userProperties,
            };
            error = string.Empty;

            return true;
        }

        private bool IsValidEventType(string? eventType)
        {
            if (string.IsNullOrEmpty(eventType) || MaxEventTypeLength < eventType.Length || char.IsAsciiLetterLower(eventType[0]) == false)
                return false;

            for (int i = 1; i < eventType.Length; i++)
            {
                var symbol = eventType[i];
                var isAllowed = char.IsAsciiLetterLower(symbol) || char.IsAsciiDigit(symbol) || symbol == '_' || symbol == '.';

                if (isAllowed == false)
                    return false;
            }

            return true;
        }

        private bool TryReadObject(JsonElement? element, out string json)
        {
            json = EmptyObject;

            if (element == null || element.Value.ValueKind == JsonValueKind.Null || element.Value.ValueKind == JsonValueKind.Undefined)
                return true;

            if (element.Value.ValueKind != JsonValueKind.Object)
                return false;

            json = element.Value.GetRawText();

            return json.Length <= _options.MaxPropertiesBytes;
        }

        private DateTime ReadTime(long unixMilliseconds, DateTime now)
        {
            if (unixMilliseconds <= 0)
                return now;

            var time = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;

            if (time < now - _maxPast || now + _maxFuture < time)
                return now;

            return time;
        }

        private string Shorten(string value)
        {
            return value.Length <= MaxTextLength ? value : value.Substring(0, MaxTextLength);
        }
    }
}
