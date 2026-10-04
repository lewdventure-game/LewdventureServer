using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsQueue : IAnalyticsSink
    {
        private readonly Channel<AnalyticsRow> _channel;

        private long _dropped;

        public AnalyticsQueue(IOptions<AnalyticsOptions> options)
        {
            _channel = Channel.CreateBounded<AnalyticsRow>(new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public bool IsEnabled => true;

        public ChannelReader<AnalyticsRow> Reader => _channel.Reader;

        public long TakeDropped()
        {
            return Interlocked.Exchange(ref _dropped, 0);
        }

        public bool TryEnqueue(AnalyticsRow row)
        {
            if (_channel.Writer.TryWrite(row))
                return true;

            Interlocked.Increment(ref _dropped);

            return false;
        }

        public void Complete()
        {
            _channel.Writer.TryComplete();
        }
    }
}
