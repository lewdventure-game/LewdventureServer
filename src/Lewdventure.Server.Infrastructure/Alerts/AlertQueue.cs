using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertQueue : IAlertPublisher
    {
        private readonly Channel<AlertMessage> _channel;

        public AlertQueue(IOptions<AlertsOptions> options)
        {
            _channel = Channel.CreateBounded<AlertMessage>(new BoundedChannelOptions(options.Value.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public ChannelReader<AlertMessage> Reader => _channel.Reader;

        public void Publish(AlertMessage message)
        {
            _channel.Writer.TryWrite(message);
        }

        public void Complete()
        {
            _channel.Writer.TryComplete();
        }
    }
}
