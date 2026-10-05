namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserQaDocument
    {
        public string Alias { get; set; } = string.Empty;

        public string MarkedBy { get; set; } = string.Empty;

        public DateTime MarkedAt { get; set; }
    }
}
