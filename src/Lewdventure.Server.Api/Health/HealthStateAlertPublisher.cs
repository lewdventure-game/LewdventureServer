using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Server.Infrastructure.Alerts;

namespace Server.Api.Health
{
    internal sealed class HealthStateAlertPublisher : IHealthCheckPublisher
    {
        private const string DedupKeyPrefix = "health:";

        private readonly IAlertPublisher _alertPublisher;
        private readonly ILogger<HealthStateAlertPublisher> _logger;

        private HealthStatus? _lastStatus;

        public HealthStateAlertPublisher(IAlertPublisher alertPublisher, ILogger<HealthStateAlertPublisher> logger)
        {
            _alertPublisher = alertPublisher;
            _logger = logger;
        }

        public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
        {
            var previous = _lastStatus;
            var current = report.Status;

            _lastStatus = current;

            if (previous == null)
            {
                if (current != HealthStatus.Healthy)
                    Publish(report, current, previous);

                return Task.CompletedTask;
            }

            if (previous.Value == current)
                return Task.CompletedTask;

            Publish(report, current, previous);

            return Task.CompletedTask;
        }

        private void Publish(HealthReport report, HealthStatus current, HealthStatus? previous)
        {
            var severity = current == HealthStatus.Healthy ? AlertSeverity.Resolved : current == HealthStatus.Degraded ? AlertSeverity.Warning : AlertSeverity.Critical;
            var title = current == HealthStatus.Healthy ? "Server recovered" : $"Server health {ToLabel(current)}";
            var message = new AlertMessage(severity, title, DescribeEntries(report), DedupKeyPrefix + ToLabel(current));

            message.Fields.Add(new KeyValuePair<string, string>("from", previous == null ? "start" : ToLabel(previous.Value)));
            message.Fields.Add(new KeyValuePair<string, string>("to", ToLabel(current)));

            _logger.LogWarning("[Health] state changed from = {Previous} to = {Current}", previous == null ? "start" : ToLabel(previous.Value), ToLabel(current));

            _alertPublisher.Publish(message);
        }

        private string DescribeEntries(HealthReport report)
        {
            var builder = new StringBuilder();

            foreach (var entry in report.Entries)
                builder.Append(entry.Key).Append(": ").Append(ToLabel(entry.Value.Status)).Append(" — ").Append(entry.Value.Description ?? string.Empty).Append('\n');

            return builder.ToString();
        }

        private string ToLabel(HealthStatus status)
        {
            if (status == HealthStatus.Healthy)
                return "ok";

            if (status == HealthStatus.Degraded)
                return "warn";

            return "critical";
        }
    }
}
