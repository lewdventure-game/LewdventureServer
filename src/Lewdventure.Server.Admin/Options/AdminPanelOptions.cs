namespace Server.Admin.Options
{
    internal sealed class AdminPanelOptions
    {
        public const string SectionName = "AdminPanel";

        public string DataPath { get; set; } = "data";

        public int SessionHours { get; set; } = 12;

        public int MaxFailedLogins { get; set; } = 5;

        public int LockoutMinutes { get; set; } = 15;

        public AdminClickHouseOptions ClickHouse { get; set; } = new();

        public List<AdminEnvironmentOptions> Environments { get; set; } = new();
    }
}
