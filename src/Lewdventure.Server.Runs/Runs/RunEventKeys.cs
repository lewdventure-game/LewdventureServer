namespace Server.Runs
{
    internal sealed class RunEventKeys
    {
        public const string LocKey = "loc_key";
        public const string LocKeyStart = "loc_key_start";
        public const string LocKeyEnd = "loc_key_end";
        public const string Rewards = "rewards";
        public const string Enemies = "enemies";
        public const string ExperienceResource = "exp_lvl";
        public const string LegacyExperienceResource = "level_exp";

        public string ForkButtonKey(int button)
        {
            return "loc_key_button" + button;
        }

        public string ForkChanceKey(int branch)
        {
            return "chance_event" + branch;
        }

        public string ForkLocKey(int branch)
        {
            return "loc_key_event" + branch;
        }

        public string ForkRewardsKey(int branch)
        {
            return "rewards_event" + branch;
        }

        public string ForkRewardLengthKey(int branch)
        {
            return "reward_lenght_event" + branch;
        }
    }
}
