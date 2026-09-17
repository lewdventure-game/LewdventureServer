using Microsoft.Extensions.Options;
using Server.Api.Options;

namespace Server.Api.Hosting
{
    internal sealed class DangerousConfigurationReporter : IHostedService
    {
        private const string LocalEnvironmentName = "Local";

        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<DangerousConfigurationReporter> _logger;
        private readonly ServerOptions _serverOptions;

        public DangerousConfigurationReporter(
            IHostEnvironment hostEnvironment,
            ILogger<DangerousConfigurationReporter> logger,
            IOptions<ServerOptions> serverOptions)
        {
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _serverOptions = serverOptions.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var isLocal = _hostEnvironment.IsEnvironment(LocalEnvironmentName);

            if (_serverOptions.EnableSwagger && isLocal == false && _hostEnvironment.IsDevelopment() == false)
                _logger.LogWarning("[Startup] swagger is enabled outside Local and Development environment = {Environment}", _hostEnvironment.EnvironmentName);

            if (isLocal && _serverOptions.BindAddress == BindAddressType.Any)
                _logger.LogWarning("[Startup] Local environment listens on all interfaces; ops port {OpsPort} is reachable from the network", _serverOptions.OpsPort);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
