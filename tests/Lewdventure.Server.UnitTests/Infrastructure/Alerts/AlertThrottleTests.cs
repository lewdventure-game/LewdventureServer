using Microsoft.Extensions.Time.Testing;
using Server.Infrastructure.Alerts;

namespace Tests.Unit.Infrastructure.Alerts
{
    [TestFixture]
    public sealed class AlertThrottleTests
    {
        private FakeTimeProvider _timeProvider = null!;
        private AlertThrottle _throttle = null!;

        [SetUp]
        public void SetUp()
        {
            _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            _throttle = new AlertThrottle(Microsoft.Extensions.Options.Options.Create(new AlertsOptions { CooldownSeconds = 300 }), _timeProvider);
        }

        [Test]
        public void TryAcquire_SameKeyWithinCooldown_Blocks()
        {
            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "health:critical")), Is.True);

            _timeProvider.Advance(TimeSpan.FromSeconds(299));

            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "health:critical")), Is.False);
        }

        [Test]
        public void TryAcquire_SameKeyAfterCooldown_Allows()
        {
            _throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "health:critical"));

            _timeProvider.Advance(TimeSpan.FromSeconds(300));

            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "health:critical")), Is.True);
        }

        [Test]
        public void TryAcquire_DifferentKeys_AreIndependent()
        {
            _throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "exception:A:/api/battle/replay"));

            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Critical, "exception:B:/api/battle/replay")), Is.True);
        }

        [Test]
        public void TryAcquire_ResolvedOrEmptyKey_NeverBlocks()
        {
            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Resolved, "health:ok")), Is.True);
            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Resolved, "health:ok")), Is.True);
            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Info, string.Empty)), Is.True);
            Assert.That(_throttle.TryAcquire(CreateMessage(AlertSeverity.Info, string.Empty)), Is.True);
        }

        private AlertMessage CreateMessage(AlertSeverity severity, string dedupKey)
        {
            return new AlertMessage(severity, "title", "description", dedupKey);
        }
    }
}
