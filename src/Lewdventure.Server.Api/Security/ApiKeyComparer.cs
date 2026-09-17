using System.Security.Cryptography;
using System.Text;

namespace Server.Api.Security
{
    internal sealed class ApiKeyComparer
    {
        public bool AreEqual(string provided, string expected)
        {
            if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
                return false;

            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
    }
}
