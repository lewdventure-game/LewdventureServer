using System.Diagnostics;

namespace Server.Api.Hosting
{
    internal sealed class GracefulShutdownService : IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ILogger<GracefulShutdownService> _logger;

        private long _stoppingTimestamp;

        public GracefulShutdownService(IHostApplicationLifetime hostApplicationLifetime, ILogger<GracefulShutdownService> logger)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            var elapsedMs = _stoppingTimestamp == 0 ? 0 : Stopwatch.GetElapsedTime(_stoppingTimestamp).TotalMilliseconds;

            _logger.LogInformation("[Shutdown] hosted services stopped elapsedMs = {ElapsedMs}", (long)elapsedMs);

            return Task.CompletedTask;
        }

        private void OnStopping()
        {
            _stoppingTimestamp = Stopwatch.GetTimestamp();

            _logger.LogInformation("[Shutdown] server stopping");
        }
    }
}
