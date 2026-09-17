using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Infrastructure.GoogleSheets;
using Server.Infrastructure.Mongo;

namespace Server.Api.Hosting
{
    internal sealed class StartupBannerService : IHostedService
    {
        private readonly BuildInfo _buildInfo;
        private readonly GoogleCredentialProvider _googleCredentialProvider;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<StartupBannerService> _logger;
        private readonly GoogleSheetsOptions _googleSheetsOptions;
        private readonly MongoOptions _mongoOptions;
        private readonly ServerOptions _serverOptions;

        public StartupBannerService(
            BuildInfo buildInfo,
            GoogleCredentialProvider googleCredentialProvider,
            IHostApplicationLifetime hostApplicationLifetime,
            IHostEnvironment hostEnvironment,
            ILogger<StartupBannerService> logger,
            IOptions<GoogleSheetsOptions> googleSheetsOptions,
            IOptions<MongoOptions> mongoOptions,
            IOptions<ServerOptions> serverOptions)
        {
            _buildInfo = buildInfo;
            _googleCredentialProvider = googleCredentialProvider;
            _hostApplicationLifetime = hostApplicationLifetime;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _googleSheetsOptions = googleSheetsOptions.Value;
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
            _logger.LogInformation("[Shutdown] server stopping");

            return Task.CompletedTask;
        }

        private void LogBanner()
        {
            var credentialsSource = string.IsNullOrWhiteSpace(_googleSheetsOptions.CredentialsJson)
                ? _googleCredentialProvider.ResolvePath(_googleSheetsOptions.CredentialsPath)
                : "inline json";

            _logger.LogInformation(
                "[Startup] environment = {Environment} version = {Version} runtime = {Runtime} publicPort = {PublicPort} opsPort = {OpsPort} bind = {BindAddress} swagger = {Swagger} sheets = {SheetsCount} credentials = {CredentialsSource} mongo = {MongoEnabled} database = {MongoDatabase}",
                _hostEnvironment.EnvironmentName,
                _buildInfo.Version,
                _buildInfo.Runtime,
                _serverOptions.PublicPort,
                _serverOptions.OpsPort,
                _serverOptions.BindAddress,
                _serverOptions.EnableSwagger,
                _googleSheetsOptions.Sheets.Count,
                credentialsSource,
                _mongoOptions.Enabled,
                _mongoOptions.DatabaseName);
        }
    }
}
