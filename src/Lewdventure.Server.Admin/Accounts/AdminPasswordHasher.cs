using System.Security.Cryptography;
using System.Text;

namespace Server.Admin.Accounts
{
    internal sealed class AdminPasswordHasher
    {
        public const int Iterations = 210000;

        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        private const int PasswordLength = 16;

        public void SetPassword(AdminAccount account, string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);

            account.PasswordSalt = Convert.ToBase64String(salt);
            account.Iterations = Iterations;
            account.PasswordHash = Convert.ToBase64String(Derive(password, salt, Iterations));
        }

        public bool Verify(AdminAccount account, string password)
        {
            if (string.IsNullOrEmpty(account.PasswordHash) || string.IsNullOrEmpty(account.PasswordSalt) || account.Iterations <= 0)
                return false;

            var expected = Convert.FromBase64String(account.PasswordHash);
            var actual = Derive(password, Convert.FromBase64String(account.PasswordSalt), account.Iterations);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        public string GeneratePassword()
        {
            var builder = new StringBuilder(PasswordLength);

            for (int i = 0; i < PasswordLength; i++)
                builder.Append(PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)]);

            return builder.ToString();
        }

        private byte[] Derive(string password, byte[] salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, HashBytes);
        }
    }
}
