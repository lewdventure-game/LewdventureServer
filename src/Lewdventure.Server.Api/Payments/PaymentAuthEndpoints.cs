using Server.Api.Security;
using Server.Infrastructure.Mongo.Players;

namespace Server.Api.Payments
{
    internal sealed class PaymentAuthEndpoints
    {
        private readonly PaymentOptions _options;

        public PaymentAuthEndpoints(PaymentOptions options)
        {
            _options = options;
        }

        public void Map(WebApplication application)
        {
            if (_options.SteamAppId != 0)
                application.MapPost("/api/auth/steam", SignInSteamAsync).RequireRateLimiting(SecurityNames.AuthRateLimitPolicy);

            if (_options.NutakuTitleId.Length == 0)
                return;

            application.MapPost("/api/auth/nutaku", SignInNutakuAsync).RequireRateLimiting(SecurityNames.AuthRateLimitPolicy);
            application.MapPost("/api/payments/nutaku/handshake", HandshakeAsync);
        }

        private async Task<IResult> SignInSteamAsync(HttpContext context, PaymentSignInRequest request, SteamPaymentsClient steam, PaymentIdentity identity, AccessTokenIssuer issuer)
        {
            if (string.IsNullOrEmpty(request.Credential) || 4096 < request.Credential.Length || request.Credential.Length % 2 != 0)
                return Results.BadRequest();

            foreach (var character in request.Credential)
            {
                if (char.IsAsciiHexDigit(character) == false)
                    return Results.BadRequest();
            }

            try
            {
                var subject = await steam.AuthenticateAsync(request.Credential, context.RequestAborted);

                return await IssueAsync(context, _options.SteamScope, subject, identity, issuer);
            }
            catch (SteamPaymentsException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private async Task<IResult> SignInNutakuAsync(HttpContext context, PaymentSignInRequest request, NutakuPayments nutaku, PaymentIdentity identity, AccessTokenIssuer issuer)
        {
            if (string.IsNullOrEmpty(request.Credential))
                return Results.BadRequest();

            var subject = await nutaku.ConsumeLoginCodeAsync(request.Credential, context.RequestAborted);

            return await IssueAsync(context, _options.NutakuScope, subject, identity, issuer);
        }

        private async Task<IResult> HandshakeAsync(HttpContext context, NutakuPayments nutaku)
        {
            if (nutaku.IsAuthentic(context.Request) == false)
                return Results.Unauthorized();

            var subject = nutaku.ReadSubject(context.Request);

            if (subject.Length == 0)
                return Results.BadRequest();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(4));

            try
            {
                var code = await nutaku.CreateLoginCodeAsync(subject, deadline.Token);
                context.Response.Headers.CacheControl = "no-store";

                return Results.Ok(new { code });
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private async Task<IResult> IssueAsync(HttpContext context, string scope, string subject, PaymentIdentity identity, AccessTokenIssuer issuer)
        {
            if (subject.Length == 0)
                return Results.Unauthorized();

            var user = await identity.SignInAsync(scope, subject, context.RequestAborted);

            if (user.Id.Length == 0 || user.Status != UserDocument.ActiveStatus || user.Qa != null)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var token = issuer.Issue(user.Id, scope);
            context.Response.Headers.CacheControl = "no-store";

            return Results.Ok(new { userId = user.Id, accessToken = token.Value, accessExpiresAt = token.ExpiresAt });
        }
    }
}
