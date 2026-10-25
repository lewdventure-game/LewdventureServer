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
        public const string ClearBattleAction = "clear-battle";
        public const string AddPerkGroupAction = "perk-group";
        public const string QaHeroDamageAction = "qa-hero-damage";
        public const string QaSummonDamageAction = "qa-summon-damage";
        public const string QaEnemyDamageAction = "qa-enemy-damage";
        public const string QaEnemyHealthAction = "qa-enemy-health";
        public const string QaEnemyStartHealthAction = "qa-enemy-start-health";
        public const string QaKillEnemiesAction = "qa-kill-enemies";
        public const string QaKillHeroAction = "qa-kill-hero";
        public const string QaActionProgressAction = "qa-action-progress";
        public const string ClearQaAction = "clear-qa";

        public string Action { get; set; } = string.Empty;

        public int Stage { get; set; }

        public int Id { get; set; }

        public float Value { get; set; }

        public int Count { get; set; } = 1;

        public int Battles { get; set; }
    }
}
