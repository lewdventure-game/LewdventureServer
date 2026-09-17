using Microsoft.AspNetCore.Mvc;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class ConfigEndpoints
    {
        private const string SecretHeaderName = "X-Config-Secret";
        private const string LegacySecret = "1";

        public void Map(WebApplication application)
        {
            application.MapPost(ApiRoutes.UpdateConfig, UpdateConfigsAsync);
        }

        private async Task<IResult> UpdateConfigsAsync(
            [FromHeader(Name = SecretHeaderName)] string? secretKey,
            [FromServices] IGameConfigService configService,
            IHostEnvironment environment)
        {
            if (secretKey != LegacySecret)
                return Results.Unauthorized();

            var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());

            return success
                ? Results.Ok(new { status = "success", message })
                : Results.Problem(detail: message, statusCode: 500);
        }
    }
}
