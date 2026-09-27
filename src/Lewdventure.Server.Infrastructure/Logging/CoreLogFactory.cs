using Server.Logging;

namespace Server.Infrastructure.Logging
{
    internal sealed class CoreLogFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public CoreLogFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ICoreLog Create(string category)
        {
            return new CoreLog(_loggerFactory.CreateLogger(category));
        }
    }
}
