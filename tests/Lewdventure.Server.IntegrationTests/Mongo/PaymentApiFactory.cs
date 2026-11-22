using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Server;
using Server.Api.Payments;

namespace Tests.Integration.Mongo
{
    internal sealed class PaymentApiFactory : WebApplicationFactory<Program>
    {
        public const string NutakuKey = "fake-nutaku-key-for-integration-tests";

        private readonly MongoTestEnvironment _environment;
        private readonly FixturePaths _paths;
        private readonly SteamPaymentTestHandler _steam;

        public PaymentApiFactory(MongoTestEnvironment environment, SteamPaymentTestHandler steam)
        {
            _environment = environment;
            _steam = steam;
            _paths = new FixturePaths();
            Environment.SetEnvironmentVariable("ASPNETCORE_TEST_CONTENTROOT_LEWDVENTURE_SERVER_API", _paths.ApiDirectory);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Mongo:Enabled", "true");
            builder.UseSetting("Mongo:ConnectionString", _environment.ConnectionString);
            builder.UseSetting("Mongo:DatabaseName", _environment.DatabaseName);
            builder.UseSetting("GameConfig:Source", "File");
            builder.UseSetting("GameConfig:FilePath", _paths.FixturePath);
            builder.UseSetting("Auth:Enabled", "true");
            builder.UseSetting("Auth:SigningKey", "test-payment-auth-signing-key-0123456789");
            builder.UseSetting("Payments:SteamAppId", "123456");
            builder.UseSetting("Payments:SteamPublisherKey", "fake-steam-key-for-integration-tests");
            builder.UseSetting("Payments:SteamPurchasesEnabled", "true");
            builder.UseSetting("Payments:NutakuTitleId", "lewdventure-sandbox");
            builder.UseSetting("Payments:NutakuServerKey", NutakuKey);
            builder.UseSetting("Payments:NutakuPurchasesEnabled", "true");
            builder.UseSetting("Payments:Offers:0:Sku", "starter");
            builder.UseSetting("Payments:Offers:0:Name", "Starter Access");
            builder.UseSetting("Payments:Offers:0:Entitlement", "starter-access");
            builder.UseSetting("Payments:Offers:0:Rewards", "resource:soft_money:100,account:starter_pack:1");
            builder.UseSetting("Payments:Offers:0:SteamItemId", "1001");
            builder.UseSetting("Payments:Offers:0:SteamPrices:USD", "199");
            builder.UseSetting("Payments:Offers:0:NutakuGold", "200");
            builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            builder.ConfigureServices(ConfigureSteam);
        }

        private void ConfigureSteam(IServiceCollection services)
        {
            for (var i = services.Count - 1; 0 <= i; i--)
            {
                if (services[i].ImplementationType == typeof(SteamPaymentWorker))
                    services.RemoveAt(i);
            }

            services.AddTransient<SteamPaymentsClient>(provider => new SteamPaymentsClient(
                new HttpClient(_steam, disposeHandler: false) { BaseAddress = new Uri("https://partner.steam-api.com/") },
                provider.GetRequiredService<IOptions<PaymentOptions>>()));
        }
    }
}
