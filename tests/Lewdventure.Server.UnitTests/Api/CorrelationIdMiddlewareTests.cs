using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Api.Http;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class CorrelationIdMiddlewareTests
    {
        [Test]
        public async Task InvokeAsync_ValidIncomingHeader_IsEchoed()
        {
            var context = new DefaultHttpContext();

            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "abc-123";

            await CreateMiddleware().InvokeAsync(context);

            Assert.That(context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString(), Is.EqualTo("abc-123"));
            Assert.That(context.TraceIdentifier, Is.EqualTo("abc-123"));
        }

        [TestCase("bad value with spaces")]
        [TestCase("<script>")]
        [TestCase("0123456789012345678901234567890123456789012345678901234567890123456789")]
        public async Task InvokeAsync_InvalidIncomingHeader_IsReplaced(string incoming)
        {
            var context = new DefaultHttpContext();

            context.Request.Headers[CorrelationIdMiddleware.HeaderName] = incoming;

            await CreateMiddleware().InvokeAsync(context);

            var correlationId = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();

            Assert.That(correlationId, Is.Not.EqualTo(incoming));
            Assert.That(correlationId, Is.Not.Empty);
        }

        private CorrelationIdMiddleware CreateMiddleware()
        {
            return new CorrelationIdMiddleware(CompleteAsync, NullLogger<CorrelationIdMiddleware>.Instance);
        }

        private Task CompleteAsync(HttpContext context)
        {
            return Task.CompletedTask;
        }
    }
}
