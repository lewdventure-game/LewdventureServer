using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class OpsPortGuardMiddlewareTests
    {
        private const int PublicPort = 5000;
        private const int OpsPort = 9090;

        [TestCase(true, OpsPort, 200)]
        [TestCase(true, PublicPort, 404)]
        [TestCase(false, PublicPort, 200)]
        [TestCase(false, OpsPort, 404)]
        public async Task InvokeAsync_RoutesByPort(bool isOpsEndpoint, int localPort, int expectedStatusCode)
        {
            var context = new DefaultHttpContext();
            var nextCalled = false;
            var metadata = isOpsEndpoint ? new EndpointMetadataCollection(new OpsPortOnlyMetadata()) : new EndpointMetadataCollection();

            context.Connection.LocalPort = localPort;
            context.SetEndpoint(new Endpoint(CompleteAsync, metadata, "test"));

            var middleware = new OpsPortGuardMiddleware(
                nextContext =>
                {
                    nextCalled = true;

                    return Task.CompletedTask;
                },
                Microsoft.Extensions.Options.Options.Create(new ServerOptions { PublicPort = PublicPort, OpsPort = OpsPort }));

            await middleware.InvokeAsync(context);

            Assert.That(nextCalled, Is.EqualTo(expectedStatusCode == 200));
            Assert.That(context.Response.StatusCode, Is.EqualTo(expectedStatusCode));
        }

        private Task CompleteAsync(HttpContext context)
        {
            return Task.CompletedTask;
        }
    }
}
