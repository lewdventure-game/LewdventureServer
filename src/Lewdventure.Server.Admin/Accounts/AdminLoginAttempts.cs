namespace Server.Admin.Accounts
{
    internal sealed class AdminLoginAttempts
    {
        public int Failures { get; set; }

        public DateTimeOffset LockedUntil { get; set; }

        public DateTimeOffset LastFailureAt { get; set; }
    }
}
