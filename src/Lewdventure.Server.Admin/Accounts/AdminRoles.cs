namespace Server.Admin.Accounts
{
    internal sealed class AdminRoles
    {
        public const string Admin = "admin";
        public const string Tester = "tester";
        public const string AdminPolicy = "admin-only";

        public bool IsKnown(string role)
        {
            return string.Equals(role, Admin, StringComparison.Ordinal) || string.Equals(role, Tester, StringComparison.Ordinal);
        }
    }
}
