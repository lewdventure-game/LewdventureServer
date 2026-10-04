namespace Server.Admin.Accounts
{
    internal sealed class AdminAccount
    {
        public string Login { get; set; } = string.Empty;

        public string Role { get; set; } = AdminRoles.Tester;

        public string PasswordHash { get; set; } = string.Empty;

        public string PasswordSalt { get; set; } = string.Empty;

        public int Iterations { get; set; }

        public bool IsDisabled { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastLoginAt { get; set; }
    }
}
