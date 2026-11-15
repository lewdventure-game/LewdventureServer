using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;

namespace Server.Api.Payments
{
    internal sealed class PaymentAdminEndpoints
    {
        private readonly PaymentOptions _options;

        public PaymentAdminEndpoints(PaymentOptions options)
        {
            _options = options;
        }

        public void Map(WebApplication application)
        {
            if (application.Services.GetRequiredService<IOptions<AdminOptions>>().Value.Enabled == false)
                return;

            var group = application.MapGroup("/api/admin/payments")
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);
            group.MapGet("/{platform}/{orderId}", GetAsync);
            group.MapPost("/{platform}/{orderId}/reverse", ReverseAsync);

            if (_options.NutakuTitleId.Length != 0)
                group.MapGet("/nutaku/{orderId}/provider-status", GetNutakuStatusAsync);
        }

        private async Task<IResult> GetAsync(HttpContext context, string platform, string orderId, PaymentStore store)
        {
            var scope = GetScope(platform);

            if (scope.Length == 0 || 128 < orderId.Length)
                return Results.NotFound();

            var order = await store.GetAsync(scope, orderId, context.RequestAborted);

            if (order == null)
                return Results.NotFound();

            var movements = await store.GetMovementsAsync(order.Id, context.RequestAborted);

            return Results.Ok(new { order, movements });
        }

        private async Task<IResult> ReverseAsync(HttpContext context, string platform, string orderId, PaymentReversalRequest request,
            PaymentStore store, SteamPaymentsClient steam, SteamPayments settlement, AdminActorReader actorReader)
        {
            var scope = GetScope(platform);

            if (scope.Length == 0 || 128 < orderId.Length)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Reference) || 120 < request.Reference.Length)
                return Results.BadRequest();

            var order = await store.GetAsync(scope, orderId, context.RequestAborted);

            if (order == null)
                return Results.NotFound();

            if (order.State == PaymentOrderState.Reversed)
                return Results.NoContent();

            var evidence = actorReader.Read(context) + ":" + request.Reference;

            if (platform == "nutaku")
            {
                var reversed = await store.SettleAsync(order, true, order.ExternalId, "nutaku:operator:" + evidence, context.RequestAborted);

                return reversed ? Results.NoContent() : Results.Conflict();
            }

            if (order.State != PaymentOrderState.Granted)
                return Results.Conflict();

            try
            {
                var response = await steam.RefundAsync(order.ExternalId, context.RequestAborted);

                if (response.Value<string>("result") == "OK" && response["params"] is JObject parameters &&
                    parameters.Value<string>("orderid") == order.ExternalId && parameters.Value<string>("transid") == order.TransactionId)
                {
                    var reversed = await store.SettleAsync(order, true, order.TransactionId, "steam:RefundTxn:" + evidence, context.RequestAborted);

                    return reversed ? Results.NoContent() : Results.Conflict();
                }

                await settlement.RefreshAsync(order, context.RequestAborted);

                return Results.Accepted();
            }
            catch (SteamPaymentsException)
            {
                return Results.Accepted();
            }
        }

        private async Task<IResult> GetNutakuStatusAsync(HttpContext context, string orderId, PaymentStore store, NutakuPaymentsClient nutaku)
        {
            if (128 < orderId.Length)
                return Results.NotFound();

            var order = await store.GetAsync(_options.NutakuScope, orderId, context.RequestAborted);

            if (order == null)
                return Results.NotFound();

            var status = await nutaku.CheckStatusAsync(order.ExternalId, context.RequestAborted);

            return status.Length == 0 ? Results.StatusCode(StatusCodes.Status502BadGateway) : Results.Ok(new { status });
        }

        private string GetScope(string platform)
        {
            return platform switch
            {
                "steam" when _options.SteamAppId != 0 => _options.SteamScope,
                "nutaku" when _options.NutakuTitleId.Length != 0 => _options.NutakuScope,
                _ => string.Empty,
            };
        }
    }
}
