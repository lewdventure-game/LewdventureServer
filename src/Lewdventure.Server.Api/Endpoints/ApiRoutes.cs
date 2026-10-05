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
        public const string ConfigBundle = "/api/config/bundle";
        public const string AuthDevice = "/api/auth/device";
        public const string AuthRefresh = "/api/auth/refresh";
        public const string AnalyticsEvents = "/api/analytics/events";
        public const string PlayerProfile = "/api/player/profile";
        public const string PlayerLoadout = "/api/player/loadout";
        public const string PlayerCharacteristics = "/api/player/characteristics";
        public const string PlayerReset = "/api/player/reset";
        public const string PlayerAccount = "/api/player/account";
        public const string PlayerEquipmentLevel = "/api/player/equipment/level";
        public const string PlayerSummonLevel = "/api/player/summon/level";
        public const string PlayerSummonMastery = "/api/player/summon/mastery";
        public const string PlayerSummonLevelReset = "/api/player/summon/level/reset";
        public const string PlayerSummonSkillLevel = "/api/player/summon/skill/level";
        public const string PlayerEquipmentLevelReset = "/api/player/equipment/level/reset";
        public const string PlayerEquipmentMerge = "/api/player/equipment/merge";
        public const string Run = "/api/run";
        public const string AdminConfig = "/admin/config";
        public const string AdminPlayer = "/admin/player";
        public const string AdminExperiments = "/admin/experiments";
        public const string AdminQa = "/admin/qa";
        public const string Health = "/health";
        public const string HealthLive = "/health/live";
        public const string HealthReady = "/health/ready";
    }
}
