using System.Diagnostics;

namespace Server.Api.Http
{
    internal sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";

        private const int MaxLength = 64;

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = ResolveCorrelationId(context);

            context.TraceIdentifier = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
                await _next(context);
        }

        private string ResolveCorrelationId(HttpContext context)
        {
            var incoming = context.Request.Headers[HeaderName].ToString();

            if (IsValid(incoming))
                return incoming;

            var activity = Activity.Current;

            if (activity != null)
                return activity.TraceId.ToHexString();

            return Guid.NewGuid().ToString("N");
        }

        private bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || MaxLength < value.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                var symbol = value[i];

                if (char.IsAsciiLetterOrDigit(symbol) == false && symbol != '-')
                    return false;
            }

            return true;
        }
    }
}
