namespace Server.Api.Endpoints
{
    internal sealed class ApiRoutes
    {
        public const string Root = "/";
        public const string Ping = "/api/ping";
        public const string SimulateBattle = "/api/battle/simulate";
        public const string ReplayBattle = "/api/battle/replay";
        public const string UpdateConfig = "/api/config/update";
        public const string PublishConfig = "/api/config/publish";
        public const string ConfigStatus = "/api/config/status";
        public const string AdminConfig = "/admin/config";
        public const string Health = "/health";
        public const string HealthLive = "/health/live";
        public const string HealthReady = "/health/ready";
    }
}
