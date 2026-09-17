using Server.Api.Hosting;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class HealthProbeCommandTests
    {
        [Test]
        public void IsRequested_HealthProbeArgument_ReturnsTrue()
        {
            var command = new HealthProbeCommand();

            Assert.That(command.IsRequested(new[] { "--health-probe" }), Is.True);
        }

        [Test]
        public void IsRequested_OtherArguments_ReturnsFalse()
        {
            var command = new HealthProbeCommand();

            Assert.That(command.IsRequested(new[] { "--urls", "http://localhost" }), Is.False);
            Assert.That(command.IsRequested(Array.Empty<string>()), Is.False);
        }

        [Test]
        public async Task RunAsync_UnreachableUrl_ReturnsOne()
        {
            var command = new HealthProbeCommand();

            var exitCode = await command.RunAsync(new[] { "--health-probe", "http://127.0.0.1:1/health/live" });

            Assert.That(exitCode, Is.EqualTo(1));
        }
    }
}
