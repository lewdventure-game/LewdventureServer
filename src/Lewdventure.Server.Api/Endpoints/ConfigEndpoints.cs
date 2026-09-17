using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class ConfigEndpoints
    {
        public void Map(WebApplication application)
        {
            var publisherOptions = application.Services.GetRequiredService<IOptions<ConfigPublisherOptions>>().Value;

            if (publisherOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.UpdateConfig, UpdateConfigsAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);
        }

        private async Task<IResult> UpdateConfigsAsync(
            [FromServices] IGameConfigService configService,
            IHostEnvironment environment)
        {
            var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());

            return success
                ? Results.Ok(new { status = "success", message })
                : Results.Problem(detail: message, statusCode: 500);
        }
    }
}
