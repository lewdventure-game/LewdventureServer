using System.Threading.Channels;
using Server.Infrastructure.Mongo.Qa;

namespace Server.Infrastructure.Qa
{
    internal sealed class QaDiagnosticsQueue
    {
        private const int Capacity = 4096;

        private readonly Channel<RequestTraceDocument> _traces = Channel.CreateBounded<RequestTraceDocument>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

        private readonly Channel<ServerErrorDocument> _errors = Channel.CreateBounded<ServerErrorDocument>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

        private long _dropped;

        public ChannelReader<RequestTraceDocument> Traces => _traces.Reader;

        public ChannelReader<ServerErrorDocument> Errors => _errors.Reader;

        public void Enqueue(RequestTraceDocument trace)
        {
            if (_traces.Writer.TryWrite(trace) == false)
                Interlocked.Increment(ref _dropped);
        }

        public void Enqueue(ServerErrorDocument error)
        {
            if (_errors.Writer.TryWrite(error) == false)
                Interlocked.Increment(ref _dropped);
        }

        public long TakeDropped()
        {
            return Interlocked.Exchange(ref _dropped, 0);
        }
    }
}
