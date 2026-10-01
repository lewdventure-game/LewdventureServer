using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;
using Server.Logging;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class ConfigBundleGoldenTests
    {
        private GoldenTestHost _host = null!;

        private string _activeVersion = string.Empty;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _host = new GoldenTestHost();
            _activeVersion = _host.Services.GetRequiredService<IGameConfigSetProvider>().Current.Version;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _host.Dispose();
        }

        [Test]
        public async Task Bundle_CarriesActiveVersionAndEveryDomain()
        {
            var response = await _host.Client.GetAsync(GoldenHttpClient.ConfigBundlePath, string.Empty);

            Assert.That(response.StatusCode, Is.EqualTo(200));

            var payload = JObject.Parse(response.Body);
            var configs = (JArray)payload["Configs"]!;
            var ordered = new ConfigDomainNames().Ordered;

            Assert.That(payload["Version"]!.Value<string>(), Is.EqualTo(_activeVersion));
            Assert.That(configs.Count, Is.EqualTo(ordered.Count));

            for (int i = 0; i < ordered.Count; i++)
                Assert.That(configs[i]["Name"]!.Value<string>(), Is.EqualTo(ordered[i]));
        }

        [Test]
        public async Task Bundle_BuildsCoreWithTheSameVersion()
        {
            var response = await _host.Client.GetAsync(GoldenHttpClient.ConfigBundlePath, string.Empty);
            var result = new BattleCoreFactory().CreateFromBundle(response.Body, new SilentCoreLog());

            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ConfigVersion, Is.EqualTo(_activeVersion));
        }

        [Test]
        public async Task Bundle_WithKnownEntityTag_IsNotModified()
        {
            var first = await _host.Client.GetAsync(GoldenHttpClient.ConfigBundlePath, string.Empty);

            Assert.That(first.EntityTag, Is.Not.Empty);

            var second = await _host.Client.GetAsync(GoldenHttpClient.ConfigBundlePath, first.EntityTag);

            Assert.That(second.StatusCode, Is.EqualTo(304));
            Assert.That(second.Body, Is.Empty);
        }
    }
}
