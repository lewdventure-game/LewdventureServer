using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace Server.Api.Http
{
    internal sealed class JObjectJsonConverter : JsonConverter<JObject>
    {
        public override JObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);

            return JObject.Parse(document.RootElement.GetRawText());
        }

        public override void Write(Utf8JsonWriter writer, JObject value, JsonSerializerOptions options)
        {
            using var document = JsonDocument.Parse(value.ToString(Newtonsoft.Json.Formatting.None));

            document.RootElement.WriteTo(writer);
        }
    }
}
