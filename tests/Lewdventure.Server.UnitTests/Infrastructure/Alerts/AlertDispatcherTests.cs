using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Server.Infrastructure.Alerts;

namespace Tests.Unit.Infrastructure.Alerts
{
    [TestFixture]
    public sealed class AlertDispatcherTests
    {
        private AlertQueue _queue = null!;
        private RecordingAlertSink _sink = null!;
        private AlertDispatcher _dispatcher = null!;

        [SetUp]
        public void SetUp()
        {
            var options = Microsoft.Extensions.Options.Options.Create(new AlertsOptions { CooldownSeconds = 300, QueueCapacity = 8 });

            _queue = new AlertQueue(options);
            _sink = new RecordingAlertSink();
            _dispatcher = new AlertDispatcher(_queue, new AlertThrottle(options, new FakeTimeProvider()), _sink, NullLogger<AlertDispatcher>.Instance);
        }

        [Test]
        public async Task DrainAsync_DuplicateKeys_SendsOnce()
        {
            _queue.Publish(new AlertMessage(AlertSeverity.Critical, "boom", string.Empty, "exception:X:/"));
            _queue.Publish(new AlertMessage(AlertSeverity.Critical, "boom", string.Empty, "exception:X:/"));
            _queue.Publish(new AlertMessage(AlertSeverity.Info, "started", string.Empty, string.Empty));

            await _dispatcher.DrainAsync(CancellationToken.None);

            Assert.That(_sink.Messages, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task DrainAsync_SinkThrows_DoesNotPropagate()
        {
            _sink.ThrowOnSend = true;
            _queue.Publish(new AlertMessage(AlertSeverity.Critical, "boom", string.Empty, string.Empty));

            await _dispatcher.DrainAsync(CancellationToken.None);

            Assert.That(_sink.Messages, Is.Empty);
        }

        [Test]
        public void Publish_OverCapacity_DropsOldest()
        {
            for (int i = 0; i < 10; i++)
                _queue.Publish(new AlertMessage(AlertSeverity.Info, "message " + i, string.Empty, string.Empty));

            Assert.That(_queue.Reader.TryRead(out var first), Is.True);
            Assert.That(first!.Title, Is.EqualTo("message 2"));
        }

        [Test]
        public async Task RunAsync_CompletedQueue_Finishes()
        {
            _queue.Publish(new AlertMessage(AlertSeverity.Info, "started", string.Empty, string.Empty));
            _queue.Complete();

            await _dispatcher.RunAsync(CancellationToken.None);

            Assert.That(_sink.Messages, Has.Count.EqualTo(1));
        }
    }
}
