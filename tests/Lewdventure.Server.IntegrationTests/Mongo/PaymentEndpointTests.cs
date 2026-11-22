using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Server.Api.Payments;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

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

            var store = factory.Services.GetRequiredService<PaymentStore>();
            var options = factory.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;
            var order = await store.GetAsync(options.NutakuScope, orderId, CancellationToken.None);
            var profile = await factory.Services.GetRequiredService<PlayerProfileRepository>().GetAsync(order.UserId, CancellationToken.None);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(100));
            Assert.That(profile.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(1));

            var otherToken = await SignInNutakuAsync(client, "492");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
            using var otherOrder = await client.GetAsync("/api/payments/orders/" + orderId);
            Assert.That(otherOrder.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task SteamCheckout_LostFinalizeResponse_RecoversThroughReportWithoutRedelivery()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler();
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            var order = await StartSteamOrderAsync(client, factory, true);
            Assert.That(simulator.Initialized, Is.EqualTo(1));

            using var scope = factory.Services.CreateScope();
            var adapter = scope.ServiceProvider.GetRequiredService<SteamPayments>();
            var store = factory.Services.GetRequiredService<PaymentStore>();
            Assert.ThrowsAsync<SteamPaymentsException>(() => adapter.RefreshAsync(order, CancellationToken.None));
            await adapter.RefreshAsync(order, CancellationToken.None);
            Assert.That(simulator.Finalized, Is.EqualTo(1));

            var granted = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            var profiles = factory.Services.GetRequiredService<PlayerProfileRepository>();
            var grantedProfile = (await profiles.GetAsync(order.UserId, CancellationToken.None))!;
            Assert.That(granted.State, Is.EqualTo(PaymentOrderState.Granted));
            Assert.That(grantedProfile.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(100));
            Assert.That(grantedProfile.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(1));

            simulator.ReportOrders = new JArray(CreateSteamReport(granted, "Chargedback"));
            await scope.ServiceProvider.GetRequiredService<SteamPaymentReconciler>().ReconcileReportAsync(CancellationToken.None);

            var revoked = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            var inventory = await store.GetEntitlementsAsync(order.Scope, order.UserId, CancellationToken.None);
            var revokedProfile = (await profiles.GetAsync(order.UserId, CancellationToken.None))!;
            var records = factory.Services.GetRequiredService<IMongoDatabaseAccessor>().GetCollection<PaymentMovement>("payment_movements");
            var movements = await records.CountDocumentsAsync(item => item.OrderId == order.Id);
            var cursor = await store.GetReportStateAsync(order.Scope, CancellationToken.None);
            Assert.That(revoked.State, Is.EqualTo(PaymentOrderState.Reversed));
            Assert.That(revoked.ReviewRequired, Is.False);
            Assert.That(inventory, Is.Empty);
            Assert.That(revokedProfile.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(0));
            Assert.That(revokedProfile.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(0));
            Assert.That(movements, Is.EqualTo(2));
            Assert.That(cursor.LastSuccessAt, Is.Not.EqualTo(DateTime.MinValue));
        }

        [Test]
        public async Task SteamCheckout_QueryFailureBeforeInitialization_RetriesSameOrder()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler { QueryFailuresBeforeSuccess = 1, InitializationFailuresBeforeSuccess = 1 };
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            var order = await StartSteamOrderAsync(client, factory);

            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SteamPayments>().RefreshAsync(order, CancellationToken.None);
            var recovered = await factory.Services.GetRequiredService<PaymentStore>().GetAsync(order.Scope, order.ExternalId, CancellationToken.None);

            Assert.That(simulator.Initialized, Is.EqualTo(2));
            Assert.That(recovered.ExternalId, Is.EqualTo(order.ExternalId));
            Assert.That(recovered.TransactionId, Is.EqualTo("10"));
            Assert.That(recovered.State, Is.EqualTo(PaymentOrderState.AwaitingApproval));
            Assert.That(recovered.InitializationAttempts, Is.EqualTo(2));
        }

        [Test]
        public async Task NutakuSignIn_DeletedAccount_RestoresUserAndEntitlements()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler();
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            const string subject = "771";
            const string paymentId = "payment-before-delete";
            var first = await SignInNutakuSessionAsync(client, subject);
            var path = "/api/payments/nutaku/handler?titleId=lewdventure-sandbox&gameType=dl&userId=" + subject + "&paymentId=" + paymentId;
            var offer = new { paymentId, skuId = "starter", name = "Starter Access", price = 200, test = 1 };
            using var created = await NotifyAsync(client, HttpMethod.Post, path, JsonContent.Create(offer));
            using var completed = await NotifyAsync(client, HttpMethod.Put, path, null);
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(completed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            await factory.Services.GetRequiredService<PlayerDataService>().DeleteAsync(first.UserId, "admin:test", CancellationToken.None);
            var second = await SignInNutakuSessionAsync(client, subject);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.Token);
            var inventory = await client.GetFromJsonAsync<Dictionary<string, int>>("/api/payments/entitlements");
            var restoredProfile = await factory.Services.GetRequiredService<PlayerProfileRepository>().GetAsync(second.UserId, CancellationToken.None);

            Assert.That(second.UserId, Is.EqualTo(first.UserId));
            Assert.That(inventory?.GetValueOrDefault("starter-access"), Is.EqualTo(1));
            Assert.That(restoredProfile, Is.Not.Null);
            Assert.That(restoredProfile!.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(100));
            Assert.That(restoredProfile.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(1));
        }

        [Test]
        public async Task SteamPayment_StalePending_MarksReviewAndRetries()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler();
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            var order = await StartSteamOrderAsync(client, factory);
            var records = factory.Services.GetRequiredService<IMongoDatabaseAccessor>().GetCollection<PaymentOrder>("payment_orders");
            await records.UpdateOneAsync(
                item => item.Id == order.Id,
                Builders<PaymentOrder>.Update.Set(item => item.UpdatedAt, DateTime.UtcNow.AddDays(-2)),
                cancellationToken: CancellationToken.None);

            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SteamPaymentReconciler>().ReviewStaleAsync(CancellationToken.None);
            var store = factory.Services.GetRequiredService<PaymentStore>();
            var stale = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            Assert.That(stale.ReviewRequired, Is.True);
            Assert.That(stale.ReviewReason, Is.EqualTo("stale_pending"));

            var resolved = await store.ResolveReviewAsync(
                stale,
                "retry",
                "admin:test",
                "case-retry",
                CancellationToken.None);
            var leased = await store.LeaseNextSteamOrderAsync(order.Scope, CancellationToken.None);
            Assert.That(resolved, Is.True);
            Assert.That(leased, Is.Not.Null);
            Assert.That(leased.Id, Is.EqualTo(order.Id));
        }

        [Test]
        public async Task SteamRefund_SpentRewards_OpensReviewWithoutNegativeBalance()
        {
            await using var environment = new MongoTestEnvironment();
            await environment.StartAsync();
            using var simulator = new SteamPaymentTestHandler { LoseFinalizeResponse = false };
            using var factory = new PaymentApiFactory(environment, simulator);
            using var client = factory.CreateClient();
            var order = await StartSteamOrderAsync(client, factory);

            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SteamPayments>().RefreshAsync(order, CancellationToken.None);
            var store = factory.Services.GetRequiredService<PaymentStore>();
            var granted = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            var profiles = factory.Services.GetRequiredService<PlayerProfileRepository>();
            var profile = (await profiles.GetAsync(order.UserId, CancellationToken.None))!;
            var expectedRev = profile.Rev;
            profile.Resources["soft_money"] = 40;
            profile.Rev += 1;
            Assert.That(await profiles.ReplaceAsync(profile, expectedRev, CancellationToken.None), Is.True);

            simulator.ReportOrders = new JArray(CreateSteamReport(granted, "Refunded"));
            await scope.ServiceProvider.GetRequiredService<SteamPaymentReconciler>().ReconcileReportAsync(CancellationToken.None);
            var reversed = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            var unchanged = (await profiles.GetAsync(order.UserId, CancellationToken.None))!;
            Assert.That(reversed.State, Is.EqualTo(PaymentOrderState.Reversed));
            Assert.That(reversed.ReviewRequired, Is.True);
            Assert.That(unchanged.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(40));
            Assert.That(unchanged.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(1));

            var resolved = await store.ResolveReviewAsync(
                reversed,
                "deduct_available",
                "admin:test",
                "case-spent",
                CancellationToken.None);
            var recovered = (await profiles.GetAsync(order.UserId, CancellationToken.None))!;
            var finalOrder = await store.GetAsync(order.Scope, order.ExternalId, CancellationToken.None);
            Assert.That(resolved, Is.True);
            Assert.That(recovered.Resources.GetValueOrDefault("soft_money"), Is.EqualTo(0));
            Assert.That(recovered.Flags.GetValueOrDefault("starter_pack"), Is.EqualTo(0));
            Assert.That(finalOrder.ReviewRequired, Is.False);
        }

        private async Task<PaymentOrder> StartSteamOrderAsync(HttpClient client, PaymentApiFactory factory, bool repeat = false)
        {
            using var signedIn = await client.PostAsJsonAsync("/api/auth/steam", new { credential = "CAFE" });
            Assert.That(signedIn.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var login = await signedIn.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
            var checkout = new { requestKey = Guid.NewGuid().ToString("D"), sku = "starter", currency = "USD", amount = 199 };
            using var response = await client.PostAsJsonAsync("/api/payments/steam/orders", checkout);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            if (repeat)
            {
                using var repeated = await client.PostAsJsonAsync("/api/payments/steam/orders", checkout);
                Assert.That(repeated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var orderId = result.GetProperty("orderId").GetString() ?? string.Empty;
            var options = factory.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;

            return await factory.Services.GetRequiredService<PaymentStore>().GetAsync(options.SteamScope, orderId, CancellationToken.None);
        }

        private JObject CreateSteamReport(PaymentOrder order, string status)
        {
            return new JObject
            {
                ["orderid"] = order.ExternalId,
                ["transid"] = order.TransactionId,
                ["steamid"] = order.Subject,
                ["status"] = status,
                ["currency"] = order.Currency,
                ["time"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["items"] = new JArray(new JObject
                {
                    ["itemid"] = order.SteamItemId,
                    ["qty"] = 1,
                    ["amount"] = 169,
                    ["vat"] = 30,
                    ["itemstatus"] = status,
                }),
            };
        }

        private async Task<string> SignInNutakuAsync(HttpClient client, string subject)
        {
            var session = await SignInNutakuSessionAsync(client, subject);

            return session.Token;
        }

        private async Task<(string Token, string UserId)> SignInNutakuSessionAsync(HttpClient client, string subject)
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

            return (login.GetProperty("accessToken").GetString() ?? string.Empty, login.GetProperty("userId").GetString() ?? string.Empty);
        }

        private async Task<HttpResponseMessage> NotifyAsync(HttpClient client, HttpMethod method, string path, [AllowNull] JsonContent body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = body };
            request.Headers.Add("NutakuS2sKey", PaymentApiFactory.NutakuKey);

            return await client.SendAsync(request);
        }
    }
}
