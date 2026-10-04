using Server.Api.Endpoints;
using Server.Api.Hosting;
using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo;

namespace Server.Api.Composition
{
    internal sealed class AnalyticsRegistrar
    {
        private readonly IConfiguration _configuration;

        public AnalyticsRegistrar(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Register(IServiceCollection services)
        {
            services.AddOptions<AnalyticsOptions>()
                .Bind(_configuration.GetSection(AnalyticsOptions.SectionName))
                .Validate(IsValid, "Analytics:Enabled requires ClickHouseUrl, Database and User, positive QueueCapacity, BatchSize, FlushIntervalSeconds and MaxEventsPerRequest.")
                .ValidateOnStart();

            services.AddSingleton<AnalyticsRowFactory>();
            services.AddSingleton<AnalyticsEventConverter>();

            var analyticsOptions = new AnalyticsOptions();
            var mongoOptions = new MongoOptions();

            _configuration.GetSection(AnalyticsOptions.SectionName).Bind(analyticsOptions);
            _configuration.GetSection(MongoOptions.SectionName).Bind(mongoOptions);

            if (analyticsOptions.Enabled == false || mongoOptions.Enabled == false)
            {
                services.AddSingleton<IAnalyticsSink, NullAnalyticsSink>();

                return;
            }

            services.AddSingleton<AnalyticsQueue>();
            services.AddSingleton<IAnalyticsSink>(ResolveQueue);
            services.AddSingleton<AnalyticsSpool>();
            services.AddSingleton<AnalyticsContextResolver>();
            services.AddHttpClient<ClickHouseClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
            services.AddHostedService<AnalyticsWriterService>();
        }

        private AnalyticsQueue ResolveQueue(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<AnalyticsQueue>();
        }

        private bool IsValid(AnalyticsOptions options)
        {
            if (options.QueueCapacity <= 0 || options.BatchSize <= 0 || options.FlushIntervalSeconds <= 0 || options.MaxEventsPerRequest <= 0 || options.MaxPropertiesBytes <= 0)
                return false;

            if (options.Enabled == false)
                return true;

            return string.IsNullOrWhiteSpace(options.ClickHouseUrl) == false
                && string.IsNullOrWhiteSpace(options.Database) == false
                && string.IsNullOrWhiteSpace(options.User) == false;
        }
    }
}
