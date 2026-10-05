namespace Server.Runs
{
    internal sealed class RunCheatCommand
    {
        public const string StageAction = "stage";
        public const string EventAction = "event";
        public const string HealthAction = "health";
        public const string AddPerkAction = "perk";
        public const string RemovePerkAction = "remove-perk";
        public const string AddBonusAction = "bonus";
        public const string AddStatusAction = "status";
        public const string ClearPerksAction = "clear-perks";
        public const string ClearBonusesAction = "clear-bonuses";
        public const string ClearStatusesAction = "clear-statuses";

        public string Action { get; set; } = string.Empty;

        public int Stage { get; set; }

        public int Id { get; set; }

        public float Value { get; set; }

        public int Count { get; set; } = 1;

        public int Battles { get; set; }
    }
}
