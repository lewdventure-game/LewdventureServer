using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;

namespace Server.Api.Endpoints
{
    internal sealed class AuthEndpoints
    {
        private const int MaxDeviceIdLength = 128;
        private const int MaxClientVersionLength = 64;
        private const string StorageRequiredMessage = "Player accounts require Mongo storage.";

        private bool _isMongoEnabled;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

            _isMongoEnabled = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled;

            if (authOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.AuthDevice, AuthenticateDeviceAsync)
                .RequireRateLimiting(SecurityNames.AuthRateLimitPolicy)
                .Accepts<DeviceAuthRequest>("application/json")
                .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.AuthRefresh, RefreshAsync)
                .RequireRateLimiting(SecurityNames.AuthRateLimitPolicy)
                .Accepts<RefreshAuthRequest>("application/json")
                .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);
        }

        private async Task<IResult> AuthenticateDeviceAsync(
            HttpContext httpContext,
            [FromBody] DeviceAuthRequest? request,
            [FromServices] AccessTokenIssuer accessTokenIssuer,
            [FromServices] ClientCountryReader clientCountryReader)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DeviceId))
                return Results.BadRequest(new { error = "deviceId is required." });

            if (MaxDeviceIdLength < request.DeviceId.Length || MaxClientVersionLength < request.ClientVersion.Length)
                return Results.BadRequest(new { error = "deviceId or clientVersion is too long." });

            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var authService = httpContext.RequestServices.GetRequiredService<PlayerAuthService>();
            var session = await authService.AuthenticateDeviceAsync(request.DeviceId, request.ClientVersion, clientCountryReader.Read(httpContext.Request), httpContext.RequestAborted);

            if (session.Succeeded == false)
                return Results.Json(new { error = session.Error }, statusCode: StatusCodes.Status403Forbidden);

            return Results.Ok(CreateResponse(session, accessTokenIssuer));
        }

        private async Task<IResult> RefreshAsync(
            HttpContext httpContext,
            [FromBody] RefreshAuthRequest? request,
            [FromServices] AccessTokenIssuer accessTokenIssuer,
            [FromServices] ClientCountryReader clientCountryReader)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return Results.BadRequest(new { error = "userId and refreshToken are required." });

            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var authService = httpContext.RequestServices.GetRequiredService<PlayerAuthService>();
            var session = await authService.RefreshAsync(request.UserId, request.RefreshToken, clientCountryReader.Read(httpContext.Request), httpContext.RequestAborted);

            if (session.Succeeded == false)
                return Results.Json(new { error = session.Error }, statusCode: StatusCodes.Status401Unauthorized);

            return Results.Ok(CreateResponse(session, accessTokenIssuer));
        }

        private AuthTokensResponse CreateResponse(AuthSessionResult session, AccessTokenIssuer accessTokenIssuer)
        {
            var accessToken = accessTokenIssuer.Issue(session.UserId);

            return new AuthTokensResponse
            {
                UserId = session.UserId,
                AccessToken = accessToken.Value,
                AccessExpiresAt = accessToken.ExpiresAt,
                RefreshToken = session.RefreshToken,
                RefreshExpiresAt = session.RefreshExpiresAt,
            };
        }
    }
}
