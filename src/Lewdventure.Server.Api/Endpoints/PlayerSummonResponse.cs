namespace Server.Api.Endpoints
{
    internal sealed class PlayerSummonResponse
    {
        public int Id { get; set; }

        public int Copies { get; set; }

        public int Level { get; set; }

        public int MasteryLevel { get; set; }
    }
}
