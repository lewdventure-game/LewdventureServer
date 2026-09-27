namespace Server.Api.Endpoints
{
    internal sealed class PlayerCharacterResponse
    {
        public int Id { get; set; }

        public int Copies { get; set; }

        public int UpgradesApplied { get; set; }
    }
}
