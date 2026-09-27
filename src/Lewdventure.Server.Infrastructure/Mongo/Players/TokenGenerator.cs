using System.Security.Cryptography;

namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class TokenGenerator
    {
        private const int TokenBytes = 32;

        public string CreateToken()
        {
            return Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        }

        public string Hash(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value);

            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        public bool AreEqual(string left, string right)
        {
            if (left.Length != right.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(left),
                System.Text.Encoding.UTF8.GetBytes(right));
        }

        private string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
