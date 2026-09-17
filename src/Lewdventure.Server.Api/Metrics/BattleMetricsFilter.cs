using System.Diagnostics;

namespace Server.Api.Metrics
{
    internal sealed class BattleMetricsFilter
    {
        private readonly BattleMetrics _battleMetrics;

        public BattleMetricsFilter(BattleMetrics battleMetrics)
        {
            _battleMetrics = battleMetrics;
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var result = await next(context);
            var statusCode = result is IStatusCodeHttpResult statusCodeResult && statusCodeResult.StatusCode.HasValue ? statusCodeResult.StatusCode.Value : StatusCodes.Status200OK;
            var kind = context.HttpContext.Request.Path.Value ?? string.Empty;

            _battleMetrics.Record(kind, statusCode, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return result;
        }
    }
}
