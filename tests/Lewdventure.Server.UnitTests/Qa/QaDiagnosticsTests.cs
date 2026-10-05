using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Api.Diagnostics;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo.Qa;
using Server.Infrastructure.Qa;
using Tests.Unit.Api;

namespace Tests.Unit.Qa
{
    [TestFixture]
    public sealed class QaDiagnosticsTests
    {
        private const string UserId = "usr_trace";

        [Test]
        public async Task Middleware_ErrorResponse_IsTracedWithBodies()
        {
            var queue = new QaDiagnosticsQueue();
            var middleware = new RequestTraceMiddleware(WriteResponseAsync(400, "{\"errors\":[\"bad\"]}"), Microsoft.Extensions.Options.Options.Create(new QaDiagnosticsOptions { MaxBodyBytes = 1024 }));
            var context = CreateContext("/api/run/advance", "{\"runId\":\"run_1\"}");

            await middleware.InvokeAsync(context, new PlayerIdentityReader(), queue, TimeProvider.System);

            Assert.That(queue.Traces.TryRead(out var trace), Is.True);
            Assert.That(trace!.UserId, Is.EqualTo(UserId));
            Assert.That(trace.StatusCode, Is.EqualTo(400));
            Assert.That(trace.CorrelationId, Is.EqualTo("corr-1"));
            Assert.That(trace.RequestBody, Is.EqualTo("{\"runId\":\"run_1\"}"));
            Assert.That(trace.ResponseBody, Is.EqualTo("{\"errors\":[\"bad\"]}"));
            Assert.That(await ReadResponseAsync(context), Is.EqualTo("{\"errors\":[\"bad\"]}"));
        }

        [Test]
        public async Task Middleware_SuccessResponse_KeepsNoResponseBody()
        {
            var queue = new QaDiagnosticsQueue();
            var middleware = new RequestTraceMiddleware(WriteResponseAsync(200, "{\"ok\":true}"), Microsoft.Extensions.Options.Options.Create(new QaDiagnosticsOptions()));

            await middleware.InvokeAsync(CreateContext("/api/player/profile", string.Empty), new PlayerIdentityReader(), queue, TimeProvider.System);

            Assert.That(queue.Traces.TryRead(out var trace), Is.True);
            Assert.That(trace!.ResponseBody, Is.Empty);
        }

        [TestCase("/api/auth/refresh")]
        [TestCase("/api/analytics/events")]
        [TestCase("/admin/qa/status")]
        public async Task Middleware_AuthAnalyticsAndAdmin_SkipBodiesOrTrace(string path)
        {
            var queue = new QaDiagnosticsQueue();
            var middleware = new RequestTraceMiddleware(WriteResponseAsync(200, "{}"), Microsoft.Extensions.Options.Options.Create(new QaDiagnosticsOptions()));

            await middleware.InvokeAsync(CreateContext(path, "{\"refreshToken\":\"secret\"}"), new PlayerIdentityReader(), queue, TimeProvider.System);

            if (queue.Traces.TryRead(out var trace))
                Assert.That(trace.RequestBody, Is.Empty);
        }

        [Test]
        public async Task Middleware_Anonymous_IsNotTraced()
        {
            var queue = new QaDiagnosticsQueue();
            var middleware = new RequestTraceMiddleware(WriteResponseAsync(200, "{}"), Microsoft.Extensions.Options.Options.Create(new QaDiagnosticsOptions()));
            var context = CreateContext("/api/player/profile", string.Empty);

            context.User = new ClaimsPrincipal(new ClaimsIdentity());

            await middleware.InvokeAsync(context, new PlayerIdentityReader(), queue, TimeProvider.System);

            Assert.That(queue.Traces.TryRead(out _), Is.False);
        }

        [Test]
        public void ResponseCaptureStream_KeepsOnlyLimit()
        {
            var inner = new MemoryStream();
            var stream = new ResponseCaptureStream(inner, 4);

            stream.Write(Encoding.UTF8.GetBytes("abcdef"), 0, 6);

            Assert.That(stream.ReadCaptured(), Is.EqualTo("abcd"));
            Assert.That(inner.Length, Is.EqualTo(6));
        }

        [Test]
        public void Logger_Warning_IsQueuedWithUserIdFromState()
        {
            var queue = new QaDiagnosticsQueue();
            var provider = new QaErrorLoggerProvider(new HttpContextAccessor(), new PlayerIdentityReader(), queue, TimeProvider.System);
            var logger = provider.CreateLogger("Server.Runs.RunService");

            logger.LogWarning("[Run] something odd userId = {UserId}", UserId);
            logger.LogInformation("[Run] fine userId = {UserId}", UserId);

            Assert.That(queue.Errors.TryRead(out var error), Is.True);
            Assert.That(error!.UserId, Is.EqualTo(UserId));
            Assert.That(error.Level, Is.EqualTo("Warning"));
            Assert.That(error.Message, Does.Contain("something odd"));
            Assert.That(queue.Errors.TryRead(out _), Is.False);
        }

        [Test]
        public void Logger_OwnCategory_IsIgnored()
        {
            var queue = new QaDiagnosticsQueue();
            var provider = new QaErrorLoggerProvider(new HttpContextAccessor(), new PlayerIdentityReader(), queue, TimeProvider.System);

            provider.CreateLogger("Server.Api.Diagnostics.QaDiagnosticsWriterService").LogError("[Qa] write failed");

            Assert.That(queue.Errors.TryRead(out _), Is.False);
        }

        [Test]
        public void Validator_EnabledInProduction_Fails()
        {
            var validator = new QaDiagnosticsOptionsValidator(new TestHostEnvironment("Production"));

            Assert.That(validator.Validate(null, new QaDiagnosticsOptions { Enabled = true }).Failed, Is.True);
            Assert.That(new QaDiagnosticsOptionsValidator(new TestHostEnvironment("Staging")).Validate(null, new QaDiagnosticsOptions { Enabled = true }).Succeeded, Is.True);
        }

        private RequestDelegate WriteResponseAsync(int statusCode, string body)
        {
            return async context =>
            {
                context.Response.StatusCode = statusCode;

                await context.Response.WriteAsync(body);
            };
        }

        private DefaultHttpContext CreateContext(string path, string body)
        {
            var context = new DefaultHttpContext();
            var bytes = Encoding.UTF8.GetBytes(body);

            context.TraceIdentifier = "corr-1";
            context.Request.Method = "POST";
            context.Request.Path = path;
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = bytes.Length;
            context.Response.Body = new MemoryStream();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", UserId) }, "test"));

            return context;
        }

        private async Task<string> ReadResponseAsync(HttpContext context)
        {
            context.Response.Body.Position = 0;

            using var reader = new StreamReader(context.Response.Body);

            return await reader.ReadToEndAsync();
        }
    }
}
