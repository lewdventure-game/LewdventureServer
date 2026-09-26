using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Infrastructure.Alerts;
using Server.Infrastructure.Mongo;

namespace Server.Api.Hosting
{
    internal sealed class StartupBannerService : IHostedService
    {
        private readonly IAlertPublisher _alertPublisher;
        private readonly AlertsOptions _alertsOptions;
        private readonly BuildInfo _buildInfo;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<StartupBannerService> _logger;
        private readonly ConfigSheetsOptions _configSheetsOptions;
        private readonly MongoOptions _mongoOptions;
        private readonly ServerOptions _serverOptions;

        public StartupBannerService(
            IAlertPublisher alertPublisher,
            IOptions<AlertsOptions> alertsOptions,
            BuildInfo buildInfo,
            IHostApplicationLifetime hostApplicationLifetime,
            IHostEnvironment hostEnvironment,
            ILogger<StartupBannerService> logger,
            IOptions<ConfigSheetsOptions> configSheetsOptions,
            IOptions<MongoOptions> mongoOptions,
            IOptions<ServerOptions> serverOptions)
        {
            _alertPublisher = alertPublisher;
            _alertsOptions = alertsOptions.Value;
            _buildInfo = buildInfo;
            _hostApplicationLifetime = hostApplicationLifetime;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _configSheetsOptions = configSheetsOptions.Value;
            _mongoOptions = mongoOptions.Value;
            _serverOptions = serverOptions.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _hostApplicationLifetime.ApplicationStarted.Register(LogBanner);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void LogBanner()
        {
            _logger.LogInformation(
                "[Startup] environment = {Environment} version = {Version} runtime = {Runtime} publicPort = {PublicPort} opsPort = {OpsPort} bind = {BindAddress} swagger = {Swagger} sheets = {SheetsCount} mongo = {MongoEnabled} database = {MongoDatabase}",
                _hostEnvironment.EnvironmentName,
                _buildInfo.Version,
                _buildInfo.Runtime,
                _serverOptions.PublicPort,
                _serverOptions.OpsPort,
                _serverOptions.BindAddress,
                _serverOptions.EnableSwagger,
                _configSheetsOptions.Sheets.Count,
                _mongoOptions.Enabled,
                _mongoOptions.DatabaseName);

            if (_alertsOptions.NotifyOnStartup == false)
                return;

            var alert = new AlertMessage(AlertSeverity.Info, "Server started", "version " + _buildInfo.Version, string.Empty);

            alert.Fields.Add(new KeyValuePair<string, string>("environment", _hostEnvironment.EnvironmentName));

            _alertPublisher.Publish(alert);
        }
    }
}
