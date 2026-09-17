using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Server.Infrastructure.Alerts;

namespace Tests.Unit.Infrastructure.Alerts
{
    [TestFixture]
    public sealed class DiscordWebhookAlertSinkTests
    {
        private StubHttpMessageHandler _handler = null!;
        private DiscordWebhookAlertSink _sink = null!;

        [SetUp]
        public void SetUp()
        {
            var options = new AlertsOptions
            {
                Enabled = true,
                DiscordWebhookUrl = "https://discord.example/api/webhooks/1/token",
                EnvironmentLabel = "stage",
                HttpTimeoutSeconds = 5,
            };

            _handler = new StubHttpMessageHandler();
            _sink = new DiscordWebhookAlertSink(new HttpClient(_handler), NullLogger<DiscordWebhookAlertSink>.Instance, Microsoft.Extensions.Options.Options.Create(options));
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
        }

        [Test]
        public void CreatePayload_ContainsEnvironmentSeverityAndFields()
        {
            var message = new AlertMessage(AlertSeverity.Critical, "Unhandled exception", "details", "key");

            message.Fields.Add(new KeyValuePair<string, string>("path", "/api/battle/replay"));
            message.Fields.Add(new KeyValuePair<string, string>("empty", string.Empty));

            var embed = JObject.Parse(_sink.CreatePayload(message))["embeds"]![0]!;
            var fields = (JArray)embed["fields"]!;

            Assert.That((string?)embed["title"], Is.EqualTo("[Critical] Unhandled exception"));
            Assert.That((int)embed["color"]!, Is.EqualTo(0xE74C3C));
            Assert.That((string?)fields[0]["value"], Is.EqualTo("stage"));
            Assert.That((string?)fields[1]["value"], Is.EqualTo("/api/battle/replay"));
            Assert.That((string?)fields[2]["value"], Is.EqualTo("-"));
        }

        [Test]
        public void CreatePayload_LongValues_AreTruncatedToDiscordLimits()
        {
            var message = new AlertMessage(AlertSeverity.Warning, new string('t', 500), new string('d', 5000), "key");

            for (int i = 0; i < 30; i++)
                message.Fields.Add(new KeyValuePair<string, string>("field" + i, new string('v', 2000)));

            var embed = JObject.Parse(_sink.CreatePayload(message))["embeds"]![0]!;
            var fields = (JArray)embed["fields"]!;

            Assert.That(((string)embed["title"]!).Length, Is.EqualTo(256));
            Assert.That(((string)embed["description"]!).Length, Is.EqualTo(4000));
            Assert.That(fields, Has.Count.EqualTo(21));
            Assert.That(((string)fields[1]["value"]!).Length, Is.EqualTo(1000));
        }

        [Test]
        public async Task SendAsync_TooManyRequestsThenOk_Retries()
        {
            var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);

            _handler.Enqueue(throttled);
            _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.NoContent));

            var sent = await _sink.SendAsync(new AlertMessage(AlertSeverity.Info, "Server started", string.Empty, string.Empty), CancellationToken.None);

            Assert.That(sent, Is.True);
            Assert.That(_handler.Bodies, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task SendAsync_ServerError_ReturnsFalse()
        {
            _handler.Enqueue(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var sent = await _sink.SendAsync(new AlertMessage(AlertSeverity.Info, "Server started", string.Empty, string.Empty), CancellationToken.None);

            Assert.That(sent, Is.False);
            Assert.That(_handler.Bodies, Has.Count.EqualTo(1));
        }
    }
}
