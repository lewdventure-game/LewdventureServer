using Newtonsoft.Json.Linq;

namespace Server.LoadTest
{
    internal sealed class PlayerResponse
    {
        public PlayerResponse(int statusCode, JObject? body, string entityTag)
        {
            StatusCode = statusCode;
            Body = body;
            EntityTag = entityTag;
        }

        public int StatusCode { get; }

        public JObject? Body { get; }

        public string EntityTag { get; }
    }
}
