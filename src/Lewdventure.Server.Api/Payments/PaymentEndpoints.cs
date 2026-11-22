using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Server.Api.Security;

namespace Server.Api.Payments
{
    internal sealed class PaymentEndpoints
    {
        private readonly PaymentOptions _options;
        private readonly PaymentResponseFactory _responses;

        public PaymentEndpoints(PaymentOptions options, PaymentResponseFactory responses)
        {
            _options = options;
            _responses = responses;
        }

        public void Map(WebApplication application)
        {
            if (_options.Configured == false)
                return;

            new PaymentAuthEndpoints(_options).Map(application);
            new PaymentAdminEndpoints(_options).Map(application);

            var filter = application.Services.GetRequiredService<PaymentSessionFilter>();
            var group = application.MapGroup("/api/payments")
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .AddEndpointFilterFactory(filter.Create);
            group.MapGet("/catalog", GetCatalogAsync);
            group.MapGet("/orders", ListOrdersAsync);
            group.MapGet("/orders/{orderId}", GetOrderAsync);
            group.MapGet("/entitlements", GetEntitlementsAsync);

            if (_options.SteamAppId != 0)
                group.MapPost("/steam/orders", StartSteamOrderAsync);

            if (_options.NutakuTitleId.Length != 0)
            {
                application.MapPost("/api/payments/nutaku/handler", CreateNutakuOrderAsync);
                application.MapPut("/api/payments/nutaku/handler", CompleteNutakuOrderAsync);
            }
        }

