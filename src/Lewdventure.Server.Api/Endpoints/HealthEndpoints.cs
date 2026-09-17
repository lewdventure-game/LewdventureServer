using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Server.Api.Health;
using Server.Api.Options;

namespace Server.Api.Endpoints
{
    internal sealed class HealthEndpoints
    {
        private readonly ServerOptions _serverOptions;
        private readonly HealthResponseWriter _healthResponseWriter = new();

        public HealthEndpoints(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
        }

        public void Map(WebApplication application)
        {
            var opsHost = $"*:{_serverOptions.OpsPort}";

            application.MapHealthChecks(ApiRoutes.HealthLive, CreateOptions(IsLive)).RequireHost(opsHost);
            application.MapHealthChecks(ApiRoutes.HealthReady, CreateOptions(IsReady)).RequireHost(opsHost);
            application.MapHealthChecks(ApiRoutes.Health, CreateOptions(IsAny)).RequireHost(opsHost);
        }

        private HealthCheckOptions CreateOptions(Func<HealthCheckRegistration, bool> predicate)
        {
            return new HealthCheckOptions
            {
                Predicate = predicate,
                ResponseWriter = _healthResponseWriter.WriteAsync,
                AllowCachingResponses = false,
            };
        }

        private bool IsLive(HealthCheckRegistration registration)
        {
            return registration.Tags.Contains(HealthTags.Live);
        }

        private bool IsReady(HealthCheckRegistration registration)
        {
            return registration.Tags.Contains(HealthTags.Ready);
        }

        private bool IsAny(HealthCheckRegistration registration)
        {
            return true;
        }
    }
}
