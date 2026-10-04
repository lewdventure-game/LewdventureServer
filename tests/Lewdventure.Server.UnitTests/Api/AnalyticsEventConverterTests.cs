using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Server.Api.Endpoints;
using Server.Infrastructure.Analytics;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class AnalyticsEventConverterTests
    {
        private readonly DateTime _now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        private AnalyticsEventConverter _converter = null!;

        [SetUp]
        public void SetUp()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions { MaxPropertiesBytes = 64 });
            var factory = new AnalyticsRowFactory(new FakeTimeProvider(new DateTimeOffset(_now)));

            _converter = new AnalyticsEventConverter(factory, options);
        }

        [Test]
        public void TryConvert_ValidEvent_FillsRowFromRequestAndServer()
        {
            var request = new AnalyticsEventRequest
            {
                EventType = "battle_complete",
                Time = new DateTimeOffset(_now.AddMinutes(-5)).ToUnixTimeMilliseconds(),
                EventProperties = Parse("{\"stage_id\":4,\"win\":true}"),
                SessionId = 1759570000000,
                InsertId = "abc",
                Platform = "android",
                AppVersion = "0.4.1",
            };

            var converted = _converter.TryConvert(request, "usr_1", "US", _now, out var row, out var error);

            Assert.That(converted, Is.True, error);
            Assert.That(row!.UserId, Is.EqualTo("usr_1"));
            Assert.That(row.Country, Is.EqualTo("US"));
            Assert.That(row.EventTime, Is.EqualTo("2026-10-04 11:55:00.000"));
            Assert.That(row.ServerTime, Is.EqualTo("2026-10-04 12:00:00.000"));
            Assert.That(row.EventProperties, Is.EqualTo("{\"stage_id\":4,\"win\":true}"));
            Assert.That(row.UserProperties, Is.EqualTo("{}"));
            Assert.That(row.Source, Is.EqualTo(AnalyticsRow.ClientSource));
            Assert.That(row.InsertId, Is.EqualTo("abc"));
        }

        [TestCase("")]
        [TestCase("Battle")]
        [TestCase("1battle")]
        [TestCase("battle complete")]
        public void TryConvert_InvalidEventType_IsRejected(string eventType)
        {
            var converted = _converter.TryConvert(new AnalyticsEventRequest { EventType = eventType }, "usr_1", "US", _now, out _, out var error);

            Assert.That(converted, Is.False);
            Assert.That(error, Does.Contain("event_type"));
        }

        [Test]
        public void TryConvert_PropertiesNotObjectOrTooLarge_AreRejected()
        {
            var array = new AnalyticsEventRequest { EventType = "tutorial_step", EventProperties = Parse("[1,2]") };
            var large = new AnalyticsEventRequest { EventType = "tutorial_step", UserProperties = Parse("{\"text\":\"" + new string('x', 100) + "\"}") };

            Assert.That(_converter.TryConvert(array, "usr_1", "US", _now, out _, out _), Is.False);
            Assert.That(_converter.TryConvert(large, "usr_1", "US", _now, out _, out _), Is.False);
        }

        [Test]
        public void TryConvert_TimeOutOfRangeOrMissing_UsesServerTimeAndGeneratesInsertId()
        {
            var future = new AnalyticsEventRequest { EventType = "start_game", Time = new DateTimeOffset(_now.AddDays(2)).ToUnixTimeMilliseconds() };
            var missing = new AnalyticsEventRequest { EventType = "start_game" };

            _converter.TryConvert(future, "usr_1", "US", _now, out var futureRow, out _);
            _converter.TryConvert(missing, "usr_1", "US", _now, out var missingRow, out _);

            Assert.That(futureRow!.EventTime, Is.EqualTo("2026-10-04 12:00:00.000"));
            Assert.That(missingRow!.EventTime, Is.EqualTo("2026-10-04 12:00:00.000"));
            Assert.That(missingRow.InsertId, Has.Length.EqualTo(32));
        }

        private JsonElement Parse(string json)
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }
    }
}
