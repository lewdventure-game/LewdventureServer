using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Server.Api.Health;
using Server.Api.Hosting;
using Server.Infrastructure.Alerts;

namespace Server.Api.Composition
{
    internal sealed class AlertsRegistrar
    {
        private readonly IConfiguration _configuration;

        public AlertsRegistrar(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Register(IServiceCollection services)
        {
            services.AddOptions<AlertsOptions>()
                .Bind(_configuration.GetSection(AlertsOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IValidateOptions<AlertsOptions>, AlertsOptionsValidator>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<AlertQueue>();
            services.AddSingleton<IAlertPublisher>(ResolveAlertQueue);
            services.AddSingleton<AlertThrottle>();
            services.AddSingleton<AlertDispatcher>();
            services.AddHostedService<AlertDispatchService>();

            var alertsOptions = new AlertsOptions();

            _configuration.GetSection(AlertsOptions.SectionName).Bind(alertsOptions);

            if (alertsOptions.Enabled)
            {
                services.AddHttpClient<DiscordWebhookAlertSink>();
                services.AddSingleton<IAlertSink>(ResolveDiscordSink);
            }
            else
            {
                services.AddSingleton<IAlertSink, NullAlertSink>();
            }

            services.AddSingleton<IHealthCheckPublisher, HealthStateAlertPublisher>();
            services.Configure<HealthCheckPublisherOptions>(options => ConfigurePublisher(options, alertsOptions));
        }

        private AlertQueue ResolveAlertQueue(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<AlertQueue>();
        }

        private DiscordWebhookAlertSink ResolveDiscordSink(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<DiscordWebhookAlertSink>();
        }

        private void ConfigurePublisher(HealthCheckPublisherOptions options, AlertsOptions alertsOptions)
        {
            options.Delay = TimeSpan.FromSeconds(15);
            options.Period = TimeSpan.FromSeconds(alertsOptions.HealthCheckPeriodSeconds);
        }
    }
}
