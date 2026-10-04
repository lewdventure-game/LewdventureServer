namespace Server.Api.Endpoints
{
    internal sealed class PlayerSummonSkillRequest
    {
        public int SummonId { get; set; }

        public int SkillId { get; set; }

        public string RequestId { get; set; } = string.Empty;
    }
}
