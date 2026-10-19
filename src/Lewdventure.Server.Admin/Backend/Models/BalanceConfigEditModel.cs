namespace Server.Admin.Backend.Models
{
    public sealed class BalanceConfigEditModel
    {
        public string Sheet { get; set; } = string.Empty;

        public string Row { get; set; } = string.Empty;

        public string Column { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }
}