        private async Task<IResult> GetCatalogAsync(HttpContext context, SteamPaymentsClient steam)
        {
            var session = ReadSession(context);
            var enabled = session.Steam ? _options.SteamPurchasesEnabled : _options.NutakuPurchasesEnabled;
            var offers = new List<object>();

            if (enabled == false)
                return Results.Ok(offers);

            try
            {
                var currency = session.Steam ? await steam.GetCurrencyAsync(session.Subject, context.RequestAborted) : "GOLD";

                foreach (var offer in _options.Offers)
                {
                    if (TryPrice(session, offer, currency, out var amount))
                        offers.Add(_responses.CreateCatalog(offer, currency, amount));
                }

                return Results.Ok(offers);
            }
            catch (SteamPaymentsException)
            {
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }

        private async Task<IResult> StartSteamOrderAsync(HttpContext context, SteamCheckoutRequest request, PaymentStore store, SteamPayments steam, SteamPaymentsClient client)
        {
            var session = ReadSession(context);

            if (session.Steam == false)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (Guid.TryParseExact(request.RequestKey, "D", out var requestKey) == false || requestKey == Guid.Empty ||
                string.IsNullOrEmpty(request.Sku) || 64 < request.Sku.Length || request.Currency?.Length != 3 || request.Amount <= 0)
                return Results.BadRequest();

            var key = requestKey.ToString("D");
            var orderId = steam.GetOrderId(session.UserId, key);
            var order = await store.GetAsync(session.Scope, orderId, context.RequestAborted);

            try
            {
                if (order == null)
                {
                    if (_options.SteamPurchasesEnabled == false)
                        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                    var currency = await client.GetCurrencyAsync(session.Subject, context.RequestAborted);

                    if (currency != request.Currency)
                        return Results.Conflict(new { error = "Price has changed or this currency is unavailable." });

                    foreach (var offer in _options.Offers)
                    {
                        if (offer.Sku != request.Sku || TryPrice(session, offer, currency, out var amount) == false || amount != request.Amount)
                            continue;

                        order = await steam.ReserveAsync(session.UserId, session.Subject, key, offer, currency, amount, context.RequestAborted);
                        break;
                    }
                }

                if (order == null || order.UserId != session.UserId || order.Subject != session.Subject || order.RequestKey != key ||
                    order.Sku != request.Sku || order.Currency != request.Currency || order.Amount != request.Amount)
                    return Results.Conflict(new { error = "The offer or request key does not match." });

                if (_options.SteamPurchasesEnabled)
                    order = await steam.StartAsync(order, context.RequestAborted);

                if (order == null)
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

                return Results.Ok(_responses.CreateOrder(order));
            }
            catch (SteamPaymentsException)
            {
                return Results.Json(new { orderId, error = "Steam could not confirm the request. Check this order or retry with the same request key." },
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }

        private async Task<IResult> GetOrderAsync(HttpContext context, string orderId, PaymentStore store)
        {
            if (128 < orderId.Length)
                return Results.NotFound();

            var session = ReadSession(context);
            var order = await store.GetAsync(session.Scope, orderId, context.RequestAborted);

            return order == null || order.UserId != session.UserId || order.Subject != session.Subject
                ? Results.NotFound()
                : Results.Ok(_responses.CreateOrder(order));
        }

        private async Task<IResult> ListOrdersAsync(HttpContext context, PaymentStore store)
        {
            var session = ReadSession(context);
            var orders = await store.ListRecentAsync(session.Scope, session.UserId, session.Subject, context.RequestAborted);
            var result = new List<object>(orders.Count);

            foreach (var order in orders)
                result.Add(_responses.CreateOrder(order));

            return Results.Ok(result);
        }

        private async Task<IResult> GetEntitlementsAsync(HttpContext context, PaymentStore store)
        {
            var session = ReadSession(context);
            var entries = await store.GetEntitlementsAsync(session.Scope, session.UserId, context.RequestAborted);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var entry in entries)
                counts[entry.Entitlement] = counts.GetValueOrDefault(entry.Entitlement) + 1;

            return Results.Ok(counts);
        }

        private async Task<IResult> CreateNutakuOrderAsync(HttpContext context, NutakuPayments nutaku)
        {
            if (nutaku.IsAuthentic(context.Request) == false)
                return Results.Unauthorized();

            var subject = nutaku.ReadSubject(context.Request);
            var paymentId = nutaku.ReadPaymentId(context.Request);

            if (subject.Length == 0 || paymentId.Length == 0 || context.Request.HasJsonContentType() == false)
                return Results.BadRequest();

            if (8192 < context.Request.ContentLength)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (size != null && size.IsReadOnly == false)
                size.MaxRequestBodySize = 8192;

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(4));

            try
            {
                var request = await context.Request.ReadFromJsonAsync<NutakuPaymentRequest>(deadline.Token);

                if (request == null || await nutaku.CreateOrderAsync(paymentId, subject, request, deadline.Token) == false)
                    return Results.BadRequest();

                return NutakuSuccess();
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
            catch (OperationCanceledException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private async Task<IResult> CompleteNutakuOrderAsync(HttpContext context, NutakuPayments nutaku, ILogger<PaymentEndpoints> logger)
        {
            if (nutaku.IsAuthentic(context.Request) == false)
                return Results.Unauthorized();

            var subject = nutaku.ReadSubject(context.Request);
            var paymentId = nutaku.ReadPaymentId(context.Request);

            if (subject.Length == 0 || paymentId.Length == 0)
                return Results.BadRequest();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(22));

            try
            {
                if (await nutaku.CompleteOrderAsync(paymentId, subject, deadline.Token) == false)
                    return Results.Conflict();

                if (context.RequestAborted.IsCancellationRequested)
                {
                    logger.LogWarning("[Error] Nutaku response disconnected after issuing payment entitlement paymentId = {PaymentId}", paymentId);
                    await nutaku.MarkCompletionUncertainAsync(paymentId, subject, "response_disconnected");
                }

                return NutakuSuccess();
            }
            catch (PaymentSettlementException exception)
            {
                await nutaku.MarkCompletionUncertainAsync(paymentId, subject, exception.Retryable ? "settlement_retry" : "settlement_failed");

                return Results.StatusCode(exception.Retryable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("[Error] Nutaku payment completion outcome needs reconciliation paymentId = {PaymentId}", paymentId);
                await nutaku.MarkCompletionUncertainAsync(paymentId, subject, "completion_timeout");

                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        private IResult NutakuSuccess()
        {
            return Results.Ok(new { response_code = "ok" });
        }

        private PaymentSession ReadSession(HttpContext context)
        {
            if (context.Items[typeof(PaymentSession)] is PaymentSession session)
                return session;

            throw new InvalidOperationException("Payment session filter is required.");
        }

        private bool TryPrice(PaymentSession session, PaymentOffer offer, string currency, out long amount)
        {
            if (session.Steam)
                return offer.SteamPrices.TryGetValue(currency, out amount);

            amount = offer.NutakuGold;

            return 0 < amount;
        }
    }
}
