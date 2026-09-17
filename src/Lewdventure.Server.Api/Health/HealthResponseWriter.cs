using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Server.Api.Hosting;

namespace Server.Api.Health
{
    internal sealed class HealthResponseWriter
    {
        private readonly JsonWriterOptions _writerOptions = new() { Indented = false };

        public async Task WriteAsync(HttpContext context, HealthReport report)
        {
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var buildInfo = context.RequestServices.GetRequiredService<BuildInfo>();

            context.Response.ContentType = "application/json";

            using var stream = new MemoryStream();

            using (var writer = new Utf8JsonWriter(stream, _writerOptions))
            {
                writer.WriteStartObject();
                writer.WriteString("status", ToStatus(report.Status));
                writer.WriteString("environment", environment.EnvironmentName);
                writer.WriteString("version", buildInfo.Version);
                writer.WriteNumber("uptimeSeconds", (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds);
                writer.WriteStartArray("checks");

                foreach (var entry in report.Entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name", entry.Key);
                    writer.WriteString("status", ToStatus(entry.Value.Status));
                    writer.WriteString("reason", entry.Value.Description ?? string.Empty);
                    writer.WriteNumber("durationMs", (long)entry.Value.Duration.TotalMilliseconds);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            await context.Response.Body.WriteAsync(stream.ToArray());
        }

        private string ToStatus(HealthStatus status)
        {
            if (status == HealthStatus.Healthy)
                return "ok";

            if (status == HealthStatus.Degraded)
                return "warn";

            return "critical";
        }
    }
}
