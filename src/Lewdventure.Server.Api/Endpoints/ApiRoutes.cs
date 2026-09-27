namespace Server.Api.Endpoints
{
    internal sealed class ApiRoutes
    {
        public const string Root = "/";
        public const string Ping = "/api/ping";
        public const string SimulateBattle = "/api/battle/simulate";
        public const string ReplayBattle = "/api/battle/replay";
        public const string ConfigStatus = "/api/config/status";
        public const string ConfigSheets = "/api/config/sheets";
        public const string ConfigUpload = "/api/config/upload";
        public const string AuthDevice = "/api/auth/device";
        public const string AuthRefresh = "/api/auth/refresh";
        public const string PlayerProfile = "/api/player/profile";
        public const string PlayerLoadout = "/api/player/loadout";
        public const string PlayerCharacteristics = "/api/player/characteristics";
        public const string PlayerEquipmentLevel = "/api/player/equipment/level";
        public const string PlayerSummonLevel = "/api/player/summon/level";
        public const string PlayerSummonMastery = "/api/player/summon/mastery";
        public const string Run = "/api/run";
        public const string AdminConfig = "/admin/config";
        public const string AdminPlayer = "/admin/player";
        public const string Health = "/health";
        public const string HealthLive = "/health/live";
        public const string HealthReady = "/health/ready";
    }
}
