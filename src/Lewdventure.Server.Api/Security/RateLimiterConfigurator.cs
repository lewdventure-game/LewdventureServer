using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Server.Api.Options;

namespace Server.Api.Security
{
    internal sealed class RateLimiterConfigurator
    {
        private const string RejectedBody = "{\"error\":\"Too many requests.\"}";
        private const string UnknownPartition = "unknown";

        private readonly RateLimitOptions _rateLimitOptions;

        public RateLimiterConfigurator(RateLimitOptions rateLimitOptions)
        {
            _rateLimitOptions = rateLimitOptions;
        }

        public void Configure(RateLimiterOptions options)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = OnRejectedAsync;
            options.AddPolicy(SecurityNames.BattleRateLimitPolicy, CreateBattleLimiter);
            options.AddPolicy(SecurityNames.ConfigRateLimitPolicy, CreateConfigLimiter);
            options.AddPolicy(SecurityNames.AdminRateLimitPolicy, CreateAdminLimiter);
        }

        private RateLimitPartition<string> CreateBattleLimiter(HttpContext context)
        {
            return CreatePartition(context, "battle", _rateLimitOptions.Battle);
        }

        private RateLimitPartition<string> CreateConfigLimiter(HttpContext context)
        {
            return CreatePartition(context, "config", _rateLimitOptions.Config);
        }

        private RateLimitPartition<string> CreateAdminLimiter(HttpContext context)
        {
            return CreatePartition(context, "admin", _rateLimitOptions.Admin);
        }

        private RateLimitPartition<string> CreatePartition(HttpContext context, string prefix, FixedWindowLimitOptions limit)
        {
            if (_rateLimitOptions.Enabled == false)
                return RateLimitPartition.GetNoLimiter(prefix);

            var remoteAddress = context.Connection.RemoteIpAddress;
            var partitionKey = prefix + ":" + (remoteAddress == null ? UnknownPartition : remoteAddress.ToString());

            return RateLimitPartition.GetFixedWindowLimiter(partitionKey, key => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = TimeSpan.FromSeconds(limit.WindowSeconds),
                QueueLimit = limit.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
        }

        private async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var response = context.HttpContext.Response;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            response.ContentType = "application/json";

            await response.WriteAsync(RejectedBody, cancellationToken);
        }
    }
}
