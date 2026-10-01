namespace Server.Api.Endpoints
{
    internal sealed class ClientConfigBundle
    {
        public ClientConfigBundle(string version, string entityTag, string json)
        {
            Version = version;
            EntityTag = entityTag;
            Json = json;
        }

        public string Version { get; }

        public string EntityTag { get; }

        public string Json { get; }
    }
}
