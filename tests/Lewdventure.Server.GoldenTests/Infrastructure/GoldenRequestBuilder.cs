using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenRequestBuilder
    {
        public string BuildReplayBody(string requestText, ulong seed)
        {
            JToken token;

            try
            {
                token = JToken.Parse(requestText);
            }
            catch (JsonReaderException)
            {
                return requestText;
            }

            if (token is JObject requestObject == false)
                return requestText;

            var replayObject = (JObject)requestObject.DeepClone();
            replayObject["seed"] = new JValue(seed);

            return replayObject.ToString(Formatting.None);
        }
    }
}
