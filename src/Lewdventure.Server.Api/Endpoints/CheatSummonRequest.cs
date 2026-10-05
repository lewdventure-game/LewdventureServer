namespace Server.Api.Endpoints
{
    internal sealed class CheatSummonRequest
    {
        public int? Level { get; set; }

        public int? MasteryLevel { get; set; }

        public int? SkillLevel { get; set; }
    }
}
