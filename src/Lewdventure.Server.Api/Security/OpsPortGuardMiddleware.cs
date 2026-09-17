using Microsoft.Extensions.Options;
using Server.Api.Options;

namespace Server.Api.Security
{
    internal sealed class OpsPortGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ServerOptions _serverOptions;

        public OpsPortGuardMiddleware(RequestDelegate next, IOptions<ServerOptions> serverOptions)
        {
            _next = next;
            _serverOptions = serverOptions.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsAllowed(context) == false)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            await _next(context);
        }

        private bool IsAllowed(HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            var isOpsEndpoint = endpoint != null && endpoint.Metadata.GetMetadata<OpsPortOnlyMetadata>() != null;
            var isOpsPort = context.Connection.LocalPort == _serverOptions.OpsPort;

            return isOpsEndpoint == isOpsPort;
        }
    }
}
