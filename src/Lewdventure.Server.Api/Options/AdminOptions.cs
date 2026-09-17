namespace Server.Api.Options
{
    internal sealed class AdminOptions
    {
        public const string SectionName = "Admin";

        public bool Enabled { get; set; }

        public string ApiKey { get; set; } = string.Empty;

        public string HeaderName { get; set; } = "X-Admin-Key";
    }
}
