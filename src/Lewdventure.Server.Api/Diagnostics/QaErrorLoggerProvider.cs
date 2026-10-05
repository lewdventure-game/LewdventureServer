using Server.Api.Security;
using Server.Infrastructure.Qa;

namespace Server.Api.Diagnostics
{
    internal sealed class QaErrorLoggerProvider : ILoggerProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PlayerIdentityReader _playerIdentityReader;
        private readonly QaDiagnosticsQueue _qaDiagnosticsQueue;
        private readonly TimeProvider _timeProvider;

        public QaErrorLoggerProvider(
            IHttpContextAccessor httpContextAccessor,
            PlayerIdentityReader playerIdentityReader,
            QaDiagnosticsQueue qaDiagnosticsQueue,
            TimeProvider timeProvider)
        {
            _httpContextAccessor = httpContextAccessor;
            _playerIdentityReader = playerIdentityReader;
            _qaDiagnosticsQueue = qaDiagnosticsQueue;
            _timeProvider = timeProvider;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new QaErrorLogger(categoryName, _httpContextAccessor, _playerIdentityReader, _qaDiagnosticsQueue, _timeProvider);
        }

        public void Dispose()
        {
        }
    }
}
