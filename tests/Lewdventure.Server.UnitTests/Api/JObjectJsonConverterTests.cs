using System.Text.Json;
using Newtonsoft.Json.Linq;
using Server.Api.Http;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class JObjectJsonConverterTests
    {
        private readonly JsonSerializerOptions _options = CreateOptions();

        [Test]
        public void Write_KeepsScalarValues()
        {
            var parameters = new JObject
            {
                ["actorId"] = 1,
                ["damage"] = 63f,
                ["isCritical"] = false,
                ["animationKey"] = "cast",
            };

            var json = JsonSerializer.Serialize(parameters, _options);

            Assert.That(json, Does.Contain("\"actorId\":1"));
            Assert.That(json, Does.Contain("\"isCritical\":false"));
            Assert.That(json, Does.Contain("\"animationKey\":\"cast\""));
            Assert.That(json, Does.Not.Contain("[]"), "значения снова сериализуются пустыми массивами");
        }

        [Test]
        public void Write_InsideResponseObject_KeepsValues()
        {
            var command = new { commandType = 5, parameters = new JObject { ["damage"] = 63f } };

            var json = JsonSerializer.Serialize(command, _options);

            Assert.That(json, Does.Contain("\"damage\":63"));
        }

        [Test]
        public void Read_RestoresObject()
        {
            var restored = JsonSerializer.Deserialize<JObject>("{\"damage\":63,\"animationKey\":\"cast\"}", _options);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!["damage"]!.Value<float>(), Is.EqualTo(63f));
            Assert.That(restored["animationKey"]!.Value<string>(), Is.EqualTo("cast"));
        }

        private static JsonSerializerOptions CreateOptions()
        {
            var options = new JsonSerializerOptions();

            options.Converters.Add(new JObjectJsonConverter());

            return options;
        }
    }
}
