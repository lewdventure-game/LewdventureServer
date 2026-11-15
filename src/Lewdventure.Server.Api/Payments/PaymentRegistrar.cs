using Microsoft.Extensions.Options;
using Server.Infrastructure.Mongo;

namespace Server.Api.Payments
{
    internal sealed class PaymentRegistrar
    {
        public void Register(IServiceCollection services, IConfiguration configuration)
        {
            var section = configuration.GetSection(PaymentOptions.SectionName);
            services.AddOptions<PaymentOptions>().Bind(section).ValidateOnStart();
            services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();

            var options = new PaymentOptions();
            section.Bind(options);

            if (options.Configured == false)
                return;

            services.AddSingleton<PaymentStore>();
            services.AddSingleton<IMongoIndexContributor>(ResolvePaymentIndexes);
            services.AddSingleton<PaymentIdentity>();
            services.AddSingleton<PaymentSessionFilter>();
            services.AddSingleton<NutakuPayments>();
            services.AddTransient<SteamPayments>();
            services.AddHttpClient<SteamPaymentsClient>(ConfigureSteamClient)
                .RemoveAllLoggers()
                .ConfigurePrimaryHttpMessageHandler(CreatePaymentHandler);
            services.AddHttpClient<NutakuPaymentsClient>(ConfigureNutakuClient)
                .RemoveAllLoggers()
                .ConfigurePrimaryHttpMessageHandler(CreatePaymentHandler);

            if (options.SteamAppId != 0)
                services.AddHostedService<SteamPaymentWorker>();
        }

        private IMongoIndexContributor ResolvePaymentIndexes(IServiceProvider services)
        {
            return services.GetRequiredService<PaymentStore>();
        }

        private void ConfigureSteamClient(HttpClient client)
        {
            client.BaseAddress = new Uri("https://partner.steam-api.com/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.MaxResponseContentBufferSize = 8 * 1024 * 1024;
        }

        private void ConfigureNutakuClient(HttpClient client)
        {
            client.Timeout = TimeSpan.FromSeconds(8);
            client.MaxResponseContentBufferSize = 32 * 1024;
        }

        private HttpMessageHandler CreatePaymentHandler()
        {
            return new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ActivityHeadersPropagator = null };
        }
    }
}
