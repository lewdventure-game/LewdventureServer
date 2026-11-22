using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;
using Server.Services;

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
            group.MapGet("/review", ListReviewAsync);
            group.MapGet("/by-user/{userId}", ListByUserAsync);
            group.MapGet("/{platform}/{orderId}", GetAsync);
            group.MapPost("/{platform}/{orderId}/reverse", ReverseAsync);
            group.MapPost("/{platform}/{orderId}/review/resolve", ResolveReviewAsync);

            if (_options.NutakuTitleId.Length != 0)
                group.MapGet("/nutaku/{orderId}/provider-status", GetNutakuStatusAsync);
        }

        private async Task<IResult> ListReviewAsync(HttpContext context, PaymentStore store)
        {
            var orders = await store.ListReviewAsync(context.RequestAborted);

            return Results.Ok(orders);
        }

        private async Task<IResult> ListByUserAsync(HttpContext context, string userId, PaymentStore store)
        {
            if (64 < userId.Length)
                return Results.NotFound();

            var orders = await store.ListForUserAsync(userId, context.RequestAborted);
            var entitlements = await store.GetEntitlementsForUserAsync(userId, context.RequestAborted);

            return Results.Ok(new { orders, entitlements });
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
            var audits = await store.GetAuditsAsync(order.Id, context.RequestAborted);

            return Results.Ok(new { order, movements, audits });
        }

        private async Task<IResult> ReverseAsync(
            HttpContext context,
            string platform,
            string orderId,
            PaymentReversalRequest request,
            PaymentStore store,
            SteamPaymentsClient steam,
            SteamPayments settlement,
            AdminActorReader actorReader,
            IConfigDistributor configDistributor)
        {
            var scope = GetScope(platform);

            if (scope.Length == 0 || 128 < orderId.Length)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Reference) || 120 < request.Reference.Length)
                return Results.BadRequest();

            var order = await store.GetAsync(scope, orderId, context.RequestAborted);

            if (order == null)
                return Results.NotFound();

            var actor = actorReader.Read(context);
            await store.AddAuditAsync(order, "refund_requested", actor, request.Reference, platform, context.RequestAborted);

            if (order.State == PaymentOrderState.Reversed)
                return Results.NoContent();

            var evidence = actor + ":" + request.Reference;

            try
            {
                if (platform == "nutaku")
                {
                    var reversed = await store.SettleAsync(order, true, order.ExternalId, "nutaku:operator:" + evidence, configDistributor, context.RequestAborted);

                    return reversed ? Results.NoContent() : Results.Conflict();
                }

                if (order.State != PaymentOrderState.Granted)
                    return Results.Conflict();

                var response = await steam.RefundAsync(order.ExternalId, context.RequestAborted);

                if (response.Value<string>("result") == "OK" && response["params"] is JObject parameters &&
                    parameters.Value<string>("orderid") == order.ExternalId && parameters.Value<string>("transid") == order.TransactionId)
                {
                    var reversed = await store.SettleAsync(order, true, order.TransactionId, "steam:RefundTxn:" + evidence, configDistributor, context.RequestAborted);

                    if (reversed)
                        await store.AddAuditAsync(order, "refund_confirmed", actor, request.Reference, "steam:RefundTxn", context.RequestAborted);

                    return reversed ? Results.NoContent() : Results.Conflict();
                }

                await store.AddAuditAsync(order, "refund_uncertain", actor, request.Reference, "steam:RefundTxn", context.RequestAborted);
                await settlement.RefreshAsync(order, context.RequestAborted);

                return Results.Accepted();
            }
            catch (SteamPaymentsException)
            {
                await store.AddAuditAsync(order, "refund_uncertain", actor, request.Reference, "steam:communication", context.RequestAborted);

                return Results.Accepted();
            }
            catch (PaymentSettlementException exception)
            {
                return Results.StatusCode(exception.Retryable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict);
            }
        }

        private async Task<IResult> ResolveReviewAsync(
            HttpContext context,
            string platform,
            string orderId,
            PaymentReviewRequest request,
            PaymentStore store,
            AdminActorReader actorReader)
        {
            var scope = GetScope(platform);

            if (scope.Length == 0 || 128 < orderId.Length)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.Reference) || 120 < request.Reference.Length || IsResolution(request.Resolution) == false)
                return Results.BadRequest();

            var order = await store.GetAsync(scope, orderId, context.RequestAborted);

            if (order == null)
                return Results.NotFound();

            var actor = actorReader.Read(context);
            var resolved = await store.ResolveReviewAsync(order, request.Resolution, actor, request.Reference, context.RequestAborted);

            return resolved ? Results.NoContent() : Results.Conflict();
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

        private bool IsResolution(string resolution)
        {
            return resolution is "confirmed" or "retry" or "rewards_kept" or "deduct_available";
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
