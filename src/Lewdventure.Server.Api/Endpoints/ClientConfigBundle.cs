namespace Server.Api.Endpoints
{
    internal sealed class ClientConfigBundle
    {
        public ClientConfigBundle(string version, string entityTagValue, string json)
        {
            Version = version;
            EntityTagValue = entityTagValue;
            Json = json;
        }

        public string Version { get; }

        public string EntityTagValue { get; }

        public string EntityTag => "\"" + EntityTagValue + "\"";

        public string Json { get; }
    }
}
