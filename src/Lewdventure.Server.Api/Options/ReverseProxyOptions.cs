namespace Server.Api.Options
{
    internal sealed class ReverseProxyOptions
    {
        public const string SectionName = "ReverseProxy";

        public bool Enabled { get; set; }

        public List<string> KnownNetworks { get; set; } = new();

        public List<string> KnownProxies { get; set; } = new();
    }
}
