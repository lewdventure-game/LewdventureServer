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
            options.AddPolicy(SecurityNames.AuthRateLimitPolicy, CreateAuthLimiter);
            options.AddPolicy(SecurityNames.PlayerRateLimitPolicy, CreatePlayerLimiter);
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

        private RateLimitPartition<string> CreateAuthLimiter(HttpContext context)
        {
            return CreatePartition(context, "auth", _rateLimitOptions.Auth);
        }

        private RateLimitPartition<string> CreatePlayerLimiter(HttpContext context)
        {
            return CreatePartition(context, "player", _rateLimitOptions.Player, ReadPlayerPartition(context));
        }

        private string ReadPlayerPartition(HttpContext context)
        {
            var claim = context.User.FindFirst("sub");

            return claim == null ? string.Empty : claim.Value;
        }

        private RateLimitPartition<string> CreatePartition(HttpContext context, string prefix, FixedWindowLimitOptions limit)
        {
            return CreatePartition(context, prefix, limit, string.Empty);
        }

        private RateLimitPartition<string> CreatePartition(HttpContext context, string prefix, FixedWindowLimitOptions limit, string identity)
        {
            if (_rateLimitOptions.Enabled == false)
                return RateLimitPartition.GetNoLimiter(prefix);

            var remoteAddress = context.Connection.RemoteIpAddress;
            var fallback = remoteAddress == null ? UnknownPartition : remoteAddress.ToString();
            var partitionKey = prefix + ":" + (string.IsNullOrEmpty(identity) ? fallback : identity);

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
