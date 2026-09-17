using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Api.Health;
using Server.Infrastructure.Alerts;
using Tests.Unit.Infrastructure.Alerts;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class HealthStateAlertPublisherTests
    {
        private RecordingAlertPublisher _alertPublisher = null!;
        private HealthStateAlertPublisher _healthPublisher = null!;

        [SetUp]
        public void SetUp()
        {
            _alertPublisher = new RecordingAlertPublisher();
            _healthPublisher = new HealthStateAlertPublisher(_alertPublisher, NullLogger<HealthStateAlertPublisher>.Instance);
        }

        [Test]
        public async Task PublishAsync_HealthyOnStart_PublishesNothing()
        {
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None);

            Assert.That(_alertPublisher.Messages, Is.Empty);
        }

        [Test]
        public async Task PublishAsync_DegradedOnStart_PublishesWarning()
        {
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None);

            Assert.That(_alertPublisher.Messages, Has.Count.EqualTo(1));
            Assert.That(_alertPublisher.Messages[0].Severity, Is.EqualTo(AlertSeverity.Warning));
        }

        [Test]
        public async Task PublishAsync_Transitions_PublishOnlyOnChange()
        {
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None);
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None);
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None);
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None);
            await _healthPublisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None);

            Assert.That(_alertPublisher.Messages, Has.Count.EqualTo(2));
            Assert.That(_alertPublisher.Messages[0].Severity, Is.EqualTo(AlertSeverity.Critical));
            Assert.That(_alertPublisher.Messages[0].DedupKey, Is.EqualTo("health:critical"));
            Assert.That(_alertPublisher.Messages[1].Severity, Is.EqualTo(AlertSeverity.Resolved));
        }

        private HealthReport CreateReport(HealthStatus status)
        {
            var entries = new Dictionary<string, HealthReportEntry>
            {
                ["mongo"] = new HealthReportEntry(status, "mongo " + status, TimeSpan.Zero, null, null),
            };

            return new HealthReport(entries, TimeSpan.Zero);
        }
    }
}
