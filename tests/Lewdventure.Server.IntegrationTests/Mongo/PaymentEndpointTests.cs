using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Server.Api.Payments;
using Server.Infrastructure.Mongo;

namespace Tests.Integration.Mongo
{
    [TestFixture]
    [Category("Integration")]
    [NonParallelizable]
    public sealed class PaymentEndpointTests
    {
        [Test]
        public async Task NutakuCheckout_ParallelAuthenticatedCallbacks_GrantsOnce()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler();
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            const string subject = "491";
            const string orderId = "payment-for-user-491";
            var handler = "/api/payments/nutaku/handler?titleId=lewdventure-sandbox&gameType=dl&userId=" + subject + "&paymentId=" + orderId;
            using var forged = await client.PutAsync(handler, null);
            Assert.That(forged.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            var token = await SignInNutakuAsync(client, subject);
            var offer = new { paymentId = orderId, skuId = "starter", name = "Starter Access", price = 200, test = 1 };
            using var wrongPrice = await NotifyAsync(client, HttpMethod.Post, handler, JsonContent.Create(new
            {
                offer.paymentId,
                offer.skuId,
                offer.name,
                price = 20,
                offer.test,
            }));
            Assert.That(wrongPrice.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            using var created = await NotifyAsync(client, HttpMethod.Post, handler, JsonContent.Create(offer));
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var pair = await Task.WhenAll(NotifyAsync(client, HttpMethod.Put, handler, null), NotifyAsync(client, HttpMethod.Put, handler, null));
            using var first = pair[0];
            using var second = pair[1];
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var inventory = await client.GetFromJsonAsync<Dictionary<string, int>>("/api/payments/entitlements");
            Assert.That(inventory, Does.ContainKey("starter-access"));
            Assert.That(inventory?.GetValueOrDefault("starter-access"), Is.EqualTo(1));

            var otherToken = await SignInNutakuAsync(client, "492");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            using var otherOrder = await client.GetAsync("/api/payments/orders/" + orderId);
            Assert.That(otherOrder.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task SteamCheckout_LostFinalizeResponse_RecoversAndRevokesWithoutRedelivery()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler();
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            using var signedIn = await client.PostAsJsonAsync("/api/auth/steam", new { credential = "CAFE" });
            Assert.That(signedIn.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            var login = await signedIn.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
            var checkout = new { requestKey = Guid.NewGuid().ToString("D"), sku = "starter", currency = "USD", amount = 199 };
            using var response = await client.PostAsJsonAsync("/api/payments/steam/orders", checkout);
            using var repeated = await client.PostAsJsonAsync("/api/payments/steam/orders", checkout);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(simulator.Initialized, Is.EqualTo(1));

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var orderId = result.GetProperty("orderId").GetString() ?? string.Empty;
            var options = factory.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;
            var store = factory.Services.GetRequiredService<PaymentStore>();
            var adapter = factory.Services.GetRequiredService<SteamPayments>();
            var order = await store.GetAsync(options.SteamScope, orderId, CancellationToken.None);
            Assert.ThrowsAsync<SteamPaymentsException>(() => adapter.RefreshAsync(order, CancellationToken.None));
            await adapter.RefreshAsync(order, CancellationToken.None);
            Assert.That(simulator.Finalized, Is.EqualTo(1));

            var granted = await store.GetAsync(options.SteamScope, orderId, CancellationToken.None);
            Assert.That(granted.State, Is.EqualTo(PaymentOrderState.Granted));

            await adapter.ApplyAsync(granted, new SteamTransaction
            {
                OrderId = orderId,
                TransactionId = granted.TransactionId,
                Subject = granted.Subject,
                Currency = granted.Currency,
                ItemId = granted.SteamItemId,
                Quantity = 1,
                Status = "Chargedback",
            }, CancellationToken.None);
            await adapter.RefreshAsync(order, CancellationToken.None);

            var revoked = await store.GetAsync(options.SteamScope, orderId, CancellationToken.None);
            var inventory = await store.GetEntitlementsAsync(order.Scope, order.UserId, CancellationToken.None);
            var records = factory.Services.GetRequiredService<IMongoDatabaseAccessor>().GetCollection<PaymentMovement>("payment_movements");
            var movements = await records.CountDocumentsAsync(item => item.OrderId == order.Id);
            Assert.That(revoked.State, Is.EqualTo(PaymentOrderState.Reversed));
            Assert.That(inventory, Is.Empty);
            Assert.That(movements, Is.EqualTo(2));
        }

        private async Task<string> SignInNutakuAsync(HttpClient client, string subject)
        {
            var path = "/api/payments/nutaku/handshake?titleId=lewdventure-sandbox&gameType=dl&userId=" + subject;
            using var handshake = await NotifyAsync(client, HttpMethod.Post, path, null);
            Assert.That(handshake.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var body = await handshake.Content.ReadFromJsonAsync<JsonElement>();
            var credential = body.GetProperty("code").GetString();

            using var signedIn = await client.PostAsJsonAsync("/api/auth/nutaku", new { credential });
            using var replay = await client.PostAsJsonAsync("/api/auth/nutaku", new { credential });
            Assert.That(signedIn.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(replay.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            var login = await signedIn.Content.ReadFromJsonAsync<JsonElement>();

            return login.GetProperty("accessToken").GetString() ?? string.Empty;
        }

        private async Task<HttpResponseMessage> NotifyAsync(HttpClient client, HttpMethod method, string path, [AllowNull] JsonContent body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = body };
            request.Headers.Add("NutakuS2sKey", PaymentApiFactory.NutakuKey);

            return await client.SendAsync(request);
        }
    }
}
