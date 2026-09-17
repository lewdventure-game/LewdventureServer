using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoHealthCheck : IHealthCheck
    {
        private const int DegradedThresholdMs = 1000;

        private readonly IMongoDatabaseAccessor _mongoDatabaseAccessor;

        public MongoHealthCheck(IMongoDatabaseAccessor mongoDatabaseAccessor)
        {
            _mongoDatabaseAccessor = mongoDatabaseAccessor;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(TimeSpan.FromSeconds(2));

                await _mongoDatabaseAccessor.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: timeout.Token);

                var elapsedMs = stopwatch.ElapsedMilliseconds;

                if (DegradedThresholdMs < elapsedMs)
                    return HealthCheckResult.Degraded($"ping {elapsedMs}ms");

                return HealthCheckResult.Healthy($"ping {elapsedMs}ms");
            }
            catch (Exception exception) when (exception is OperationCanceledException || exception is TimeoutException || exception is MongoDB.Driver.MongoException)
            {
                return HealthCheckResult.Unhealthy($"ping failed: {exception.GetType().Name}");
            }
        }
    }
}
