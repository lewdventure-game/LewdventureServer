namespace Server.Admin.Backend.Models
{
    public sealed class BalanceVersionModel
    {
        public string Version { get; set; } = string.Empty;

        public string ShortVersion { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
