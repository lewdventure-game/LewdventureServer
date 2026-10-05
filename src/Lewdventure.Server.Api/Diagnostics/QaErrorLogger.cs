using Server.Api.Security;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Qa;

namespace Server.Api.Diagnostics
{
    internal sealed class QaErrorLogger : ILogger
    {
        private const string UserIdProperty = "UserId";
        private const string DiagnosticsCategoryPrefix = "Server.Api.Diagnostics";
        private const int MaxMessageLength = 4000;
        private const int MaxExceptionLength = 8000;

        private readonly string _categoryName;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly PlayerIdentityReader _playerIdentityReader;
        private readonly QaDiagnosticsQueue _qaDiagnosticsQueue;
        private readonly TimeProvider _timeProvider;
        private readonly bool _isIgnored;

        public QaErrorLogger(
            string categoryName,
            IHttpContextAccessor httpContextAccessor,
            PlayerIdentityReader playerIdentityReader,
            QaDiagnosticsQueue qaDiagnosticsQueue,
            TimeProvider timeProvider)
        {
            _categoryName = categoryName;
            _httpContextAccessor = httpContextAccessor;
            _playerIdentityReader = playerIdentityReader;
            _qaDiagnosticsQueue = qaDiagnosticsQueue;
            _timeProvider = timeProvider;
            _isIgnored = categoryName.StartsWith(DiagnosticsCategoryPrefix, StringComparison.Ordinal);
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _isIgnored == false && LogLevel.Warning <= logLevel && logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) == false)
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var httpContext = _httpContextAccessor.HttpContext;
            var userId = ReadUserId(state);

            if (userId.Length == 0 && httpContext != null)
                userId = _playerIdentityReader.Read(httpContext.User);

            _qaDiagnosticsQueue.Enqueue(new ServerErrorDocument
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedAt = now,
                UpdatedAt = now,
                Level = logLevel.ToString(),
                Category = _categoryName,
                Message = Truncate(formatter(state, exception), MaxMessageLength),
                Exception = exception == null ? string.Empty : Truncate(exception.ToString(), MaxExceptionLength),
                CorrelationId = httpContext == null ? string.Empty : httpContext.TraceIdentifier,
                UserId = userId,
                Path = httpContext == null ? string.Empty : httpContext.Request.Path.ToString(),
            });
        }

        private string ReadUserId<TState>(TState state)
        {
            if (state is not IReadOnlyList<KeyValuePair<string, object?>> properties)
                return string.Empty;

            for (int i = 0; i < properties.Count; i++)
            {
                if (string.Equals(properties[i].Key, UserIdProperty, StringComparison.Ordinal) && properties[i].Value is string value)
                    return value;
            }

            return string.Empty;
        }

        private string Truncate(string value, int maxLength)
        {
            return maxLength < value.Length ? value.Substring(0, maxLength) : value;
        }
    }
}
