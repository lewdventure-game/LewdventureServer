using System.Diagnostics;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Qa;

namespace Server.Api.Diagnostics
{
    internal sealed class RequestTraceMiddleware
    {
        public const string ClientVersionHeader = "X-Client-Version";

        private const string ApiPrefix = "/api/";
        private const string AuthPrefix = "/api/auth/";
        private const string AnalyticsPrefix = "/api/analytics";
        private const int ErrorStatusCode = 400;
        private const int MaxHeaderLength = 64;

        private readonly RequestDelegate _next;
        private readonly int _maxBodyBytes;

        public RequestTraceMiddleware(RequestDelegate next, IOptions<QaDiagnosticsOptions> options)
        {
            _next = next;
            _maxBodyBytes = options.Value.MaxBodyBytes;
        }

        public async Task InvokeAsync(
            HttpContext context,
            PlayerIdentityReader playerIdentityReader,
            QaDiagnosticsQueue qaDiagnosticsQueue,
            TimeProvider timeProvider)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (ShouldTrace(path) == false)
            {
                await _next(context);

                return;
            }

            var userId = playerIdentityReader.Read(context.User);

            if (string.IsNullOrEmpty(userId))
            {
                await _next(context);

                return;
            }

            var startedAt = timeProvider.GetUtcNow().UtcDateTime;
            var stopwatch = Stopwatch.StartNew();
            var requestBody = await ReadRequestBodyAsync(context.Request, path);
            var originalBody = context.Response.Body;
            var capture = new ResponseCaptureStream(originalBody, _maxBodyBytes);
            var error = string.Empty;

            context.Response.Body = capture;

            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;

                throw;
            }
            finally
            {
                context.Response.Body = originalBody;
                stopwatch.Stop();

                var statusCode = error.Length == 0 ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;

                qaDiagnosticsQueue.Enqueue(new RequestTraceDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    CreatedAt = startedAt,
                    UpdatedAt = startedAt,
                    UserId = userId,
                    CorrelationId = context.TraceIdentifier,
                    Method = context.Request.Method,
                    Path = path,
                    ClientVersion = ReadHeader(context.Request, ClientVersionHeader),
                    StatusCode = statusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    RequestBody = requestBody,
                    ResponseBody = statusCode < ErrorStatusCode ? string.Empty : capture.ReadCaptured(),
                    Error = error,
                });
            }
        }

        private bool ShouldTrace(string path)
        {
            return path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase)
                && path.StartsWith(AnalyticsPrefix, StringComparison.OrdinalIgnoreCase) == false;
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request, string path)
        {
            if (path.StartsWith(AuthPrefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            if (request.ContentLength == null || request.ContentLength.Value <= 0)
                return string.Empty;

            request.EnableBuffering();

            var buffer = new byte[_maxBodyBytes];
            var read = 0;

            while (read < buffer.Length)
            {
                var chunk = await request.Body.ReadAsync(buffer.AsMemory(read, buffer.Length - read));

                if (chunk == 0)
                    break;

                read += chunk;
            }

            request.Body.Position = 0;

            return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        }

        private string ReadHeader(HttpRequest request, string name)
        {
            var value = request.Headers[name].ToString();

            return MaxHeaderLength < value.Length ? value.Substring(0, MaxHeaderLength) : value;
        }
    }
}
