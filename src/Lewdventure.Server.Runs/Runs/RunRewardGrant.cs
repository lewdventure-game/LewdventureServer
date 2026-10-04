namespace Server.Runs
{
    internal sealed class RunRewardGrant
    {
        public RunRewardGrant(int experience, bool isConflict)
        {
            Experience = experience;
            IsConflict = isConflict;
        }

        public int Experience { get; }

        public bool IsConflict { get; }
    }
}
