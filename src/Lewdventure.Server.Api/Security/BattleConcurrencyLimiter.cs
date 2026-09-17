using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Server.Api.Options;

namespace Server.Api.Security
{
    internal sealed class BattleConcurrencyLimiter : IDisposable
    {
        private const string RejectedBody = "{\"error\":\"Server is busy.\"}";

        private readonly ConcurrencyLimiter? _limiter;

        public BattleConcurrencyLimiter(IOptions<RateLimitOptions> rateLimitOptions)
        {
            var options = rateLimitOptions.Value;

            if (options.Enabled == false)
                return;

            var permitLimit = options.BattleConcurrencyLimit <= 0 ? Environment.ProcessorCount * 2 : options.BattleConcurrencyLimit;

            _limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = permitLimit,
                QueueLimit = permitLimit * 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            if (_limiter == null)
                return await next(context);

            using var lease = await _limiter.AcquireAsync(1, context.HttpContext.RequestAborted);

            if (lease.IsAcquired == false)
                return Results.Content(RejectedBody, "application/json", null, StatusCodes.Status503ServiceUnavailable);

            return await next(context);
        }

        public void Dispose()
        {
            _limiter?.Dispose();
        }
    }
}
