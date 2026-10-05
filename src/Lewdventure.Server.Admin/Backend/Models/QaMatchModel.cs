namespace Server.Admin.Backend.Models
{
    public sealed class QaMatchModel
    {
        public string UserId { get; set; } = string.Empty;

        public string Alias { get; set; } = string.Empty;

        public string MatchedBy { get; set; } = string.Empty;
    }
}
