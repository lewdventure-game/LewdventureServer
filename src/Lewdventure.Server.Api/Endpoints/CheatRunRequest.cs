namespace Server.Api.Endpoints
{
    internal sealed class CheatRunRequest
    {
        public string Action { get; set; } = string.Empty;

        public int Stage { get; set; }

        public int Id { get; set; }

        public float Value { get; set; }

        public int Count { get; set; } = 1;

        public int Battles { get; set; }
    }
}
