using System.ComponentModel.DataAnnotations;

namespace Server.Battles
{
    internal sealed class BattleReplayData : IBattleReplayData
    {
        [Required] public ITeamSnapshot TeamA { get; set; } = null!;

        [Required] public ITeamSnapshot TeamB { get; set; } = null!;

        public int StoryLevelId { get; set; }

        public int StageId { get; set; }

        public ulong Seed { get; set; }
    }
}
